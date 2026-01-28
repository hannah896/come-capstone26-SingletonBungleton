using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class ItemManager
{
    private const string ITEM_DATA_LABEL = "ItemData";
    private Dictionary<string, ItemDataSO> _itemDatabase = new Dictionary<string, ItemDataSO>();
    private ResourceManager _resourceManager;

    public async UniTask Init(ResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
        Debug.Log("[ItemManager] 아이템 데이터 로드 시작...");

        var itemDataList = await _resourceManager.LoadAssetsByLabelAsync<ItemDataSO>(
            ITEM_DATA_LABEL,
            AssetCacheType.Required
        );

        foreach (var itemData in itemDataList)
        {
            if (!string.IsNullOrEmpty(itemData.itemID))
            {
                if (_itemDatabase.ContainsKey(itemData.itemID))
                {
                    Debug.LogWarning($"[ItemManager] 중복된 아이템 ID: {itemData.itemID}");
                    continue;
                }

                _itemDatabase[itemData.itemID] = itemData;
                Debug.Log($"[ItemManager] 로드됨: {itemData.itemID} ({itemData.itemName})");
            }
            else
            {
                Debug.LogWarning($"[ItemManager] itemID가 비어있는 아이템 발견");
            }
        }

        Debug.Log($"[ItemManager] 총 {_itemDatabase.Count}개의 아이템 로드 완료!");
    }

    public ItemDataSO GetItemData(string itemID)
    {
        if (_itemDatabase.TryGetValue(itemID, out var data))
        {
            return data;
        }
        Debug.LogWarning($"[ItemManager] 아이템을 찾을 수 없음: {itemID}");
        return null;
    }

    public Item CreateItem(string itemID, int count = 1)
    {
        var data = GetItemData(itemID);
        if (data == null) return null;

        Item newItem = new Item(data, count);
        Debug.Log($"[ItemManager] 아이템 생성: {newItem.ToString()}");
        return newItem;
    }

    public List<ItemDataSO> GetItemsByType(ItemType type)
    {
        List<ItemDataSO> result = new List<ItemDataSO>();
        foreach (var data in _itemDatabase.Values)
        {
            if (data.itemType == type)
                result.Add(data);
        }
        return result;
    }

    public int GetTotalItemCount()
    {
        return _itemDatabase.Count;
    }
}