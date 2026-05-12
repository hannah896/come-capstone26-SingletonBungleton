using UnityEngine;
using System.Collections.Generic;

public class GatherableObject : MonoBehaviour
{
    [System.Serializable]
    public struct DropEntry
    {
        public ItemDataSO itemData; //드랍할아이템 연결
        [Range(0f, 1f)]
        public float dropChance;  //드랍확률
        public int minAmount;
        public int maxAmount;
    }

    [Header("채집 설정")]
    public List<DropEntry> dropTable;
    public int maxHits = 3; //몇번 쳐야되는지
    public SurvivalToolType requiredTool = SurvivalToolType.None; //None이면 맨손

    [Header("드랍 프리팹 (fallback)")]
    [Tooltip("ItemDataSO.prefab이 없는 아이템의 fallback용")]
    public GameObject fallbackDropPrefab;

    private int _hits = 0;
    private ResourceNode _resourceNode;

    private void Awake()
    {
        _resourceNode = GetComponent<ResourceNode>();
    }

    public bool OnHit(SurvivalToolType usedTool)  //PlayerToolUsage에서 호출
    {
        if (requiredTool != SurvivalToolType.None && usedTool != requiredTool)
        {
            Debug.Log($"[채집] {requiredTool} 도구가 필요합니다.");
            return false;
        }
        _hits++;
        Debug.Log($"[채집] {gameObject.name} {_hits}/{maxHits}");
        if (_hits >= maxHits)
        {
            Gather();
            return true;
        }
        return false;
    }

    private void Gather() 
    {
        foreach (var entry in dropTable)
        {
            if (Random.value > entry.dropChance) continue;
            int amount = Random.Range(entry.minAmount, entry.maxAmount + 1);
            bool added = InventoryManager.Instance.AddItem(entry.itemData, amount);
            if (!added) SpawnDroppedItem(entry.itemData, amount);
        }
        if (_resourceNode != null)
            _resourceNode.OnDepleted();
        else
            Destroy(gameObject);
    }

    private void SpawnDroppedItem(ItemDataSO itemData, int amount)
    {
        // 아이템별 고유 프리팹 우선 사용, 없으면 fallback
        GameObject prefab = (itemData.prefab != null) ? itemData.prefab : fallbackDropPrefab;
        if (prefab == null)
        {
            Debug.LogWarning($"[채집] {itemData.itemName}: 드롭 프리팹이 없습니다. ItemDataSO.prefab을 연결해주세요.");
            return;
        }

        Vector3 pos = transform.position + Random.insideUnitSphere * 0.5f;
        pos.y = transform.position.y + 0.2f;
        var go = Instantiate(prefab, pos, Quaternion.identity);

        var dropped = go.GetComponent<DroppedItem>();
        if (dropped == null) dropped = go.AddComponent<DroppedItem>();
        dropped.Setup(new ItemInstance(itemData, amount));
    }

    public void ResetHits()
    {
        _hits = 0;
    }
}
