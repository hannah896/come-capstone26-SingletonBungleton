using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 월문(月門): 최종 보스로 들어가는 포털. (설계: docs/기획_보스포탈_조건.md)
///
/// 규칙
///  - 보름달이 뜬 날 하루 동안만 열린다. 한 번 도전하면(성공/실패 무관) 그날은 다시 열리지 않는다.
///  - 열린 문에서 E키 → 5초 카운트다운 후 문 반경 안의 생존 플레이어 전원이 같은 아레나로 이동한다.
///  - 아레나에서 보스를 쓰러뜨리면 엔딩, 아레나 안 생존자가 없어지면 실패(다음 보름에 재도전).
///
/// 멀티플레이: 판정(개방/참가자/보스 스폰/종료)은 호스트(싱글이면 본인)가 하고,
/// 결과는 NetworkPlayerSync의 RPC로 모든 피어에 알린다. 아레나는 모든 피어가 같은 월드 좌표에 각자 만든다.
/// </summary>
public class MoonGate : Structure
{
    public enum GateState { Closed, Open, Entering, InProgress, Cleared }

    /// <summary>호스트가 모든 피어에 보내는 진행 이벤트 코드.</summary>
    public enum PortalEvent : byte
    {
        Countdown = 1,   // 입장 카운트다운 시작
        Begin = 2,       // 전원 이동, 아레나 생성
        Ending = 3,      // 보스 처치 → 엔딩
        Failed = 4,      // 아레나 안 생존자 없음 → 실패
        Cancelled = 5,   // 입장 취소(문 앞에 아무도 없음)
        ArenaClosed = 6, // 아레나 제거
    }

    private const int MoonCycleDays = 8;
    private const int FullMoonIndex = 4;

    public static MoonGate Instance { get; private set; }

    [Header("개방 / 입장")]
    [Tooltip("이 반경(m) 안의 생존 플레이어가 함께 입장한다")]
    [SerializeField] private float gatherRadius = 6f;
    [SerializeField] private float countdownSeconds = 5f;
    [Tooltip("테스트용: 켜면 보름달이 아니어도, 이미 도전한 날에도 열린다. 배포 전에 끌 것.")]
    [SerializeField] private bool debugAlwaysOpen = false;

    [Header("보스 아레나")]
    [SerializeField] private GameObject arenaPrefab;
    [Tooltip("문 위치 기준 아레나 오프셋. 지형과 겹치지 않게 하늘 높이에 둔다.")]
    [SerializeField] private Vector3 arenaOffset = new(0f, 400f, 0f);
    [Tooltip("MonsterCatalog에 등록된 보스 키. 아레나 프리팹 안에 FinalBossEncounter가 있으면 이 키 대신 그쪽이 최종 보스(월식룡 2페이즈)를 진행한다.")]
    [SerializeField] private string bossMonsterKey = "Treeguard";

    [Header("연출")]
    [Tooltip("열려 있을 때만 켜지는 포털 면")]
    [SerializeField] private GameObject portalVisual;
    [SerializeField] private Light portalLight;

    public GateState State { get; private set; } = GateState.Closed;

    private int lastAttemptDay = -1;
    private bool visualOpen;
    private float pollTimer;
    private BossArena arena;
    private Monster boss;
    private FinalBossEncounter encounter;   // 아레나에 최종 보스전 관리자가 있으면 그걸로 진행한다
    private readonly List<Player> participants = new();

    private Vector3 ArenaOrigin => transform.position + arenaOffset;
    private bool Busy => State == GateState.Entering || State == GateState.InProgress || State == GateState.Cleared;

    #region 생명주기

    private void OnEnable()
    {
        if (Instance == null) Instance = this;
        if (Main.Loop != null) Main.Loop.OnUpdate += Tick;
        ApplyVisual(false);
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
        if (Main.Loop != null) Main.Loop.OnUpdate -= Tick;
    }

