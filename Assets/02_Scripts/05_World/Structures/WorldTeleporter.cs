using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>E 상호작용 구조물. 이동은 사라질 수 있는 청크 인스턴스 대신 플레이어 수명에 연결합니다.</summary>
public sealed class WorldTeleporter : Structure, IDisposeInitializable
{
    private int _id;
    private static readonly Collider[] Blockers = new Collider[32];

    public void InitializeDispose(DisposeData dispose, ChunkData chunk) => _id = dispose.instanceId;

    private void OnDisable() => _id = 0;

    public override bool CanInteract(InteractionContext context)
    {
        Player player = context.Instigator != null ? context.Instigator.GetComponentInParent<Player>() : null;
        // 비용 부족도 상호작용 대상으로 유지해야 E가 장착 동작으로 폴백하지 않습니다.
        return player != null && player.IsLocalPlayer && _id != 0;
    }

    public override void Interact(InteractionContext context)
    {
        if (!CanInteract(context)) return;
        Player player = context.Instigator.GetComponentInParent<Player>();
        var entry = WorldGenManager.Instance?.CurrentLogicData?.FindTeleportByInstanceId(_id);
        if (entry == null || player.Motor == null || player.IsWorldTeleporting || !player.IsAlive || player.InBossArena) return;
        if (!player.Stat.CanPayTeleport(entry.cost, entry.amount))
        {
            Toast.Show("텔레포트 비용이 부족합니다. HP는 최소 1이 남아야 합니다.");
            return;
        }
        player.IsWorldTeleporting = true;
        player.InputData?.SuppressAllInputs();
        player.Motor.ResetVelocity();
        int version = ++player.WorldTeleportRequestVersion;
        player.IsWorldTeleportApprovalPending = true;
#if PHOTON_FUSION
        if (player.TryGetComponent<NetworkPlayerSync>(out var sync) && sync.Object != null && sync.Object.IsValid)
        {
            sync.RequestWorldTeleport(_id, version);
            WaitForApprovalAsync(player, version).Forget();
            return;
        }
#endif
        CompleteRequest(player, _id, version, IsRequestValid(player, _id));
    }

    /// <summary>호스트는 로드되지 않은 출입구도 월드 데이터로 조회해 생존/사용 거리를 확인합니다.</summary>
    public static bool IsRequestValid(Player player, int id)
    {
        var logic = WorldGenManager.Instance?.CurrentLogicData;
        var source = logic?.FindTeleportByInstanceId(id);
        return player != null && player.isActiveAndEnabled && player.IsAlive && !player.InBossArena && source != null &&
            logic.FindTeleportByInstanceId(source.targetId) != null &&
            (player.transform.position - source.Position(logic)).sqrMagnitude <= 36f;
    }

    public static void CompleteRequest(Player player, int id, int version, bool approved)
    {
        if (player == null || !player.IsLocalPlayer || !player.IsWorldTeleporting ||
            !player.IsWorldTeleportApprovalPending || player.WorldTeleportRequestVersion != version) return;
        player.IsWorldTeleportApprovalPending = false;
        if (!approved)
        {
            player.IsWorldTeleporting = false;
            Toast.Show("현재 위치에서는 텔레포트할 수 없습니다.");
            return;
        }
        TravelAsync(player, id, version).Forget();
    }

    private static async UniTaskVoid WaitForApprovalAsync(Player player, int version)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(10), DelayType.UnscaledDeltaTime,
                cancellationToken: player.GetCancellationTokenOnDestroy());
            if (player.WorldTeleportRequestVersion != version || !player.IsWorldTeleportApprovalPending) return;
            player.IsWorldTeleportApprovalPending = false;
            player.IsWorldTeleporting = false;
            Toast.Show("텔레포트 요청 시간이 초과되었습니다. 다시 시도해 주세요.");
        }
        catch (OperationCanceledException) { }
    }

    private static async UniTaskVoid TravelAsync(Player player, int id, int version)
    {
        // 청크 갱신은 목적지 준비와 이동을 마친 뒤 기존 스트리밍 루프에 맡깁니다.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(player.GetCancellationTokenOnDestroy());
        timeout.CancelAfterSlim(TimeSpan.FromSeconds(30));
        try
        {
            WorldGenManager world = WorldGenManager.Instance;
            WorldLogicData logic = world?.CurrentLogicData;
            WorldTeleportPlacement source = logic?.FindTeleportByInstanceId(id);
            WorldTeleportPlacement target = source != null ? logic.FindTeleportByInstanceId(source.targetId) : null;
            if (source == null || target == null || world.ChunkDirector == null) return;
            Vector3 destination = target.ArrivalPosition(logic);
            await world.ChunkDirector.PrepareTeleportAsync(destination, timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
            if (world == null || world.CurrentLogicData != logic || !player.IsWorldTeleporting ||
                player.WorldTeleportRequestVersion != version || !IsRequestValid(player, id) ||
                Main.Save != null && (Main.Save.IsCapturing || Main.Save.IsRestoring)) return;
            Physics.SyncTransforms();
            if (!TryLanding(player, destination, out destination))
            {
                Toast.Show("도착 지점에 장애물이 있어 이동할 수 없습니다.");
                return;
            }
            if (player.Motor == null) return;
            if (!player.Stat.TryPayTeleport(source.cost, source.amount))
            {
                Toast.Show("텔레포트 비용이 부족합니다.");
                return;
            }
            // 실제 이동 전 마지막 단계에서만 비용을 지불합니다. 방어력/무적은 비용에 영향을 주지 않습니다.
            player.Motor.Teleport(destination);
            player.InputData?.SuppressAllInputs();
        }
        catch (OperationCanceledException)
        {
            if (player != null) Toast.Show("텔레포트 준비가 취소되었습니다.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (player != null) Toast.Show("텔레포트 준비에 실패했습니다.");
        }
        finally
        {
            if (player != null && player.WorldTeleportRequestVersion == version) player.IsWorldTeleporting = false;
        }
    }

    private static bool TryLanding(Player player, Vector3 expected, out Vector3 position)
    {
        position = expected;
        int ground = LayerMask.NameToLayer("Ground");
        if (ground < 0 || !Physics.Raycast(expected + Vector3.up * 8f, Vector3.down, out var hit,
                16f, 1 << ground, QueryTriggerInteraction.Ignore) || !(hit.collider is TerrainCollider) || hit.normal.y < 0.65f)
            return false;
        position = hit.point + Vector3.up * 0.15f;
        CharacterController cc = player.GetComponent<CharacterController>();
        float radius = cc != null ? cc.radius * Mathf.Max(Mathf.Abs(player.transform.lossyScale.x), Mathf.Abs(player.transform.lossyScale.z)) : 0.4f;
        float height = cc != null ? Mathf.Max(radius * 2f, cc.height * Mathf.Abs(player.transform.lossyScale.y)) : 1.8f;
        Vector3 center = position + (cc != null ? player.transform.TransformVector(cc.center) : Vector3.up * 0.9f);
        Vector3 half = Vector3.up * (height * 0.5f - radius);
        int count = Physics.OverlapCapsuleNonAlloc(center - half, center + half, Mathf.Max(0.05f, radius - 0.02f),
            Blockers, ~0, QueryTriggerInteraction.Ignore);
        if (count == Blockers.Length) return false;
        for (int i = 0; i < count; i++)
            if (Blockers[i] != null && !(Blockers[i] is TerrainCollider) &&
                !Blockers[i].transform.IsChildOf(player.transform)) return false;
        return true;
    }
}
