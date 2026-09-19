using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlayerSaveData
{
    public string playerId;
    public int characterIndex;
    public Vector3 position;
    public float yaw;
    public PlayerStatusSaveData status = new();
    public InventorySaveData inventory = new();
    public CraftingSaveData crafting = new();
    // 구버전의 피어별 건축 저장 호환용. 현재 멀티 건축은 WorldSaveData.structures에 공유 상태로 저장한다.
    public bool hasLocalStructures;
    public List<StructureSaveData> localStructures = new();
}

[Serializable]
public class PlayerStatusSaveData
{
    public float hp;
    public float hunger;
    public float ego;
    public float temperature;
    public float wetness;
    public float dotDamagePerTick;
    public float dotTickInterval;
    public float dotTickTimer;
    public int dotTicksLeft;
}

[Serializable]
public class InventorySaveData
{
    public int slotCount = 20;
    public int quickSlotCount;
    public int selectedSlotIndex;
    public List<ItemStackSaveData> slots = new();
    public List<EquipmentSaveData> equipment = new();
}

[Serializable]
public class EquipmentSaveData
{
    public EquipSlot equipSlot;
    public ItemStackSaveData item;
}

[Serializable]
public class CraftingSaveData
{
    public List<string> learnedRecipeIds = new();
    public List<CraftJobSaveData> pending = new();
}

[Serializable]
public class CraftJobSaveData
{
    public string recipeId;
    public float remainingSeconds;
}