    // 달 위상은 0.5초마다 확인한다. 열림/닫힘이 바뀌는 순간 연출과 안내 메시지를 낸다.
    private void Tick(float deltaTime)
    {
        pollTimer -= deltaTime;
        if (pollTimer > 0f) return;
        pollTimer = 0.5f;

        bool open = IsOpenNow();
        if (!Busy) State = open ? GateState.Open : GateState.Closed;

        if (open == visualOpen) return;

        ApplyVisual(open);
        if (open)
            Toast.Show("월문이 열렸습니다! 보름달이 뜬 오늘, 파티를 모아 문 앞에서 E키로 입장하세요.", 5f, ToastColor.Yellow, ToastPosition.TopCenter);
    }

    private void ApplyVisual(bool open)
    {
        visualOpen = open;
        if (portalVisual != null) portalVisual.SetActive(open);
        if (portalLight != null) portalLight.enabled = open;
    }

    // 도전 중에는 부수지 못한다
    public override bool CanDemolish() => !Busy;

    #endregion

    #region 개방 판정

    private bool IsOpenNow()
    {
        if (Busy) return false;
        if (debugAlwaysOpen) return true;

        WorldClock clock = WorldClock.Instance;
        if (clock == null) return false;

        return clock.CurrentMoonPhase == MoonPhase.Full && clock.CurrentDay != lastAttemptDay;
    }

    /// <summary>다음 보름까지 남은 일수. 오늘이 보름이면 0.</summary>
    private static int DaysUntilFullMoon()
    {
        WorldClock clock = WorldClock.Instance;
        if (clock == null) return -1;

        int index = (clock.CurrentDay - 1) % MoonCycleDays;
        return (FullMoonIndex - index + MoonCycleDays) % MoonCycleDays;
    }

    #endregion

    #region 상호작용 (E키)

    public override bool CanInteract(InteractionContext context) => true;

    public override void Interact(InteractionContext context)
    {
        Player player = context.Instigator != null ? context.Instigator.GetComponent<Player>() : null;
        if (player == null) return;

        if (State == GateState.Cleared)
        {
            Toast.Show("월문은 이미 닫혔습니다.", 3f, ToastColor.Yellow, ToastPosition.TopCenter);
            return;
        }

        if (Busy)
        {
            Toast.Show("월문 안에서 이미 전투가 벌어지고 있습니다.", 3f, ToastColor.Yellow, ToastPosition.TopCenter);
            return;
        }

        if (!IsOpenNow())
        {
            int days = DaysUntilFullMoon();
            bool attemptedToday = WorldClock.Instance != null && WorldClock.Instance.CurrentDay == lastAttemptDay;
            string message = attemptedToday
                ? "오늘은 이미 도전했습니다. 다음 보름에 다시 열립니다."
                : days > 0 ? $"월문은 보름달이 뜨는 날에만 열립니다. (보름까지 {days}일)"
                : "월문은 보름달이 뜨는 날에만 열립니다.";
            Toast.Show(message, 3f, ToastColor.Yellow, ToastPosition.TopCenter);
            return;
        }

        RequestEnter(player);
    }

    private static void RequestEnter(Player player)
    {
#if PHOTON_FUSION
        if (Main.Network != null && Main.Network.IsInRoom)
        {
            // 판정은 호스트가 한다
            if (player.TryGetComponent(out NetworkPlayerSync sync)) sync.RequestEnterGate();
            return;
        }
#endif
        HostHandleEnterRequest(player);
    }

    #endregion

    #region 호스트 판정

    /// <summary>입장 요청을 처리한다. 싱글 또는 호스트에서만 동작한다.</summary>
    public static void HostHandleEnterRequest(Player requester)
    {
        if (Instance == null || requester == null) return;
        if (!Monster.IsSimulatedPeer) return;

        Instance.HostBeginEntry();
    }

    private void HostBeginEntry()
    {
        if (!IsOpenNow()) return;

        State = GateState.Entering;
        Broadcast(PortalEvent.Countdown);
        EntryAsync().Forget();
    }

