using System.Linq;
using UnityEngine;

/// <summary>
/// 배치되는 자원 오브젝트 노드의 기본이 되는 추상 클래스.
/// </summary>
public abstract class ResourceNode : MonoBehaviour, IInteractable, IDamageable, IGatherable, IDisposeInitializable
{
    [SerializeField] private ResourceNodeData _resourceNodeData;
    protected ResourceNodeData ResourceNodeData => _resourceNodeData;

    /// <summary>이 노드의 자원 타입 (맨손 채집 허용 여부 판정 등에 사용).</summary>
    public ResourceNodeType NodeType => _resourceNodeData != null ? _resourceNodeData.ResourceNodeType : ResourceNodeType.None;

    /// <summary>도구 없이 G키(줍기)로 바로 채집 가능한 노드인지 (풀 등).</summary>
    public bool IsHandPickable => _resourceNodeData != null && _resourceNodeData.HandPickable && !_isDestroyed;

    /// <summary>G키 줍기로 즉시 채집한다. Drops를 (설정에 따라) 인벤토리로 보내고 노드를 소진시킨다.</summary>
    public void HandPick(DamageContext context)
    {
        if (!IsHandPickable) return;

        // 멀티: 즉시 소진 = 최대 체력만큼 데미지를 보고해 호스트가 파괴를 확정하게 한다
        if (IsNetworkManaged)
        {
            RememberLocalContext(context);
            ReportNetworkDamage(MaxHealth);
            return;
        }

        HandleDestroyed(context);
    }

    [SerializeField] private bool _despawnOnDestroyed = true;

    private int _currentHealth;
    private int _remainingGather;
    private bool _isDestroyed;
    private DisposeData _placement;
    private ChunkData _chunk;

    // 멀티: 이 피어가 마지막으로 가한 타격 (호스트가 "이 피어가 부쉈다"고 확정하면 드롭 지급에 사용)
    private DamageContext _lastLocalContext;
    private bool _hasLocalContext;

    private int MaxHealth => Mathf.Max(1, _resourceNodeData.MaxHealth);

    // 월드에 배치된 노드이고 세션 중이면 체력/파괴를 호스트가 관리한다
    private bool IsNetworkManaged => WorldResourceSync.IsNetworked && _placement != null && _placement.instanceId != 0;

    private Vector3 _deathPosition;
    protected Vector3 DeathPosition => _deathPosition;

    protected int CurrentHealth => _currentHealth;
    protected bool IsDestroyed => _isDestroyed;

    protected virtual void Awake()
    {
        ResetState();
    }

    // 체력/채집 횟수/파괴 여부를 초기값으로
    private void ResetState()
    {
        _currentHealth = Mathf.Max(1, _resourceNodeData.MaxHealth);
        _remainingGather = Mathf.Max(0, _resourceNodeData.GatherAmount);
        _isDestroyed = false;
        _hasLocalContext = false;
        _lastLocalContext = default;
    }

    private void RememberLocalContext(DamageContext context)
    {
        _lastLocalContext = context;
        _hasLocalContext = true;
    }

    // 호스트에 데미지 보고 (재생성 시간은 이 노드 데이터 기준)
    private void ReportNetworkDamage(int amount)
    {
        WorldResourceSync.ReportDamage(_placement.instanceId, amount, MaxHealth, _resourceNodeData.RespawnTime);
    }

    // 배치 초기화 메서드. DisposeData와 ChunkData를 받아 초기화 작업을 수행
    public void InitializeDispose(DisposeData placement, ChunkData chunk)
    {
        // 풀에서 재사용된 노드는 이전 수명의 "파괴됨" 상태를 들고 있으므로 스폰마다 초기화한다.
        // (Awake는 최초 1회만 불려, 재생성된 자원이 채집 불가 상태로 나오던 원인)
        ResetState();

        _placement = placement;
        _chunk = chunk;
        OnPlacementInitialized(placement, chunk);
    }

    // 상호작용 가능 여부를 판단하는 메서드. 기본적으로 파괴되지 않은 상태에서만 상호작용 가능
    public bool CanInteract(InteractionContext context)
    {
        return !_isDestroyed;
    }
    /// <summary>
    /// 상호작용을 처리하는 메서드. CanInteract에서 허용된 경우에만 OnInteracted를 호출.
    /// OnInteracted는 실제 상호작용 로직을 구현하는 메서드로, 자식 클래스에서 오버라이드하여 구체적인 행동을 정의.
    /// </summary>
    /// <param name="context"></param>
    public void Interact(InteractionContext context)
    {
        if (!CanInteract(context)) return;
        OnInteracted(context);
    }

    public bool CanDamage(DamageContext context)
    {
        if (_isDestroyed || context.Amount <= 0) return false;

        // 필요 도구 체크
        HarvestToolType required = _resourceNodeData.RequiredTool;
        if (required != HarvestToolType.None && !ToolMatches(context.ActionType, required))
        {
            Debug.Log($"[{_resourceNodeData.Name}] 필요 도구: {required} / 현재: {context.ActionType}");
            return false;
        }

        // 도구의 채집 가능 노드 타입 체크
        if (context.HarvestableNodeTypes != null
            && context.HarvestableNodeTypes.Count > 0
            && !context.HarvestableNodeTypes.Contains(_resourceNodeData.ResourceNodeType))
        {
            Debug.Log($"[{_resourceNodeData.Name}] 이 도구로 채집할 수 없는 노드입니다. (노드: {_resourceNodeData.ResourceNodeType})");
            return false;
        }

        return true;
    }

