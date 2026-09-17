#if PHOTON_FUSION
using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;

/// <summary>
/// 동물 복제 디렉터. 세션에 단 하나만 존재하며 호스트가 스폰한다.
/// NetworkMonsterDirector와 같은 방식으로, 동물 프리팹에는 NetworkObject를 붙이지 않고
/// 이 디렉터가 살아있는 동물 전체의 상태(위치/회전/애니)를 고정 슬롯 배열로 복제한다.
/// - 호스트: AI(Idle/배회/넉백/도망)를 구동하고(Animal.IsSimulatedPeer == true), 매 틱 결과를 배열에 기록
/// - 클라: AI를 돌리지 않고, 배열을 읽어 로컬 동물을 생성/갱신/제거만
/// </summary>
public class NetworkAnimalDirector : NetworkBehaviour
{
    /// <summary>동시에 살아있을 수 있는 동물 최대 수. 배열 크기라 런타임에 못 바꾼다.</summary>
    public const int Capacity = 64;

    [Networked, Capacity(Capacity)]
    private NetworkArray<AnimalNetState> Slots { get; }

    /// <summary>현재 세션의 디렉터. 스포너/데미지 보고가 참조한다.</summary>
    public static NetworkAnimalDirector Instance { get; private set; }

    // 슬롯 번호 → 로컬 동물 인스턴스
    private readonly Animal[] _animals = new Animal[Capacity];

    // 클라 전용: 슬롯이 현재 어떤 Id를 재현 중인지
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