    private async UniTaskVoid EntryAsync()
    {
        var token = this.GetCancellationTokenOnDestroy();
        await UniTask.Delay(System.TimeSpan.FromSeconds(countdownSeconds), cancellationToken: token).SuppressCancellationThrow();
        if (this == null) return;

        List<Player> gathered = CollectParticipants();
        if (gathered.Count == 0)
        {
            State = GateState.Open;
            Broadcast(PortalEvent.Cancelled);
            return;
        }

        participants.Clear();
        participants.AddRange(gathered);
        State = GateState.InProgress;

        // 모든 피어가 같은 월드 좌표에 아레나를 만든다 (호스트는 바로 쓸 수 있도록 직접도 보장한다)
        Broadcast(PortalEvent.Begin);
        EnsureArena();
        if (arena == null)
        {
            Debug.LogError("[MoonGate] 아레나 프리팹이 없어 입장을 취소합니다.");
            State = GateState.Open;
            return;
        }

        for (int i = 0; i < participants.Count; i++)
            Teleport(participants[i], arena.GetPlayerSpawn(i), true);

        encounter = arena.GetComponentInChildren<FinalBossEncounter>(true);
        if (encounter != null)
        {
            // 최종 보스(월식룡): 페이즈·인원 보정·광폭화는 관리자가 맡고, 2페이즈까지 잡으면 Cleared가 온다
            encounter.Cleared -= OnEncounterCleared;
            encounter.Cleared += OnEncounterCleared;
            encounter.StartEncounter(participants.Count);
        }
        else
        {
            await SpawnBossAsync();
        }
        MonitorAsync().Forget();
    }

    // 문 반경 안의 생존 플레이어 (모든 피어의 캐릭터가 호스트 씬에 있으므로 거리로 모은다)
    private List<Player> CollectParticipants()
    {
        var result = new List<Player>();
        foreach (Player player in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (player == null || !player.IsAlive) continue;

            Vector3 d = player.transform.position - transform.position;
            d.y = 0f;
            if (d.magnitude <= gatherRadius) result.Add(player);
        }

        return result;
    }

    private async UniTask SpawnBossAsync()
    {
        MonsterCatalog catalog = MonsterCatalog.Instance;
        byte catalogId = catalog != null ? catalog.GetCatalogId(bossMonsterKey) : (byte)0;
        if (catalogId == 0)
        {
            Debug.LogError($"[MoonGate] '{bossMonsterKey}'가 MonsterCatalog에 없습니다. 보스를 소환하지 못했습니다.");
            return;
        }

        MonsterCatalog.Entry entry = catalog.GetEntry(catalogId);
        Monster spawned = await Monster.SpawnAsync(entry.addressableKey, entry.statData, arena.BossSpawn);
        if (spawned == null) return;

        boss = spawned;
        boss.Died += OnBossDied;

#if PHOTON_FUSION
        NetworkMonsterDirector.Instance?.RegisterMonster(boss, catalogId);
#endif
    }

    private void OnBossDied(Monster monster)
    {
        if (monster != boss) return;
        HostFinish(true);
    }

    private void OnEncounterCleared() => HostFinish(true);

    // 1초마다 아레나 안 생존자를 확인한다. 아무도 없으면 실패.
    private async UniTaskVoid MonitorAsync()
    {
        var token = this.GetCancellationTokenOnDestroy();
        while (State == GateState.InProgress)
        {
            bool cancelled = await UniTask.Delay(1000, cancellationToken: token).SuppressCancellationThrow();
            if (cancelled || this == null) return;
            if (State != GateState.InProgress) return;

            int alive = 0;
            foreach (Player player in participants)
            {
                if (player != null && player.IsAlive && arena != null && arena.Contains(player.transform.position))
                    alive++;
            }

            if (alive == 0)
            {
                HostFinish(false);
                return;
            }
        }
    }

    private void HostFinish(bool cleared)
    {
        if (State != GateState.InProgress) return;

        if (cleared)
        {
            State = GateState.Cleared;
            Broadcast(PortalEvent.Ending);
            return;
        }

        State = GateState.Closed;
        Broadcast(PortalEvent.Failed);

        if (encounter != null)
        {
            encounter.Cleared -= OnEncounterCleared;
            encounter.Abort();
            encounter = null;
        }

        if (boss != null && boss.isActiveAndEnabled)
        {
            boss.Died -= OnBossDied;
            Extensions.Despawn(boss.gameObject);
        }

        boss = null;
        CloseArenaLaterAsync().Forget();
    }

