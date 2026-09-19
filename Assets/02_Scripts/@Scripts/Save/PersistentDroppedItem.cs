using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>싱글플레이 드롭을 추적한다. 풀 반납 시 즉시 목록에서 제외한다.</summary>
public sealed class PersistentDroppedItem : MonoBehaviour
{
    private static readonly Dictionary<string, PersistentDroppedItem> Entries = new();
    public string Id { get; private set; }
    public Item Item { get; private set; }
    public static IEnumerable<PersistentDroppedItem> All => Entries.Values;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => Entries.Clear();

    public void Initialize(Item item, string id = null)
    {
        Unregister();
        Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
        Item = item;
        if (Entries.TryGetValue(Id, out var existing) && existing != null && existing != this)
            throw new InvalidOperationException($"중복 드롭 저장 ID: {Id}");
        Entries[Id] = this;
    }

    public void Unregister()
    {
        if (Id != null && Entries.TryGetValue(Id, out var value) && value == this)
            Entries.Remove(Id);
    }

    private void OnDisable() => Unregister();
    private void OnDestroy() => Unregister();
}
