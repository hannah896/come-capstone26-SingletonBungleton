using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

[Serializable]
public class ItemStackSaveData
{
    public int slotIndex = -1;
    public string itemId;
    public string itemKey;
    public int count;
    public float durability = -1f;
    public float spoilRemainingSeconds = -1f;
}

/// <summary>영구 아이템 ID를 런타임 Addressable 키와 연결한다.</summary>
public static class ItemSaveCatalog
{
    [Serializable] private class Catalog { public List<Entry> items = new(); }
    [Serializable] private class Entry { public string itemId; public string itemKey; }
    private static readonly Dictionary<string, string> Keys = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, ItemDataSO> Loaded = new(StringComparer.Ordinal);
    private static bool initialized;

    public static ItemStackSaveData Create(ItemDataSO so, int count, int slotIndex = -1,
        float durability = -1f, float spoilRemainingSeconds = -1f)
    {
        if (so == null || count <= 0) return null;
        if (string.IsNullOrWhiteSpace(so.itemID))
            throw new InvalidOperationException($"아이템 '{so.name}'에 영구 ID가 없습니다.");
        Loaded[so.itemID] = so;
        return new ItemStackSaveData
        {
            slotIndex = slotIndex, itemId = so.itemID, itemKey = so.name, count = count,
            durability = so.hasDurability ? (durability < 0f ? so.maxDurability : durability) : -1f,
            spoilRemainingSeconds = spoilRemainingSeconds
        };
    }

    public static async UniTask<ItemDataSO> ResolveAsync(ItemStackSaveData data, CancellationToken token = default)
    {
        if (data == null || data.count <= 0) return null;
        token.ThrowIfCancellationRequested();
        EnsureInitialized();
        if (string.IsNullOrWhiteSpace(data.itemId))
            throw new InvalidOperationException("저장된 아이템 ID가 비어 있습니다.");
        if (Loaded.TryGetValue(data.itemId, out var cached) && cached != null) return cached;
        string key = Keys.TryGetValue(data.itemId, out var mapped) ? mapped : data.itemKey;
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException($"아이템 '{data.itemId}'의 에셋 주소가 없습니다.");
        var so = await WorldItemSync.LoadItemDataAsync(key).AttachExternalCancellation(token);
        if (so == null || !string.Equals(so.itemID, data.itemId, StringComparison.Ordinal))
            throw new InvalidOperationException($"저장된 아이템 '{data.itemId}'을 '{key}'에서 찾을 수 없습니다.");
        Loaded[data.itemId] = so;
        return so;
    }

    private static void EnsureInitialized()
    {
        if (initialized) return;
        var asset = Resources.Load<TextAsset>("ItemSaveCatalog");
        if (asset != null)
        {
            var catalog = JsonUtility.FromJson<Catalog>(asset.text);
            foreach (var entry in catalog.items)
            {
                if (string.IsNullOrWhiteSpace(entry.itemId) || string.IsNullOrWhiteSpace(entry.itemKey)
                    || Keys.ContainsKey(entry.itemId))
                    throw new InvalidOperationException("아이템 저장 카탈로그의 ID가 비어 있거나 중복됩니다.");
                Keys.Add(entry.itemId, entry.itemKey);
            }
        }
        initialized = true;
    }
}
