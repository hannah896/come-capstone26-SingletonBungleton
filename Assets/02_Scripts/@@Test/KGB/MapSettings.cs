using UnityEngine;

[CreateAssetMenu(fileName = "New Map Settings", menuName = "Test_Map/Map Settings")]
public class MapSettings : ScriptableObject
{
    [Header("Map Size")]
    [SerializeField] private int sizeSmall = 80;    //6400
    [SerializeField] private int sizeMedium = 120;  //14400
    [SerializeField] private int sizeLarge = 160;   //25600
    [SerializeField] private int sizeHuge = 200;    //40000
    private MapSize _mapSize = MapSize.Medium;
    public MapSize MapSizeOption => _mapSize;

    [Header("Land Branch")]
    [SerializeField] private float branchNever = 0f;
    [SerializeField] private float branchLeast = 0.1f;
    [SerializeField] private float branchDefault = 0.25f;
    [SerializeField] private float branchMost = 0.4f;
    private LandBranchSetting _landBranch = LandBranchSetting.Default;
    public LandBranchSetting LandBranchOption => _landBranch;

    [Header("Land Loop")]
    [SerializeField] private float loopNever = 0f;
    [SerializeField] private float loopFew = 0.15f;
    [SerializeField] private float loopAlways = 0.5f;
    private LandLoopSetting _landLoop = LandLoopSetting.Few;
    public LandLoopSetting LandLoopOption => _landLoop;

    [Header("Advanced Settings")]
    [Range(0.5f, 2f)]
    private float _densityMultiplier = 1f; // 영역 밀도 조절 0.5 ~ 2 
    public float DensityMultiplier
    {
        get => _densityMultiplier;
        set => _densityMultiplier = Mathf.Clamp(value, 0.5f, 2f);
    }
    public Vector2Int GetMapSize()
    {
        return _mapSize switch
        {
            MapSize.Small => new Vector2Int(sizeSmall, sizeSmall),
            MapSize.Medium => new Vector2Int(sizeMedium, sizeMedium),
            MapSize.Large => new Vector2Int(sizeLarge, sizeLarge),
            MapSize.Huge => new Vector2Int(sizeHuge, sizeHuge),
            _ => new Vector2Int(sizeMedium, sizeMedium)
        };
    }



    public float GetBranchMultiplier()
    {
        return _landBranch switch
        {
            LandBranchSetting.Never => branchNever,     // 0f
            LandBranchSetting.Least => branchLeast,     // 0.1f
            LandBranchSetting.Default => branchDefault, // 0.25f
            LandBranchSetting.Most => branchMost,       // 0.4f
            LandBranchSetting.Random => Random.Range(branchLeast, branchMost),  // 0.1f ~ 0.4f
            _ => 0.25f
        };
    }

    public float GetLoopMultiplier()
    {
        return _landLoop switch
        {
            LandLoopSetting.Never => loopNever,     //0f,
            LandLoopSetting.Few => loopFew,         //0.15f,
            LandLoopSetting.Always => loopAlways,   //0.5f,
            _ => 0.15f
        };
    }

    // 코드에서 사용할 수 있는 프리셋 생성 메서드
    public static MapSettings CreatePreset(MapSize size, LandBranchSetting branch, LandLoopSetting loop)
    {
        MapSettings settings = CreateInstance<MapSettings>();
        settings._mapSize = size;
        settings._landBranch = branch;
        settings._landLoop = loop;

        settings.hideFlags = HideFlags.DontSave; // 에디터에 저장되지 않도록 설정

        return settings;
    }

    public MapSettings Clone()
    {
        MapSettings clone = Instantiate(this);

        clone.hideFlags = HideFlags.DontSave;

        return clone;
    }
}

#region Enums : MapSize, LandBranchSetting, LandLoopSetting
public enum MapSize
{
    Small,      // 작은 (80x80)
    Medium,     // 중간 (120x120)
    Large,      // 큰 (160x160)
    Huge        // 매우 큰 (200x200)
}

public enum LandBranchSetting
{
    Never,      // 없음 (0)
    Least,      // 최소 (0.1)
    Default,    // 기본 (0.25)
    Most,       // 최대 (0.4)
    Random      // 무작위 (0.1 ~ 0.4)
}

public enum LandLoopSetting
{
    Never,      // 없음 (0)
    Few,        // 적음 (0.15)
    Always      // 항상 (0.5)
}
#endregion