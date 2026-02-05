using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New World Settings", menuName = "ScriptableObjects/TestWorld/World Settings")]
public class WorldSettings : ScriptableObject
{
    [Header("--- Game Context ---")]
    [Tooltip("이번 월드 생성에 사용할 스토리 데이터")]
    [SerializeField] public StoryData CurrentStory; 

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
    [SerializeField] private float loopAlways = 0.75f;
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
    #region Force Sim Settings, Partition Settings, Spawn Settings
    public ForceSimSettings MacroSettings = new ForceSimSettings { idealEdgeLength = 20f, repulsionStrength = 250f }; // Region 배치용 (넓게)
    public ForceSimSettings MicroSettings = new ForceSimSettings { idealEdgeLength = 5f, repulsionStrength = 50f };
    public ForceSimSettings FastSettings = new ForceSimSettings { idealEdgeLength = 5f, repulsionStrength = 50f, simulationIterations = 50 };
    
    public PartitionSettings PartitionSettings = new PartitionSettings();
    public DisposeSettings DisposeSettings = new DisposeSettings();
    #endregion


    #region Getters for World Settings
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
    #endregion
    #region Debug Settings
    [Header("Debug")]
    [SerializeField] private bool _enableStepByStep = false;
    public bool EnableStepByStep => _enableStepByStep;
    [SerializeField] private float _stepDelay = 0.1f;
    public float StepDelay => _stepDelay;
    #endregion
}

#region ForceSim Settings
[System.Serializable]
public class ForceSimSettings
{
    [Header("Force Simulation")]
    public int simulationIterations = 150;
    public float repulsionStrength = 250f;
    public float attractionStrength = 0.3f;
    public float idealEdgeLength = 15f;
    public float dampingFactor = 0.9f;
    public float minNodeDistance = 10f;
}
#endregion
#region Partition Settings
[System.Serializable]
public class PartitionSettings
{
    [Header("Noise Settings")]
    public float noiseScale = 0.1f;          // 펄린 노이즈 스케일
    public float noiseStrength = 15f;        // 노이즈가 거리에 미치는 영향력
    public int noiseSeed = 0;                // 노이즈 시드

    [Header("Border Settings")]
    public int borderWidth = 2;              // 영역 경계 두께
    public float oceanThreshold = 0.85f;     // 바다로 처리할 맵 가장자리 비율

    [Header("Performance")]
    public int batchSize = 1000;             // 비동기 처리 시 배치 크기
}
#endregion
#region Dispose Settings
[System.Serializable]
public class DisposeSettings
{
    [Header("Poisson Disk Sampling")]
    public float minObjectDistance = 2f;     // 오브젝트 간 최소 거리
    public int maxSamplingAttempts = 30;     // 푸아송 샘플링 시도 횟수

    [Header("Performance")]
    public int batchSize = 100;              // 비동기 처리 배치 크기
}
#endregion

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