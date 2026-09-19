using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>시드로 재생성하는 원본 월드와 별개로 보관하는 플레이 중 변경분.</summary>
[Serializable]
public sealed class WorldSaveData
{
    public int seed;
    public WorldBranchSetting branch;
    public WorldLoopSetting loop;
    public WorldSize size;
    public float totalSeconds;
    public List<DestroyedObjectSaveData> destroyedObjects = new();
    public List<StructureSaveData> structures = new();
    public List<DroppedItemSaveData> droppedItems = new();
    public NetworkWorldSaveData network;
}

[Serializable]
public sealed class DestroyedObjectSaveData
{
    public int instanceId;
    public float respawnTime;
}

[Serializable]
public sealed class StructureSaveData
{
    public string id;
    public ItemStackSaveData sourceItem;
    public Vector3 position;
    public Quaternion rotation = Quaternion.identity;
    public Vector3 scale = Vector3.one;
    public int slotCount;
    public List<ItemStackSaveData> slots = new();
    public bool isCooking;
}

[Serializable]
public sealed class DroppedItemSaveData
{
    public string id;
    public ItemStackSaveData item;
    public Vector3 position;
}
