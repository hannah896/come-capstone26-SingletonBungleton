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
        if (!IsLocalPlayer(other)) return;
        CraftingManager.Instance?.EnterStation(stationType);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsLocalPlayer(other)) return;
        CraftingManager.Instance?.ExitStation(stationType);
    }

    // 이 피어가 조작하는 플레이어만 센다 (멀티에서 원격 플레이어가 다가와도 내 제작대 판정이 바뀌면 안 된다)
    private static bool IsLocalPlayer(Collider other)
    {
        if (!other.CompareTag("Player")) return false;

        Player player = other.GetComponentInParent<Player>();
        return player == null || player.IsLocalPlayer;
    }
}
