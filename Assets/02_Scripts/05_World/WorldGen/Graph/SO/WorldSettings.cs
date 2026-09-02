using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New World Settings", menuName = "Scriptable Objects/TestWorld/World Settings")]
public class WorldSettings : ScriptableObject
{
    [Header("--- World Gen Context ---")]
    [Tooltip("이번 월드 생성에 사용할 스토리 데이터")]
    [SerializeField] public StoryData CurrentStory;
    [SerializeField] public int WorldSeed = 0; // 월드 시드 (랜덤 시드로 덮어쓰기됨)

    [Header("Map Size")]
    [SerializeField] private int worldSmall;    
    [SerializeField] private int worldMedium;  
    [SerializeField] private int worldLarge;
    [SerializeField] private int chunkSize;


    private WorldSize _worldSize = WorldSize.Medium;
    public WorldSize WorldSize { get => _worldSize; set => _worldSize = value; }

    public int ChunkSize { get => chunkSize; }

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
    [Tooltip("기반 높이 설정")]
    [SerializeField] private float Height_Ocean;    
    [SerializeField] private float Height_Plains;
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
    #region Partition Settings, Spawn Settings
    public ForceSimulatorSettings ForceSimulatorSettings = new ForceSimulatorSettings();
    public PartitionSettings PartitionSettings = new PartitionSettings();
    public DisposeSettings DisposeSettings = new DisposeSettings();
    public DynamicSpawnSettings DynamicSpawnSettings = new DynamicSpawnSettings();
    #endregion


    #region Getters for World Settings
    public Vector2Int GetWorldSize()
    {
        return _worldSize switch
        {
            WorldSize.Small => new Vector2Int(worldSmall + 1, worldSmall + 1),
            WorldSize.Medium => new Vector2Int(worldMedium + 1, worldMedium + 1),
            WorldSize.Large => new Vector2Int(worldLarge + 1, worldLarge + 1),
            _ => new Vector2Int(worldMedium + 1, worldMedium + 1)
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
            case HeightLevel.Ocean: return Height_Ocean; // 바다 깊이 고정
            case HeightLevel.Plains: return Height_Plains;
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
#region Force Simulator Settings
[System.Serializable]
public class ForceSimulatorSettings
{
    [Header("Macro (Region-level)")]
    [Tooltip("구역(Region) 공간 대비 이상적 거리 비율 (기본 0.6)")]
    public float macroLengthRatio;
    [Tooltip("구역 간 기본적인 척력(밀어내는 힘) 배율 (기본 1.0)")]
    public float macroRepulsionFactor;

    [Header("Micro (Node-level)")]
    [Tooltip("방(Node) 공간 대비 이상적 거리 비율 (기본 0.8)")]
    public float microLengthRatio;
    [Tooltip("구역 내부 방들이 너무 뭉치지 않게 하는 척력 배율 (기본 1.5)")]
    public float microRepulsionFactor;

    [Header("Iteration Control")]
    [Tooltip("노드 개수 당 시뮬레이션 반복 횟수 (기본 3.0)")]
    public float iterationMultiplier;
    public int minIterations;
    public int maxIterations;
}
#endregion
#region Dispose Settings
[System.Serializable]
public class DisposeSettings
{
    [Header("Poisson Disk Sampling")]
    public float minObjectDistance = 1f;     // 오브젝트 간 최소 거리
    public int maxSamplingAttempts = 30;     // 푸아송 샘플링 시도 횟수
}
#endregion
#region Spawn Settings
[System.Serializable]
public class DynamicSpawnSettings
{
    [Header("Spawn Budget")]
    [Tooltip("DynamicSpawnDirector가 동시에 유지할 수 있는 전체 스폰 슬롯입니다.")]
    [Min(0)] public int MaxSpawnSlots = 30;

    [Header("Runtime Tracking")]
    [Tooltip("플레이어와 활성 청크 목록을 다시 찾는 간격입니다.")]
    [Min(0.1f)] public float TargetRefreshInterval = 1f;
    [Tooltip("모든 플레이어로부터 이 거리 이상 벗어나면 체류 시간 계산을 시작합니다.")]
    [Min(0f)] public float DespawnDistanceFromPlayers = 60f;

    [Header("Position Sampling")]
    [Tooltip("스폰 한 마리당 유효 위치를 찾기 위해 시도할 최대 횟수입니다.")]
    [Min(1)] public int MaxPositionAttempts = 12;
    [Tooltip("스폰 위치에서 동적 장애물을 검사할 반경입니다.")]
    [Min(0f)] public float CollisionCheckRadius = 0.5f;
    [Tooltip("스폰을 막을 레이어입니다. Nothing이면 물리 중첩 검사를 생략합니다.")]
    public LayerMask SpawnBlockingMask;
    [Tooltip("논리 지형 높이에 더할 Y 오프셋입니다.")]
    public float SpawnHeightOffset = 0.1f;
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
    Mesa,
    Highlands,
    Max
}


public enum WorldSeedChannel
{
    StoryGenerator = 101,

    RegionGenerator = 201,

    ForceSimulator = 301,

    //TODO: 그래프 이후 3단계는 청크 단위로 나누기
    TerritoryBuilder = 401,

    HeightBuilder = 501,

    ObjectDisposer = 601,

    ItemDisposer = 701,
}
#endregion
