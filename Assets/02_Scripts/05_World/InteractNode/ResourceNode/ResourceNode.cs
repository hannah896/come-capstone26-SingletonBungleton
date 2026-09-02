using System.Linq;
using UnityEngine;

/// <summary>
/// 배치되는 자원 오브젝트 노드의 기본이 되는 추상 클래스.
/// </summary>
public abstract class ResourceNode : MonoBehaviour, IInteractable, IDamageable, IGatherable, IDisposeInitializable
{
    [SerializeField] private ResourceNodeData _resourceNodeData;
    protected ResourceNodeData ResourceNodeData => _resourceNodeData;

    [SerializeField] private bool _despawnOnDestroyed = true;

    private int _currentHealth;
    private int _remainingGather;
    private bool _isDestroyed;
    private DisposeData _placement;
    private ChunkData _chunk;

    private Vector3 _deathPosition;
    protected Vector3 DeathPosition => _deathPosition;

    protected int CurrentHealth => _currentHealth;
    protected bool IsDestroyed => _isDestroyed;

    protected virtual void Awake()
    {
        _currentHealth = Mathf.Max(1, _resourceNodeData.MaxHealth);
        _remainingGather = Mathf.Max(0, _resourceNodeData.GatherAmount);
    }

    // 배치 초기화 메서드. DisposeData와 ChunkData를 받아 초기화 작업을 수행
    public void InitializeDispose(DisposeData placement, ChunkData chunk)
    {
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
        HarvestToolType.Axe        => action == ActionType.Chop,
        HarvestToolType.Pickaxe    => action == ActionType.Mine,
        HarvestToolType.Shovel     => action == ActionType.Dig,
        HarvestToolType.Hammer     => action == ActionType.Build,
        HarvestToolType.FishingRod => action == ActionType.Pick,
        HarvestToolType.AnyTool    => action != ActionType.None,
        _                          => true,
    };

    public void ApplyDamage(DamageContext context)
    {
        if (!CanDamage(context)) return;

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

        if (_despawnOnDestroyed)
        {
            Extensions.Despawn(gameObject);
        }
    }
}
