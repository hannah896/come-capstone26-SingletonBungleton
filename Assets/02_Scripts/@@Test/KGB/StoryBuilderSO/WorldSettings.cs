using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New World Settings", menuName = "ScriptableObjects/TestMap/World Settings")]
public class WorldSettings : ScriptableObject
{
    [Header("--- Game Context ---")]
    [Tooltip("이번 월드 생성에 사용할 스토리 데이터")]
    [SerializeField] private StoryData currentStory; 
    public StoryData CurrentStory => currentStory;

    [Header("Map Size")]
    [SerializeField] private int sizeSmall = 80;    
    [SerializeField] private int sizeMedium = 200;  
    [SerializeField] private int sizeLarge = 300;   
    [SerializeField] private int sizeHuge = 400;    
    private WorldSize _worldSize = WorldSize.Medium;
    public WorldSize WorldSize { get => _worldSize; set => _worldSize = value; }

    [Header("Land Branch")]
    [SerializeField] private float branchNever = 0f;
    [SerializeField] private float branchLeast = 0.4f;
    [SerializeField] private float branchDefault = 0.6f;
    [SerializeField] private float branchMost = 0.8f;
    private WorldBranchSetting _worldBranch = WorldBranchSetting.Default;
    public WorldBranchSetting WorldBranch
    {
        get => _worldBranch;
        set {
            if (value != WorldBranchSetting.Random)
                _worldBranch = value;
            else
                _worldBranch = (WorldBranchSetting)Random.Range(0, 4);
        }
    }

    [Header("Land Loop")]
    [SerializeField] private float loopNever = 0f;
    [SerializeField] private float loopDefault = 0.5f;
    [SerializeField] private float loopAlways = 0.8f;
    private WorldLoopSetting _worldLoop = WorldLoopSetting.Default;
    public WorldLoopSetting WorldLoop { get => _worldLoop; set => _worldLoop = value; }

    [Header("Advanced Settings")]
    [Range(0.5f, 2f)]
    [SerializeField] private float densityMultiplier = 1f; // 영역 밀도 조절 0.5 ~ 2 
    public float DensityMultiplier
    {
        get => densityMultiplier;
        set => densityMultiplier = Mathf.Clamp(value, 0.5f, 2f);
    }



    // ========== FD Force Simulation Settings ==========
    [Header("FD Force Simulation")]
    [SerializeField] private int simulationIterations = 100;
    [SerializeField] private float repulsionStrength = 500f;
    [SerializeField] private float attractionStrength = 0.1f;
    [SerializeField] private float idealEdgeLength = 20f;
    [SerializeField] private float dampingFactor = 0.9f;
    [SerializeField] private float minNodeDistance = 10f;

    public int SimulationIterations => simulationIterations;
    public float RepulsionStrength => repulsionStrength;
    public float AttractionStrength => attractionStrength;
    public float IdealEdgeLength => idealEdgeLength;
    public float DampingFactor => dampingFactor;
    public float MinNodeDistance => minNodeDistance;

    public Vector2Int GetWorldSize()
    {
        return _worldSize switch
        {
            WorldSize.Small => new Vector2Int(sizeSmall, sizeSmall),
            WorldSize.Medium => new Vector2Int(sizeMedium, sizeMedium),
            WorldSize.Large => new Vector2Int(sizeLarge, sizeLarge),
            WorldSize.Huge => new Vector2Int(sizeHuge, sizeHuge),
            _ => new Vector2Int(sizeMedium, sizeMedium)
        };
    }

    public float GetBranchMultiplier()
    {
        return _worldBranch switch
        {
            WorldBranchSetting.Never => branchNever,     // 0f
            WorldBranchSetting.Least => branchLeast,     // 0.4f
            WorldBranchSetting.Default => branchDefault, // 0.6f
            WorldBranchSetting.Most => branchMost,       // 0.8f
            _ => 0.6f
        };
    }

    public float GetLoopMultiplier()
    {
        return _worldLoop switch
        {
            WorldLoopSetting.Never => loopNever,       // 0f
            WorldLoopSetting.Default => loopDefault,   // 0.5f
            WorldLoopSetting.Always => loopAlways,     // 0.8f
            _ => 0.5f
        };
    }
}

#region Enums : WorldSize, WorldBranchSetting, WorldLoopSetting
public enum WorldSize
{
    Small,
    Medium,
    Large,
    Huge
}

public enum WorldBranchSetting
{
    Never,
    Least,
    Default,
    Most,
    Random
}

public enum WorldLoopSetting
{
    Never,
    Default,
    Always
}
#endregion