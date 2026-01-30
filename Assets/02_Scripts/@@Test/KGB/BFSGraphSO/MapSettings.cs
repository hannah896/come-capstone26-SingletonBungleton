using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "New Map Settings", menuName = "ScriptableObjects/TestMap/Map Settings")]
public class MapSettings : ScriptableObject
{
    [Header("Map Size")]
    [SerializeField] private int sizeSmall; // = 80;    
    [SerializeField] private int sizeMedium;// = 200;  
    [SerializeField] private int sizeLarge;// = 300;   
    [SerializeField] private int sizeHuge;// = 400;    
    private MapSize _mapSize = MapSize.Medium;
    public MapSize MapSize {get => _mapSize; set => _mapSize = value; }

    [Header("Land Branch")]
    [SerializeField] private float branchNever;// = 0f;
    [SerializeField] private float branchLeast;// = 0.4f;
    [SerializeField] private float branchDefault;// = 0.6f;
    [SerializeField] private float branchMost;// = 0.8f;
    private LandBranchSetting _landBranch = LandBranchSetting.Default;
    public LandBranchSetting LandBranch
    {
        get => _landBranch;
        set {
            if (value != LandBranchSetting.Random)
                _landBranch = value;
            else
                _landBranch = (LandBranchSetting)Random.Range(0, 4);
            
        }
    }

    [Header("Land Loop")]
    [SerializeField] private float loopNever;// = 0f;
    [SerializeField] private float loopDefault;// = 0.5f;
    [SerializeField] private float loopAlways;// = 0.8f;
    private LandLoopSetting _landLoop = LandLoopSetting.Default;
    public LandLoopSetting LandLoop { get => _landLoop; set => _landLoop = value; }

    [Header("Advanced Settings")]
    [Range(0.5f, 2f)]
    private float _densityMultiplier;// = 1f; // 영역 밀도 조절 0.5 ~ 2 
    public float DensityMultiplier
    {
        get => _densityMultiplier;
        set => _densityMultiplier = Mathf.Clamp(value, 0.5f, 2f);
    }

    // ========== FD Graph Settings ==========
    [Header("FD Graph Settings")]
    [SerializeField] private int _nodeCount;// = 30;
    [SerializeField] private int _maxDepth;// = 5;
    [SerializeField] private int _minChildNodes;// = 1;
    [SerializeField] private int _maxChildNodes;// = 3;

    public int NodeCount => _nodeCount;
    public int MaxDepth => _maxDepth;
    public int MinChildNodes => _minChildNodes;
    public int MaxChildNodes => _maxChildNodes;

    // ========== FD Force Simulation Settings ==========
    [Header("FD Force Simulation")]
    [SerializeField] private int _simulationIterations;// = 100;
    [SerializeField] private float _repulsionStrength;// = 500f;
    [SerializeField] private float _attractionStrength;// = 0.1f;
    [SerializeField] private float _idealEdgeLength;// = 20f;
    [SerializeField] private float _dampingFactor;// = 0.9f;
    [SerializeField] private float _minNodeDistance;// = 10f;

    public int SimulationIterations => _simulationIterations;
    public float RepulsionStrength => _repulsionStrength;
    public float AttractionStrength => _attractionStrength;
    public float IdealEdgeLength => _idealEdgeLength;
    public float DampingFactor => _dampingFactor;
    public float MinNodeDistance => _minNodeDistance;

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
            _ => 0.25f
        };
    }

    public float GetLoopMultiplier()
    {
        return _landLoop switch
        {
            LandLoopSetting.Never => loopNever,             //0f,
            LandLoopSetting.Default => loopDefault,         //0.15f,
            LandLoopSetting.Always => loopAlways,           //0.5f,
            _ => 0.15f
        };
    }

    //// 코드에서 사용할 수 있는 프리셋 생성 메서드
    //public static void CreatePreset(MapSize size, LandBranchSetting branch, LandLoopSetting loop)
    //{
    //    MapSize = size;
    //    settings.LandBranch = branch;
    //    settings.LandLoop = loop;

    //    settings.hideFlags = HideFlags.DontSave; // 에디터에 저장되지 않도록 설정

    //}

}

#region Enums : MapSize, LandBranchSetting, LandLoopSetting
public enum MapSize
{
    Small,
    Medium,
    Large,
    Huge
}

public enum LandBranchSetting
{
    Never,
    Least,
    Default,
    Most,
    Random
}

public enum LandLoopSetting
{
    Never,
    Default,
    Always
}
#endregion