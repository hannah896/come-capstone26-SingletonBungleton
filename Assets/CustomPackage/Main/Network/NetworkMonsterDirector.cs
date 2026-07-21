#if PHOTON_FUSION
using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;

/// <summary>
/// 몬스터 복제 디렉터. 세션에 단 하나만 존재하며 호스트가 스폰한다.
///
/// 몬스터 프리팹에는 NetworkObject를 붙이지 않는다. 대신 이 디렉터가 살아있는 몬스터 전체의
/// 상태(위치/회전/애니)를 고정 슬롯 배열로 복제하고, 클라이언트는 그걸 읽어 로컬 몬스터를 재현한다.
/// - 호스트: AI를 정상 구동하고(Monster.IsSimulatedPeer == true), 매 틱 결과를 배열에 기록
/// - 클라: AI를 돌리지 않고(Monster.OnGameUpdate가 차단), 배열을 읽어 생성/갱신/제거만
///
/// 몬스터 자체는 Main.Pool로 스폰되므로 기존 풀링/스탯 주입 설계를 그대로 쓴다.
/// </summary>
public class NetworkMonsterDirector : NetworkBehaviour
{
    /// <summary>동시에 살아있을 수 있는 몬스터 최대 수. 배열 크기라 런타임에 못 바꾼다.</summary>
    public const int Capacity = 128;

    // 호스트가 쓰고 모든 피어가 읽는다. Fusion이 변경된 슬롯만 델타로 보내므로
    // 빈 슬롯은 대역폭을 먹지 않는다.
    [Networked, Capacity(Capacity)]
    private NetworkArray<MonsterNetState> Slots { get; }

    /// <summary>현재 세션의 디렉터. 스포너/데미지 보고가 참조한다.</summary>
    public static NetworkMonsterDirector Instance { get; private set; }

    // 슬롯 번호 → 로컬 몬스터 인스턴스 (호스트/클라 공용으로 이 피어가 들고 있는 실제 오브젝트)
    private readonly Monster[] _monsters = new Monster[Capacity];

    // 클라 전용: 슬롯이 현재 어떤 Id를 재현 중인지 (Id가 바뀌면 다른 몬스터로 교체해야 한다)
    private readonly ushort[] _shownIds = new ushort[Capacity];

    // 호스트 전용: 다음에 발급할 Id (0은 빈 슬롯 표시라 1부터 시작)
    private ushort _nextId = 1;

    #region Lifecycle

    public override void Spawned()
    {
        Instance = this;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this) Instance = null;

