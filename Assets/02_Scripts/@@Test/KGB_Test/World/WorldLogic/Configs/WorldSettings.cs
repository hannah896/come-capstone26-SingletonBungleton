using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New World Settings", menuName = "ScriptableObjects/TestWorld/World Settings")]
public class WorldSettings : ScriptableObject
{
    [Header("--- Game Context ---")]
    [Tooltip("이번 월드 생성에 사용할 스토리 데이터")]
    [SerializeField] public StoryData CurrentStory;
    [SerializeField] public int WorldSeed = 0; // 월드 시드 (랜덤 시드로 덮어쓰기됨)

    [Header("Map Size")]
    [SerializeField] private int worldSmall = 255;    
    [SerializeField] private int worldMedium;  
    [SerializeField] private int worldLarge;   

    private WorldSize _worldSize = WorldSize.Medium;
    public WorldSize WorldSize { get => _worldSize; set => _worldSize = value; }

    [Header("Land Branch")]
    [SerializeField] private float branchNever;
    [SerializeField] private float branchLeast;
    [SerializeField] private float branchDefault;
    [SerializeField] private float branchMost;
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
    [SerializeField] private float loopNever;
    [SerializeField] private float loopDefault;
    [SerializeField] private float loopAlways;
    private WorldLoopSetting _worldLoop = WorldLoopSetting.Default;
    public WorldLoopSetting WorldLoop { get => _worldLoop; set => _worldLoop = value; }

    [Header("Terrain Height Settings")]
    [Tooltip("각 지형 등급(Tier)별 실제 높이 블록 설정")]
    [SerializeField] private float Height_Plains;
    [SerializeField] private float Height_Hills;
    [SerializeField] private float Height_Mesa;
    [SerializeField] private float Height_Highlands;
    [SerializeField] private float Hegiht_Max;

    [Header("Advanced Settings")]
    //[Range(0.5f, 2f)]
    //[SerializeField] private float densityMultiplier = 1f; // 영역 밀도 조절 0.5 ~ 2 
    //public float DensityMultiplier
    //{
    //    get => densityMultiplier;
    //    set => densityMultiplier = Mathf.Clamp(value, 0.5f, 2f);
    //}
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
            WorldSize.Small => new Vector2Int(worldSmall, worldSmall),
            WorldSize.Medium => new Vector2Int(worldMedium, worldMedium),
            WorldSize.Large => new Vector2Int(worldLarge, worldLarge),
            _ => new Vector2Int(worldMedium, worldMedium)
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

    public float GetHeight(HeightLevel heightLevel)
    {
        switch (heightLevel)
        {
            case HeightLevel.Ocean: return -2.0f; // 바다 깊이 고정
            case HeightLevel.Plains: return Height_Plains;
            case HeightLevel.Hills: return Height_Hills;
            case HeightLevel.Mesa: return Height_Mesa;
            case HeightLevel.Highlands: return Height_Highlands;
            case HeightLevel.Max: return Hegiht_Max;
            default: return 0.0f;
        }
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
#region Partition Noise Settings
[System.Serializable]
public class PartitionSettings
{
    [Header("Noise Settings")]
    public float noiseScale = 0.1f;          // 펄린 노이즈 스케일
    public float noiseStrength = 15f;        // 노이즈가 거리에 미치는 영향력

    [Header("Border Settings")]
    public int borderWidth = 2;              // 영역 경계 두께
    public float oceanThreshold = 0.85f;     // 바다로 처리할 맵 가장자리 비율

    [Header("Performance")]
    public int batchSize = 100000;             // 비동기 처리 시 배치 크기
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
#region Height Noise Parameters
[System.Serializable]
public class NoiseParams
{
    [Tooltip("지역 안의 최소 봉우리 횟수 ")]
    public float MinBumps;

    [Tooltip("지역 안의 봉우리 최대 횟수")]
    public float MaxBumps;

    [Tooltip("지형의 굴곡이 최대 몇 블록 높이까지 생기는가?")]
    public float HeightVarianceBlocks;

    [Header("Fractal Noise (fBm) Settings")]
    [Tooltip("노이즈 겹침 횟수 (1이면 매끄러움, 높을수록 원래의 노이즈 값 안에서 요동침.)")]
    public int Octaves;

    [Tooltip("다음 옥타브의 진폭(영향력) 감소 비율 (기본 0.4~0.5)")]
    public float Persistence;

    [Tooltip("다음 옥타브의 주파수(촘촘함) 증가 비율 (기본 1.5~2.0)")]
    public float Lacunarity;

}
#endregion

#region Enums : WorldSize, WorldBranchSetting, WorldLoopSetting, HeightLevel, NoiseTier
public enum WorldSize
{
    Small,
    Medium,
    Large
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

public enum HeightLevel
{ 
    Ocean,      
    Plains,               
    Hills,
    Mesa,
    Highlands, 
    Max
}


public enum WorldSeedChannel
{
    Story_PickParentNode = 101,
    Story_TryProcessNextRegionAsync = 102,
    Story_ApplyWorldLoopAsync = 103,

    Region_GetDirectionAwayFromGrandparent = 202,
    Region_PickParentRoom = 203,
    Region_AssignEssentialRooms = 204,
    Region_GenerateRoomsForRegion = 205,
    Region_CreateLoopsAsync = 206,

    Force_ArrangeRegionNodes = 301,

    Territory_GenerateNoiseWorld = 401,

    Height_BuildRegionFrequencyCache = 501,
    Height_GenerateHeightMapAsync = 502,

    Disposer_SpawnRegionObjects = 601,
    Disposer_SuffleList = 602,
    Disposer_WeightedRandom = 603,
    Disposer_GeneratePoissonPoints = 604,
}
#endregion