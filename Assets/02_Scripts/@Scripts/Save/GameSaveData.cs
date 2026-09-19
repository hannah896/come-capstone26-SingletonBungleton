using System;
using System.Collections.Generic;

/// <summary>씬 오브젝트 참조 없이 보관하는 월드 하나의 저장 데이터.</summary>
[Serializable]
public class GameSaveData
{
    public const int CurrentSchemaVersion = 1;
    public const int CurrentGeneratorVersion = 1;
    public int schemaVersion = CurrentSchemaVersion;
    public int generatorVersion = CurrentGeneratorVersion;
    public string worldId;
    public string worldName;
    public string savedAtUtc;
    public bool isMultiplayer;
    public WorldSaveData world;
    public List<PlayerSaveData> players = new();
}
