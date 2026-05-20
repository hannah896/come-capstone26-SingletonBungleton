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
        return !_isDestroyed && context.Amount > 0 && IsToolAllowed(context.ToolId);
    }

    public void ApplyDamage(DamageContext context)
    {
        if (!CanDamage(context)) return;

        _currentHealth = Mathf.Max(0, _currentHealth - context.Amount);
        OnDamaged(context);

        if (_currentHealth <= 0)
        {
            HandleDestroyed();
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
            HandleDestroyed();
        }
    }
    // 도구 허용 여부를 판단하는 메서드. CanDamage 메서드에서 사용됨
    // 노드 데이터에 허용된 도구 ID 목록이 있는 경우, 해당 목록에 도구 ID가 포함되어 있는지 확인
    protected virtual bool IsToolAllowed(string toolId)
    {
        if (_resourceNodeData.AllowedToolIds == null || _resourceNodeData.AllowedToolIds.Length == 0) return true;
        if (string.IsNullOrEmpty(toolId)) return false;

        for (int i = 0; i < _resourceNodeData.AllowedToolIds.Length; i++)
        {
            if (_resourceNodeData.AllowedToolIds[i] == toolId) return true;
        }

        return false;
    }

    protected virtual void OnPlacementInitialized(DisposeData placement, ChunkData chunk) { }
    protected virtual void OnInteracted(InteractionContext context) { }
    protected virtual void OnDamaged(DamageContext context) { }
    protected virtual void OnGathered(GatherContext context) { }
    protected virtual void OnDestroyed() 
    {
        _deathPosition = transform.position;
        Debug.Log($"{ResourceNodeData.Name} 파괴됨! 위치: {_deathPosition}");
    }

    private void HandleDestroyed()
    {
        if (_isDestroyed) return;

        _isDestroyed = true;

        if (_placement != null && _chunk != null)
        {
            // 파괴된 시간을 기록
            float currentTime = WorldClock.Instance.TotalInGameSeconds;
            float targetRespawnTime = currentTime + _resourceNodeData.RespawnTime;
            _chunk.MarkObjectDestroyed(_placement.instanceId, targetRespawnTime);
        }

        OnDestroyed();

        if (_despawnOnDestroyed)
        {
            Extensions.Despawn(gameObject);
        }
    }
}
