using UnityEngine;

/// 플레이어가 도구로 채집 오브젝트 타격
/// 플레이어 오브젝트에 붙임

public class PlayerToolUsage : MonoBehaviour
{
    [Header("설정")]
    [SerializeField] private float hitRange = 2.5f;
    [SerializeField] private LayerMask gatherableLayer;

    // InventoryManager.equippedHand 머지 후 연결
    private Item_SurvivalTool _equippedTool;

    public void SetEquippedTool(Item_SurvivalTool tool)
    {
        _equippedTool = tool;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            TryHit();
    }

    private void TryHit()
    {
        if (_equippedTool != null)
        {
            if (_equippedTool.CurrentDurability <= 0)
            {
                Debug.Log("[도구] 내구도 없어 사용 불가!");
                return;
            }
            _equippedTool.UseDurability(1);
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, hitRange, gatherableLayer))
        {
            if (hit.collider.TryGetComponent<GatherableObject>(out var gatherable))
            {
                SurvivalToolType toolType = _equippedTool != null
                    ? _equippedTool.survivalToolType
                    : SurvivalToolType.None;

                gatherable.OnHit(toolType);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        if (Camera.main != null)
            Gizmos.DrawRay(Camera.main.transform.position,
                Camera.main.transform.forward * hitRange);
    }
}