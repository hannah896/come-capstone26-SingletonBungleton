using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>세션 식별자나 Fusion 객체를 저장하지 않는 호스트 월드 상태.</summary>
[Serializable]
public class NetworkWorldSaveData
{
    public int lastDropId;
    public List<NetworkDestroyedSaveData> destroyed = new();
    public List<NetworkDropSaveData> drops = new();
}

[Serializable]
public class NetworkDestroyedSaveData
{
    public int instanceId;
    public float respawnTime;
}

[Serializable]
public class NetworkDropSaveData
{
    public int dropId;
    public Vector3 position;
    public ItemStackSaveData item;
}
