using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEditorInternal;

/// <summary>
/// MapGenerator 사용 예제
/// </summary>
public class MapGeneratorUsageExample : MonoBehaviour
{
    [SerializeField] private MapGenerator _mapGenerator;

    async void Start()
    {
        await UniTask.WaitUntil(() => Managers.Instance != null);

        if (_mapGenerator == null)
            _mapGenerator = Utility.GetOrAddComponent<MapGenerator>(this.gameObject);
        // 예제: 다양한 맵 설정으로 생성
        // GenerateExampleMaps();
    }

    void Update()
    {
        // 테스트: 키 입력으로 다른 설정의 맵 생성
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            GenerateSmallBranchyMap();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            GenerateMediumLoopyMap();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            GenerateLargeRandomMap();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            GenerateHugeOpenMap();
        }else if(Input.GetKeyDown(KeyCode.Alpha5))
        {
            _mapGenerator.GenerateMapWithSettings(MapSettings.CreatePreset(
                MapSize.Large,
                LandBranchSetting.Never,
                LandLoopSetting.Never
            ));
        }
    }

    /// <summary>
    /// 작은 사이즈 + 가지가 많은 맵 (미로 같은 느낌)
    /// </summary>
    public void GenerateSmallBranchyMap()
    {
        MapSettings settings = MapSettings.CreatePreset(
            MapSize.Small,
            LandBranchSetting.Most,
            LandLoopSetting.Never
        );

        _mapGenerator.GenerateMapWithSettings(settings);
        Debug.Log("작은 가지형 맵 생성!");
    }

    /// <summary>
    /// 중간 사이즈 + 순환 경로가 있는 맵 (탐험하기 좋음)
    /// </summary>
    public void GenerateMediumLoopyMap()
    {
        MapSettings settings = MapSettings.CreatePreset(
            MapSize.Medium,
            LandBranchSetting.Default,
            LandLoopSetting.Always
        );

        _mapGenerator.GenerateMapWithSettings(settings);
        Debug.Log("중간 루프형 맵 생성!");
    }

    /// <summary>
    /// 큰 사이즈 + 랜덤 설정 맵 (매번 다른 느낌)
    /// </summary>
    public void GenerateLargeRandomMap()
    {
        MapSettings settings = MapSettings.CreatePreset(
            MapSize.Large,
            LandBranchSetting.Random,
            LandLoopSetting.Few
        );

        // 밀도도 랜덤하게
        settings.DensityMultiplier = Random.Range(0.8f, 1.5f);

        _mapGenerator.GenerateMapWithSettings(settings);
        Debug.Log("큰 랜덤 맵 생성!");
    }

    /// <summary>
    /// 매우 큰 사이즈 + 개방적인 맵 (오픈월드 느낌)
    /// </summary>
    public void GenerateHugeOpenMap()
    {
        MapSettings settings = MapSettings.CreatePreset(
            MapSize.Huge,
            LandBranchSetting.Least,
            LandLoopSetting.Always
        );

        //settings.DensityMultiplier = 0.7f; // 영역을 더 넓게

        _mapGenerator.GenerateMapWithSettings(settings);
        Debug.Log("거대 오픈 맵 생성!");
    }

    /// <summary>
    /// 코드로 완전히 커스텀 설정
    /// </summary>
    public void GenerateCustomMap(
        MapSize size,
        LandBranchSetting branch,
        LandLoopSetting loop,
        float density = 1f)
    {
        MapSettings settings = MapSettings.CreatePreset(size, branch, loop);
        settings.DensityMultiplier = density;

        _mapGenerator.GenerateMapWithSettings(settings);
    }

    /// <summary>
    /// 난이도별 맵 생성 예제
    /// </summary>
    public void GenerateMapByDifficulty(int difficulty)
    {
        MapSettings settings;

        switch (difficulty)
        {
            case 1: // 쉬움 - 작고 순환 경로 많음
                settings = MapSettings.CreatePreset(
                    MapSize.Small,
                    LandBranchSetting.Least,
                    LandLoopSetting.Always
                );
                break;

            case 2: // 보통 - 중간 크기, 균형잡힌 구조
                settings = MapSettings.CreatePreset(
                    MapSize.Medium,
                    LandBranchSetting.Default,
                    LandLoopSetting.Few
                );
                break;

            case 3: // 어려움 - 크고 복잡한 가지 구조
                settings = MapSettings.CreatePreset(
                    MapSize.Large,
                    LandBranchSetting.Most,
                    LandLoopSetting.Never
                );
                break;

            default: // 매우 어려움 - 거대하고 미로같은 구조
                settings = MapSettings.CreatePreset(
                    MapSize.Huge,
                    LandBranchSetting.Most,
                    LandLoopSetting.Never
                );
                settings.DensityMultiplier = 1.5f;
                break;
        }

        _mapGenerator.GenerateMapWithSettings(settings);
        Debug.Log($"난이도 {difficulty} 맵 생성!");
    }
}