using UnityEngine;

public abstract class ResourceNode : MonoBehaviour, IInteractable, IDamageable, IGatherable, IPlacementInitializable
{
    [SerializeField] private ResourceNodeData _nodeData;
    protected ResourceNodeData NodeData => _nodeData;

    [SerializeField] private bool _despawnOnDestroyed = true;

    private int _currentHealth;
    private int _remainingGather;
    private bool _isDestroyed;
    private PlacementData _placement;
    private ChunkData _chunk;

    private Vector3 _deathPosition;
    protected Vector3 DeathPosition => _deathPosition;

    protected int CurrentHealth => _currentHealth;
    protected bool IsDestroyed => _isDestroyed;

    protected virtual void Awake()
    {
        _currentHealth = Mathf.Max(1, _nodeData.MaxHealth);
        _remainingGather = Mathf.Max(0, _nodeData.GatherAmount);
    }

    public void InitializePlacement(PlacementData placement, ChunkData chunk)
    {
        _placement = placement;
        _chunk = chunk;
        OnPlacementInitialized(placement, chunk);
    }

    public bool CanInteract(InteractionContext context)
    {
        return !_isDestroyed;
    }

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

    protected virtual bool IsToolAllowed(string toolId)
    {
        if (_nodeData.AllowedToolIds == null || _nodeData.AllowedToolIds.Length == 0) return true;
        if (string.IsNullOrEmpty(toolId)) return false;

        for (int i = 0; i < _nodeData.AllowedToolIds.Length; i++)
        {
            if (_nodeData.AllowedToolIds[i] == toolId) return true;
        }

        return false;
    }

    protected virtual void OnPlacementInitialized(PlacementData placement, ChunkData chunk) { }
    protected virtual void OnInteracted(InteractionContext context) { }
    protected virtual void OnDamaged(DamageContext context) { }
    protected virtual void OnGathered(GatherContext context) { }
    protected virtual void OnDestroyed() 
    {
        _deathPosition = transform.position;
    }

    private void HandleDestroyed()
    {
        if (_isDestroyed) return;

        _isDestroyed = true;

        if (_placement != null && _chunk != null)
        {
            // 파괴된 시간을 기록
            float currentTime = WorldClock.Instance.TotalInGameSeconds;
            float targetRespawnTime = currentTime + NodeData.RespawnTime;
            _chunk.MarkObjectDestroyed(_placement.instanceId, targetRespawnTime);
        }

        OnDestroyed();

        if (_despawnOnDestroyed)
        {
            Extensions.Despawn(gameObject);
        }
    }
}