        if (!HasStateAuthority)
        {
            for (int i = 0; i < Capacity; i++)
                DespawnLocal(i);
        }
    }

    #endregion

    #region 호스트: 등록 / 해제 / 기록

    /// <summary>호스트가 스폰(또는 씬 배치)한 동물을 복제 대상으로 등록하고 슬롯을 부여한다. 실패 시 -1.</summary>
    public int RegisterAnimal(Animal animal, byte catalogId)
    {
        if (!HasStateAuthority || animal == null) return -1;
        if (animal.NetSlot >= 0) return animal.NetSlot;

        for (int i = 0; i < Capacity; i++)
        {
            if (_animals[i] != null) continue;

            _animals[i] = animal;
            animal.NetSlot = i;

            Slots.Set(i, new AnimalNetState
            {
                Id = _nextId++,
                CatalogId = catalogId,
                AnimId = (byte)animal.CurrentAnimId,
                Position = animal.transform.position,
                Yaw = animal.transform.eulerAngles.y,
            });

            if (_nextId == 0) _nextId = 1;
            return i;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning($"[AnimalDirector] 슬롯이 가득 찼습니다(Capacity={Capacity}). 등록을 건너뜁니다.");
#endif
        return -1;
    }

    /// <summary>호스트가 동물을 복제 대상에서 제거한다.</summary>
    public void UnregisterAnimal(Animal animal)
    {
        if (!HasStateAuthority || animal == null) return;

        int slot = animal.NetSlot;
        if (slot < 0 || slot >= Capacity) return;
        if (_animals[slot] != animal) return;

        _animals[slot] = null;
        animal.NetSlot = -1;
        Slots.Set(slot, default);
    }

    /// <summary>슬롯의 동물을 Id로 찾는다(데미지 보고 처리용). 호스트 전용.</summary>
    public Animal FindById(ushort id)
    {
        if (id == 0) return null;

        for (int i = 0; i < Capacity; i++)
        {
            if (_animals[i] != null && Slots[i].Id == id)
                return _animals[i];
        }

        return null;
    }

    /// <summary>슬롯의 현재 Id. 클라가 데미지를 보고할 때 동물을 지목하는 값이다.</summary>
    public ushort GetNetId(Animal animal)
    {
        if (animal == null) return 0;

        int slot = animal.NetSlot;
        if (slot < 0 || slot >= Capacity) return 0;
        return Slots[slot].Id;
    }

    // 호스트: 살아있는 동물의 최신 상태를 배열에 기록한다.
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        for (int i = 0; i < Capacity; i++)
        {
            Animal animal = _animals[i];
            if (ReferenceEquals(animal, null)) continue; // 빈 슬롯

            // 풀로 반환됐거나(사망) 파괴됐거나(씬 배치 동물 사망), 틱 사이에 풀에서 재사용돼 슬롯이 초기화된 경우 비운다.
            if (animal == null || !animal.isActiveAndEnabled || animal.NetSlot != i)
            {
                if (animal != null && animal.NetSlot == i) animal.NetSlot = -1;
                _animals[i] = null;
                Slots.Set(i, default);
                continue;
            }

            AnimalNetState state = Slots[i];
            state.AnimId = (byte)animal.CurrentAnimId;
            state.Position = animal.transform.position;
            state.Yaw = animal.transform.eulerAngles.y;
            Slots.Set(i, state);
        }
    }

    #endregion

    #region 클라: 재현

    public override void Render()
    {
        if (HasStateAuthority) return;

        for (int i = 0; i < Capacity; i++)
        {
            AnimalNetState state = Slots[i];

            if (state.Id == 0)
            {
                DespawnLocal(i);
                continue;
            }

            if (_shownIds[i] != state.Id)
            {
                DespawnLocal(i);
                _shownIds[i] = state.Id;
                SpawnLocalAsync(i, state).Forget();
                continue;
            }

            Animal animal = _animals[i];
            if (animal == null) continue; // 스폰 대기 중

            animal.ApplyNetState(state.Position, state.Yaw, (AnimalAnimId)state.AnimId);
        }
    }

    // 클라: 복제 정보로 로컬 동물을 풀에서 꺼낸다(네트워크 스폰이 아니다).
    private async UniTaskVoid SpawnLocalAsync(int slot, AnimalNetState state)
    {
        AnimalCatalog.Entry entry = AnimalCatalog.Instance.GetEntry(state.CatalogId);
        if (entry == null || string.IsNullOrEmpty(entry.addressableKey))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[AnimalDirector] CatalogId {state.CatalogId}에 해당하는 동물이 카탈로그에 없습니다.");
#endif
            return;
        }

        Animal animal = await Animal.SpawnAsync(entry.addressableKey, entry.statData, state.Position);
        if (animal == null) return;

        if (_shownIds[slot] != state.Id)
        {
            Extensions.Despawn(animal.gameObject);
            return;
        }

        animal.NetSlot = slot;
        _animals[slot] = animal;
        animal.ApplyNetState(state.Position, state.Yaw, (AnimalAnimId)state.AnimId);
    }

    private void DespawnLocal(int slot)
    {
        _shownIds[slot] = 0;

        Animal animal = _animals[slot];
        _animals[slot] = null;
        if (animal == null) return;

        animal.NetSlot = -1;
        Extensions.Despawn(animal.gameObject);
    }

    #endregion

    #region 데미지 보고 (클라 → 호스트)

    /// <summary>
    /// 동물을 때렸을 때 호스트에 보고한다. 호스트는 직접 적용한다.
    /// attackerPosition은 넉백/도망 방향 계산에 쓰인다.
    /// </summary>
    public void ReportDamage(Animal animal, int amount, Vector3 attackerPosition, string toolId, ActionType actionType)
    {
        ushort id = GetNetId(animal);
        if (id == 0) return;

        if (HasStateAuthority)
            ApplyDamageOnHost(id, amount, attackerPosition, toolId, actionType);
        else
            Rpc_ReportDamage(id, amount, attackerPosition, toolId ?? string.Empty, (byte)actionType);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_ReportDamage(ushort id, int amount, Vector3 attackerPosition, string toolId, byte actionType, RpcInfo info = default)
    {
        ApplyDamageOnHost(id, amount, attackerPosition, toolId, (ActionType)actionType);
    }

    private void ApplyDamageOnHost(ushort id, int amount, Vector3 attackerPosition, string toolId, ActionType actionType)
    {
        Animal animal = FindById(id);
        if (animal == null) return;

        // Instigator 없이 Point에 공격자 위치를 담는다 (Animal.ApplyDamage가 넉백 방향으로 사용)
        var ctx = new DamageContext(
            instigator: null,
            point: attackerPosition,
            amount: amount,
            toolId: toolId,
            actionType: actionType);

        if (animal.CanDamage(ctx))
            animal.ApplyDamage(ctx);
    }

    #endregion
}
#endif
