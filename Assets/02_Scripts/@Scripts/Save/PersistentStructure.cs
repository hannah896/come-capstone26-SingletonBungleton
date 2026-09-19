using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>플레이어가 건축한 오브젝트의 영속 ID와 원본 아이템을 추적한다.</summary>
public sealed class PersistentStructure : MonoBehaviour
{
    private static readonly Dictionary<string, PersistentStructure> Entries = new();
    public string Id { get; private set; }
    public ItemDataSO SourceItem { get; private set; }
    public static IEnumerable<PersistentStructure> All => Entries.Values;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => Entries.Clear();

    public void Initialize(ItemDataSO sourceItem, string id = null)
    {
        Unregister();
        Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
        SourceItem = sourceItem;
        if (Entries.TryGetValue(Id, out var existing) && existing != null && existing != this)
            throw new InvalidOperationException($"중복 건축물 저장 ID: {Id}");
        Entries[Id] = this;
    }

    public void Unregister()
    {
        if (Id != null && Entries.TryGetValue(Id, out var value) && value == this)
            Entries.Remove(Id);
    }

    private void OnDestroy() => Unregister();
}