        // 클라가 만들어둔 로컬 몬스터를 정리한다(호스트 몬스터는 각자 사망 흐름으로 사라진다).
        if (!HasStateAuthority)
        {
            for (int i = 0; i < Capacity; i++)
                DespawnLocal(i);
        }
    }

    #endregion

    #region 호스트: 등록 / 해제 / 기록

    /// <summary>
    /// 호스트가 스폰한 몬스터를 복제 대상으로 등록하고 슬롯을 부여한다. 실패 시 -1.
    /// </summary>
    public int RegisterMonster(Monster monster, byte catalogId)
    {
        if (!HasStateAuthority || monster == null) return -1;

        for (int i = 0; i < Capacity; i++)
        {
            if (_monsters[i] != null) continue;

            _monsters[i] = monster;
            monster.NetSlot = i;

            Slots.Set(i, new MonsterNetState
            {
                Id = _nextId++,
                CatalogId = catalogId,
                AnimId = (byte)monster.CurrentAnimId,
                Position = monster.transform.position,
                Yaw = monster.transform.eulerAngles.y,
            });

            if (_nextId == 0) _nextId = 1; // ushort 한 바퀴 방지(0은 빈 슬롯 예약값)
            return i;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning($"[MonsterDirector] 슬롯이 가득 찼습니다(Capacity={Capacity}). 스폰을 건너뜁니다.");
#endif
        return -1;
    }

    /// <summary>호스트가 몬스터를 복제 대상에서 제거한다. 클라는 이 슬롯이 비는 걸 보고 로컬 몬스터를 지운다.</summary>
    public void UnregisterMonster(Monster monster)
    {
        if (!HasStateAuthority || monster == null) return;

        int slot = monster.NetSlot;
        if (slot < 0 || slot >= Capacity) return;
        if (_monsters[slot] != monster) return;

        _monsters[slot] = null;
        monster.NetSlot = -1;
        Slots.Set(slot, default); // Id = 0 → 빈 슬롯
    }

    /// <summary>슬롯의 몬스터를 Id로 찾는다(데미지 보고 처리용). 호스트 전용.</summary>
    public Monster FindById(ushort id)
    {
        if (id == 0) return null;

        for (int i = 0; i < Capacity; i++)
        {
            if (_monsters[i] != null && Slots[i].Id == id)
                return _monsters[i];
        }

        return null;
    }

    /// <summary>슬롯의 현재 Id. 클라가 데미지를 보고할 때 몬스터를 지목하는 값이다.</summary>
    public ushort GetNetId(Monster monster)
    {
        if (monster == null) return 0;

        int slot = monster.NetSlot;
        if (slot < 0 || slot >= Capacity) return 0;
        return Slots[slot].Id;
    }

    // 호스트: 살아있는 몬스터의 최신 상태를 배열에 기록한다.
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        for (int i = 0; i < Capacity; i++)
        {
            Monster monster = _monsters[i];
            if (monster == null) continue;

            // 몬스터가 사망 흐름으로 풀에 반환됐다면 슬롯을 비운다.
            if (!monster.isActiveAndEnabled)
            {
                _monsters[i] = null;
                monster.NetSlot = -1;
                Slots.Set(i, default);
                continue;
            }

            MonsterNetState state = Slots[i];
            state.AnimId = (byte)monster.CurrentAnimId;
            state.Position = monster.transform.position;
            state.Yaw = monster.transform.eulerAngles.y;
            Slots.Set(i, state);
        }
    }

    #endregion

    #region 클라: 재현

    // 클라: 복제된 배열을 읽어 로컬 몬스터를 생성/갱신/제거한다.
    public override void Render()
    {
        if (HasStateAuthority) return; // 호스트는 자기 AI가 이미 움직였다

        for (int i = 0; i < Capacity; i++)
        {
            MonsterNetState state = Slots[i];

            // 빈 슬롯 → 재현 중이던 몬스터가 있으면 제거
            if (state.Id == 0)
            {
                DespawnLocal(i);
                continue;
            }

            // 다른 Id로 바뀌었으면(슬롯 재사용) 기존 것을 버리고 새로 만든다
            if (_shownIds[i] != state.Id)
            {
                DespawnLocal(i);
                _shownIds[i] = state.Id;
                SpawnLocalAsync(i, state).Forget();
                continue;
            }

            Monster monster = _monsters[i];
            if (monster == null) continue; // 스폰 대기 중

            monster.ApplyNetState(state.Position, state.Yaw, (MonsterAnimId)state.AnimId);
        }
    }

    // 클라: 복제 정보로 로컬 몬스터를 풀에서 꺼낸다(네트워크 스폰이 아니다).
    private async UniTaskVoid SpawnLocalAsync(int slot, MonsterNetState state)
    {
        MonsterCatalog.Entry entry = MonsterCatalog.Instance.GetEntry(state.CatalogId);
        if (entry == null || string.IsNullOrEmpty(entry.addressableKey))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[MonsterDirector] CatalogId {state.CatalogId}에 해당하는 몬스터가 카탈로그에 없습니다.");
#endif
            return;
        }

        Monster monster = await Monster.SpawnAsync(entry.addressableKey, entry.statData, state.Position);
        if (monster == null) return;

        // 로드 대기 중에 슬롯이 비었거나 다른 몬스터로 바뀌었으면 취소
        if (_shownIds[slot] != state.Id)
        {
            Extensions.Despawn(monster.gameObject);
            return;
        }

        monster.NetSlot = slot;
        _monsters[slot] = monster;
        monster.ApplyNetState(state.Position, state.Yaw, (MonsterAnimId)state.AnimId);
    }

    private void DespawnLocal(int slot)
    {
        _shownIds[slot] = 0;

        Monster monster = _monsters[slot];
        if (monster == null) return;

        _monsters[slot] = null;
        monster.NetSlot = -1;
        Extensions.Despawn(monster.gameObject);
    }

    #endregion

    #region 데미지 보고 (클라 → 호스트)

    /// <summary>
    /// 클라이언트가 몬스터를 때렸을 때 호스트에 보고한다. 호스트는 직접 적용한다.
    /// 데미지 확정은 호스트 전담이라 클라는 절대 로컬로 HP를 깎지 않는다(치트/불일치 방지).
    /// </summary>
    public void ReportDamage(Monster monster, int amount, string toolId, ActionType actionType)
    {
        ushort id = GetNetId(monster);
        if (id == 0) return;

        if (HasStateAuthority)
            ApplyDamageOnHost(id, amount, toolId, actionType);
        else
            Rpc_ReportDamage(id, amount, toolId ?? string.Empty, (byte)actionType);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_ReportDamage(ushort id, int amount, string toolId, byte actionType, RpcInfo info = default)
    {
        ApplyDamageOnHost(id, amount, toolId, (ActionType)actionType);
    }

    private void ApplyDamageOnHost(ushort id, int amount, string toolId, ActionType actionType)
    {
        Monster monster = FindById(id);
        if (monster == null) return;

        var ctx = new DamageContext(
            instigator: gameObject,
            point: monster.transform.position,
            amount: amount,
            toolId: toolId,
            actionType: actionType);

        if (monster.CanDamage(ctx))
            monster.ApplyDamage(ctx);
    }

    #endregion
}
#endif
