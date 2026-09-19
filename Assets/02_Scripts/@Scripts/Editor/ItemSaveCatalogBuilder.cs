#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ItemSaveCatalogBuilder
{
    [Serializable] private class Catalog { public List<Entry> items = new(); }
    [Serializable] private class Entry { public string itemId; public string itemKey; }

    [MenuItem("Tools/Save/Rebuild Item Catalog")]
    public static void Rebuild()
    {
        var catalog = new Catalog();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDataSO", new[] { "Assets/05_Datas" }))
        {
            var so = AssetDatabase.LoadAssetAtPath<ItemDataSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (string.IsNullOrWhiteSpace(so.itemID) || !ids.Add(so.itemID))
                throw new InvalidOperationException($"아이템 '{so.name}'의 영구 ID가 없거나 중복됩니다.");
            catalog.items.Add(new Entry { itemId = so.itemID, itemKey = so.name });
        }
        catalog.items.Sort((a, b) => string.CompareOrdinal(a.itemId, b.itemId));
        const string path = "Assets/Resources/ItemSaveCatalog.json";
        File.WriteAllText(path, JsonUtility.ToJson(catalog, true), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(path);
        Debug.Log($"[Save] 아이템 카탈로그 {catalog.items.Count}개 갱신 완료");
    }
}
#endif