    private static bool ToolMatches(ActionType action, HarvestToolType required) => required switch
    {
        // 도끼/곡괭이가 필요한 노드(나무·돌)는 맨손(Hand)으로도 느리게 채집 가능
        HarvestToolType.Axe        => action == ActionType.Chop || action == ActionType.Hand,
        HarvestToolType.Pickaxe    => action == ActionType.Mine || action == ActionType.Hand,
        HarvestToolType.Shovel     => action == ActionType.Dig,
        HarvestToolType.Hammer     => action == ActionType.Build,
        HarvestToolType.FishingRod => action == ActionType.Pick,
        HarvestToolType.AnyTool    => action != ActionType.None,
        _                          => true,
    };

    public void ApplyDamage(DamageContext context)
    {
        if (!CanDamage(context)) return;

        // 멀티: 체력은 호스트가 모든 플레이어의 타격을 합산해 관리한다.
        // 여기서는 표시용 예상 체력만 계산하고, 파괴는 호스트 확정(ApplyNetworkDestroyed)을 기다린다.
        if (IsNetworkManaged)
        {
            RememberLocalContext(context);
            int predictedDamage = WorldResourceSync.GetSharedDamage(_placement.instanceId) + context.Amount;
            _currentHealth = Mathf.Max(0, MaxHealth - predictedDamage);
            OnDamaged(context);
            ReportNetworkDamage(context.Amount);
            return;
        }

        _currentHealth = Mathf.Max(0, _currentHealth - context.Amount);
        OnDamaged(context);

        if (_currentHealth <= 0)
        {
            HandleDestroyed(context);
        }
    }

    public bool CanGather(GatherContext context)
    {
        return !_isDestroyed && _remainingGather > 0;
    }

    public void Gather(GatherContext context)
    {
        if (!CanGather(context)) return;

        _remainingGather--;
        OnGathered(context);

        if (_remainingGather <= 0)
        {
            if (IsNetworkManaged)
            {
                ReportNetworkDamage(MaxHealth); // 소진 확정은 호스트에 맡긴다
                return;
            }

            HandleDestroyed(default);
        }
    }

    protected virtual void OnPlacementInitialized(DisposeData placement, ChunkData chunk) { }
    protected virtual void OnInteracted(InteractionContext context) { }
    protected virtual void OnDamaged(DamageContext context) { }
    protected virtual void OnGathered(GatherContext context) { }

    /// <summary>노드가 파괴될 때 호출된다. context는 도구 공격으로 파괴된 경우에만 채워지고,
    /// Gather로 소진된 경우엔 기본값(default)이 전달된다.</summary>
    protected virtual void OnDestroyed(DamageContext context)
    {
        _deathPosition = transform.position;
    }

    /// <summary>
    /// 호스트가 파괴를 확정했을 때 호출된다. (WorldResourceSync 전용 — 청크 파괴 표시는 호출 측에서 이미 했다)
    /// destroyedByLocal: 이 피어의 플레이어가 마지막 타격을 넣었으면 드롭까지 처리하고,
    /// 아니면 드롭 없이 파괴 연출과 제거만 한다 (드롭은 부순 한 명만 받는다).
    /// </summary>
    public void ApplyNetworkDestroyed(bool destroyedByLocal, GameObject localPlayer)
    {
        if (_isDestroyed) return;

        _isDestroyed = true;

        if (destroyedByLocal)
        {
            DamageContext context = _hasLocalContext
                ? _lastLocalContext
                : new DamageContext(localPlayer, transform.position, 0, "Network");
            OnDestroyed(context);
        }
        else
        {
            _deathPosition = transform.position;
            OnRemoteDestroyed();
        }

        DespawnSelf();
    }

    /// <summary>원격 파괴 시 연출. 드롭은 부순 플레이어만 받으므로 여기서는 생성하지 않는다.</summary>
    protected virtual void OnRemoteDestroyed() { }

    private void HandleDestroyed(DamageContext context)
    {
        if (_isDestroyed) return;

        _isDestroyed = true;

        if (_placement != null && _chunk != null)
        {
            float currentTime = WorldClock.Instance != null ? WorldClock.Instance.TotalInGameSeconds : 0f;
            float targetRespawnTime = currentTime + _resourceNodeData.RespawnTime;
            _chunk.MarkObjectDestroyed(_placement.instanceId, targetRespawnTime);
        }

        OnDestroyed(context);
        DespawnSelf();
    }

    // 풀 반납 (스포너 관리 목록에서 먼저 빼서 청크 언로드 때 중복 반납되지 않게)
    private void DespawnSelf()
    {
        if (!_despawnOnDestroyed) return;

        if (_placement != null)
            WorldResourceSync.DetachFromSpawner(_chunk, _placement.instanceId);

        Extensions.Despawn(gameObject);
    }
}