    private async UniTaskVoid CloseArenaLaterAsync()
    {
        var token = this.GetCancellationTokenOnDestroy();
        bool cancelled = await UniTask.Delay(System.TimeSpan.FromSeconds(5f), cancellationToken: token).SuppressCancellationThrow();
        if (cancelled || this == null) return;

        Broadcast(PortalEvent.ArenaClosed);
    }

    private void Teleport(Player player, Vector3 position, bool inArena)
    {
        if (player == null) return;

#if PHOTON_FUSION
        if (Main.Network != null && Main.Network.IsInRoom && player.TryGetComponent(out NetworkPlayerSync sync))
        {
            sync.ForwardPortalTeleport(position, inArena);
            return;
        }
#endif
        player.ApplyPortalTeleport(position, inArena);
    }

    // 멀티면 RPC로 모든 피어(호스트 포함)에 알리고, 싱글이면 직접 처리한다.
    private void Broadcast(PortalEvent code)
    {
#if PHOTON_FUSION
        if (Main.Network != null && Main.Network.IsInRoom)
        {
            foreach (Player player in FindObjectsByType<Player>(FindObjectsSortMode.None))
            {
                if (player != null && player.TryGetComponent(out NetworkPlayerSync sync)
                    && sync.BroadcastPortalEvent((byte)code, ArenaOrigin))
                    return;
            }

            // 호스트 캐릭터를 못 찾으면 로컬로라도 처리한다
        }
#endif
        OnPortalEvent((byte)code, ArenaOrigin);
    }

    #endregion

    #region 모든 피어: 이벤트 반영

    /// <summary>호스트가 보낸 진행 이벤트를 이 피어에 반영한다. (RPC 또는 싱글에서 호출)</summary>
    public static void OnPortalEvent(byte code, Vector3 origin)
    {
        if (Instance == null) return;
        Instance.ApplyPortalEvent((PortalEvent)code, origin);
    }

    private void ApplyPortalEvent(PortalEvent code, Vector3 origin)
    {
        switch (code)
        {
            case PortalEvent.Countdown:
                State = GateState.Entering;
                Toast.Show($"월문이 입장을 시작합니다! {countdownSeconds:0}초 뒤 문 앞({gatherRadius:0}m 안)에 있는 사람이 함께 들어갑니다.",
                    4f, ToastColor.Yellow, ToastPosition.TopCenter);
                break;

            case PortalEvent.Begin:
                State = GateState.InProgress;
                lastAttemptDay = WorldClock.Instance != null ? WorldClock.Instance.CurrentDay : lastAttemptDay;
                EnsureArena(origin);
                ApplyVisual(false);
                break;

            case PortalEvent.Ending:
                State = GateState.Cleared;
                EndingScreen.Show();
                break;

            case PortalEvent.Failed:
                State = GateState.Closed;
                Toast.Show("월문 도전에 실패했습니다. 다음 보름에 다시 열립니다.", 5f, ToastColor.Yellow, ToastPosition.TopCenter);
                break;

            case PortalEvent.Cancelled:
                State = GateState.Open;
                Toast.Show("입장이 취소되었습니다. 문 앞에 아무도 없습니다.", 3f, ToastColor.Yellow, ToastPosition.TopCenter);
                break;

            case PortalEvent.ArenaClosed:
                DestroyArena();
                break;
        }
    }

    private void EnsureArena() => EnsureArena(ArenaOrigin);

    private void EnsureArena(Vector3 origin)
    {
        if (arena != null || arenaPrefab == null) return;

        GameObject go = Instantiate(arenaPrefab, origin, Quaternion.identity);
        arena = go.GetComponent<BossArena>();
    }

    private void DestroyArena()
    {
        if (arena == null) return;

        Destroy(arena.gameObject);
        arena = null;
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.7f, 0.4f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, gatherRadius);
    }
}
