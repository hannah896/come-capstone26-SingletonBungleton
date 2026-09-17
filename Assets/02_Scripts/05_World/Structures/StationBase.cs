using UnityEngine;

public abstract class StationBase : Structure
{
    public abstract StationType StationType { get; }

    [Header("근접 트리거")]
    [SerializeField] private SphereCollider proximityTrigger;
    [SerializeField] private float proximityRadius = 1.0f;

    protected virtual void Awake() => EnsureTrigger();
    protected virtual void OnValidate() => EnsureTrigger();

    //TODO: 플레이어가 여러 명일 경우, 플레이어 ID를 기반으로 관리하도록 변경 필요
    protected virtual void OnPlayerEnter(Player player)
    {
        StationManager.Instance?.AddStation(0, StationType);
        Debug.Log("Player entered station: " + StationType);
    }

    protected virtual void OnPlayerExit(Player player)
    {
        StationManager.Instance?.RemoveStation(0, StationType);
        Debug.Log("Player exited station: " + StationType);
    }

    private void EnsureTrigger()
    {
        if (proximityTrigger == null)
        {
            proximityTrigger = Extensions.GetOrAddComponent<SphereCollider>(gameObject);
        }
        proximityTrigger.radius = proximityRadius;
        proximityTrigger.isTrigger = true;
    }

    // 스테이션 근접 판정은 이 피어가 조작하는 플레이어만 센다 (원격 플레이어가 다가와도 내 제작 조건이 바뀌면 안 된다)
    private void OnTriggerEnter(Collider other)
    {
        Player player = other.GetComponentInParent<Player>();
        if (player == null || !player.IsLocalPlayer) return;
        OnPlayerEnter(player);
    }

    private void OnTriggerExit(Collider other)
    {
        Player player = other.GetComponentInParent<Player>();
        if (player == null || !player.IsLocalPlayer) return;
        OnPlayerExit(player);
    }
}
