using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New World Settings", menuName = "ScriptableObjects/TestWorld/World Settings")]
public class WorldSettings : ScriptableObject
{
    [Header("--- Game Context ---")]
    [Tooltip("이번 월드 생성에 사용할 스토리 데이터")]
    [SerializeField] public StoryData CurrentStory; 

    [Header("Map Size")]
    [SerializeField] private int tileGridSmall = 300;    
    [SerializeField] private int tileGridMedium = 350;  
    [SerializeField] private int tileGridLarge = 400;   
    [SerializeField] private int tileGridHuge = 500;
    private WorldSize _worldSize = WorldSize.Medium;
    public WorldSize WorldSize { get => _worldSize; set => _worldSize = value; }

    [Header("Optimization")]
    [Tooltip("1개의 타일이 차지하는 유닛 크기 (예: 4면 1타일 = 4x4 WorldUnit)")]
    [SerializeField] private int _tileUnitSize = 2; // 기본값 4
    [SerializeField] private int _tileUnitHeight; // 타일 높이   
    public int TileUnitSize { get => _tileUnitSize; set => _tileUnitSize = Mathf.Max(1, value); } 
    public int TileUnitHeight { get => _tileUnitSize / 2;}


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
    [SerializeField] private float loopAlways = 1.0f;
    private WorldLoopSetting _worldLoop = WorldLoopSetting.Default;
    public WorldLoopSetting WorldLoop { get => _worldLoop; set => _worldLoop = value; }

    [Header("Terrain Height Settings")]
    [Tooltip("각 지형 등급(Tier)별 실제 높이(Y축) 설정")]
    [SerializeField] private float Height_Plains = 0.0f;
    [SerializeField] private float Height_LowHills = 10.0f;
    [SerializeField] private float Height_Hills = 25.0f;
    [SerializeField] private float Height_Highlands = 50.0f;

    [Header("--- Low Frequency (거대한 평원/고원) ---")]
    // 방 하나에 봉우리가 1~1.5개 (방 전체가 서서히 높아지는 거대한 고원 느낌)
    public NoiseParams Noise_LowLow = new NoiseParams(1.0f, 1.0f, 1.0f);
    public NoiseParams Noise_LowMid = new NoiseParams(1.0f, 1.0f, 5.0f);
    public NoiseParams Noise_LowHigh = new NoiseParams(1.0f, 1.5f, 15.0f); // 초거대 화산 1개 느낌

    [Header("--- Mid Frequency (일반 숲/언덕) ---")]
    // 방 하나에 언덕이 2~3개 (적당히 오르락 내리락 하는 숲)
    public NoiseParams Noise_MidLow = new NoiseParams(1.5f, 2.5f, 2.0f);
    public NoiseParams Noise_MidMid = new NoiseParams(2.0f, 3.0f, 5.0f);
    public NoiseParams Noise_MidHigh = new NoiseParams(2.0f, 3.5f, 10.0f);

    [Header("--- High Frequency (복잡한 산맥/바위) ---")]
    // 방 하나에 봉우리가 4~6개 (완전 빽빽하고 험준한 산맥)
    public NoiseParams Noise_HighLow = new NoiseParams(4.0f, 6.0f, 1.0f);
    public NoiseParams Noise_HighMid = new NoiseParams(4.0f, 6.0f, 3.0f);
    public NoiseParams Noise_HighHigh = new NoiseParams(5.0f, 8.0f, 5.0f);



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
    public Vector2Int GetTileGridSize()
    {
        return _worldSize switch
        {
            WorldSize.Small => new Vector2Int(tileGridSmall, tileGridSmall),
            WorldSize.Medium => new Vector2Int(tileGridMedium, tileGridMedium),
            WorldSize.Large => new Vector2Int(tileGridLarge, tileGridLarge),
            WorldSize.Huge => new Vector2Int(tileGridHuge, tileGridHuge),
            _ => new Vector2Int(tileGridMedium, tileGridMedium)
        };
    }

    public Vector2 GetRealWorldSize()
    {
        Vector2Int grid = GetTileGridSize();
        return new Vector2(grid.x * TileUnitSize, grid.y * TileUnitSize);
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

    public float GetHeight(HeightLevel tier)
    {
        switch (tier)
        {
            case HeightLevel.Ocean: return -2.0f; // 바다 깊이 고정
            case HeightLevel.Plains: return Height_Plains;
            case HeightLevel.LowHills: return Height_LowHills;
            case HeightLevel.Hills: return Height_Hills;
            case HeightLevel.Highlands: return Height_Highlands;
            default: return 0.0f;
        }
    }

    public NoiseParams GetNoiseSettings(NoiseTier tier)
    {
        switch (tier)
        {
            case NoiseTier.Low_Low: return Noise_LowLow;
            case NoiseTier.Low_Mid: return Noise_LowMid;
            case NoiseTier.Low_High: return Noise_LowHigh;

            case NoiseTier.Mid_Low: return Noise_MidLow;
            case NoiseTier.Mid_Mid: return Noise_MidMid;
            case NoiseTier.Mid_High: return Noise_MidHigh;

            case NoiseTier.High_Low: return Noise_HighLow;
            case NoiseTier.High_Mid: return Noise_HighMid;
            case NoiseTier.High_High: return Noise_HighHigh;

            default: return Noise_MidMid;
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

[System.Serializable] // 이게 있어야 인스펙터에 보입니다!
public class NoiseParams
{
    [Tooltip("지역 안의 봉우리 횟수 ")]
    public float MinBumps;

    [Tooltip("지역 안의 봉우리 최대 횟수")]
    public float MaxBumps;

    [Tooltip("지형의 굴곡이 최대 몇 블록 높이까지 생기는가?")]
    public float HeightVarianceBlocks;

    public NoiseParams(float minBumps, float maxBumps, float heightVarianceBlocks)
    {
        MinBumps = minBumps;
        MaxBumps = maxBumps;
        HeightVarianceBlocks = heightVarianceBlocks;
    }
}

#region Enums : WorldSize, WorldBranchSetting, WorldLoopSetting, HeightLevel, NoiseTier
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

public enum HeightLevel
{ 
    Ocean,      
    Plains,         
    LowHills,           
    Hills,              
    Highlands,          
}

public enum NoiseTier
{
    // [빈도 Low] : 넓직넓직한 지형
    Low_Low,    // 평평함
    Low_Mid,    // 완만한 언덕
    Low_High,   // 높은 산 (거대함)

    // [빈도 Mid] : 일반적인 지형
    Mid_Low,    // 약간 울퉁불퉁
    Mid_Mid,    // 보통 야생
    Mid_High,   // 험함

    // [빈도 High] : 자글자글한 지형
    High_Low,   // 자갈/노이즈 바닥
    High_Mid,   // 거친 바위산
    High_High   // 카오스
}
#endregion