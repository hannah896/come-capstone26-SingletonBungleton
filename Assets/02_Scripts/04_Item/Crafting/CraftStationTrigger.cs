using UnityEngine;

/// <summary>
/// 작업대 오브젝트에 붙이는 컴포넌트.
/// 플레이어가 Collider 범위 안으로 들어오면 CraftingManager에 스테이션을 등록하고,
/// 나가면 해제한다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CraftStationTrigger : MonoBehaviour
{
    [SerializeField] private CraftStation stationType = CraftStation.Workbench;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        CraftingManager.Instance?.SetNearbyStation(stationType);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        // 같은 타입의 다른 스테이션이 있을 수 있으므로 None으로만 초기화
        if (CraftingManager.Instance?.GetNearbyStation() == stationType)
            CraftingManager.Instance.SetNearbyStation(CraftStation.None);
    }
}
