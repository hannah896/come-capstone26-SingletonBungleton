using UnityEngine;
using System.Collections.Generic;

public class GatherableObject : MonoBehaviour
{
    [System.Serializable]
    public struct DropEntry
    {
        public ItemDataSO itemData;  //드랍할아이템 연결
        [Range(0f, 1f)]
        public float dropChance;  //드랍확률
        public int minAmount;
        public int maxAmount;
    }

    [Header("채집 설정")]
    public List<DropEntry> dropTable;
    public int maxHits = 3; //몇번 쳐야되는지
    public SurvivalToolType requiredTool = SurvivalToolType.None; //None이면 맨손

    [Header("드랍 프리팹")]
    public GameObject droppedItemPrefab;  //인벤 다 차면 바닥에 스폰
    private int _hits = 0;

    public bool OnHit(SurvivalToolType usedTool)   //PlayerToolUsage에서 호출
    {
        if (requiredTool != SurvivalToolType.None && usedTool != requiredTool)
        {
            Debug.Log($"[채집] {requiredTool} 도구가 필요합니다.");
            return false;
        }

        _hits++;
        Debug.Log($"[채집] {gameObject.name} {_hits}/{maxHits}");

        if (_hits >= maxHits )
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

            // 인벤토리에 추가
            bool added = InventoryManager.Instance.AddItem(entry.itemData, amount);

            // 인벤토리 가득 찼으면 바닥에 드랍
            if (!added) SpawnDroppedItem(entry.itemData, amount);
        }
        Destroy(gameObject);
    }

    private void SpawnDroppedItem(ItemDataSO itemData, int amount)
    {
        if (droppedItemPrefab == null) return;
        Vector3 pos = transform.position + Random.insideUnitSphere * 0.5f;
        pos.y = transform.position.y + 0.2f;
        var go = Instantiate(droppedItemPrefab, pos, Quaternion.identity);
        go.GetComponent<DroppedItem>().Setup(itemData, amount);
    }
}
