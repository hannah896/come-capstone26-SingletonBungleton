using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// MapGenerator 사용 예제
/// </summary>
public class MapGeneratorUsageExample : MonoBehaviour
{
    [SerializeField] private MapGenerator _mapGenerator;

    [SerializeField] private MapSettings _mapSettings;
    
    private string _mapSettingLabel = "TestMapSettings";

    private CancellationTokenSource _cts;

    #region Load MapSettings
    private async UniTask LoadMapSettingAsync(CancellationToken ct)
    {
        try
        {
            _mapSettings = await Extensions.LoadAssetAsync<MapSettings>(
                _mapSettingLabel,
                AssetCacheType.Required,
                ct
            );
            Debug.Log("MapSettings 로드 완료");
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log("MapSettings 로드 취소됨");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"MapSettings 로드 실패: {e.Message}");
        }
    }
    #endregion
    async void Start()
    {
        await UniTask.WaitUntil(() => Managers.Instance != null);
        _cts = new CancellationTokenSource();

        if (_mapGenerator == null)
            _mapGenerator = Utility.GetOrAddComponent<MapGenerator>(this.gameObject);

        await LoadMapSettingAsync(_cts.Token);
        
        // 예제: 다양한 맵 설정으로 생성
        // GenerateExampleMaps();
    }

    void Update()
    {
        // 테스트: 키 입력으로 다른 설정의 맵 생성
        
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            GenerateMapByKey(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            GenerateMapByKey(2);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            GenerateMapByKey(3);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            GenerateMapByKey(4);
        }else if(Input.GetKeyDown(KeyCode.Alpha5))
        {
            GenerateMapByKey(5);
        }else if(Input.GetKeyDown(KeyCode.Alpha6))
        {
            GenerateMapByKey(6);
        }
        
    }


    /// <summary>
    /// 키다운 맵 생성 예제
    /// </summary>
    public void GenerateMapByKey(int difficulty)
    {
        _mapSettings.MapSize = MapSize.Huge;
        switch (difficulty)
        {
            case 1: // 가지 : 최대, 순환 : 항상
                _mapSettings.LandBranch = LandBranchSetting.Most;
                _mapSettings.LandLoop = LandLoopSetting.Always;
                break;

            case 2: // 가지 : 절대, 순환 : 절대
                _mapSettings.LandBranch = LandBranchSetting.Never;
                _mapSettings.LandLoop = LandLoopSetting.Never;
                break;

            case 3: // 가지 : 기본, 순환 : 항상
                _mapSettings.LandBranch = LandBranchSetting.Default;
                _mapSettings.LandLoop = LandLoopSetting.Always;
                break;

            case 4: // 가지 최대, 순환 : 기본
                _mapSettings.LandBranch = LandBranchSetting.Most;
                _mapSettings.LandLoop = LandLoopSetting.Default;
                break;

            case 5: // 가지 : 절대, 순환 : 항상
                _mapSettings.LandBranch = LandBranchSetting.Never;
                _mapSettings.LandLoop = LandLoopSetting.Always;
                break;

            case 6: // 가지 : 최대, 순환 : 절대
                _mapSettings.LandBranch = LandBranchSetting.Most;
                _mapSettings.LandLoop = LandLoopSetting.Never;
                break;

            default: // 가지 : 최대, 순환 : 절대
                _mapSettings.LandBranch = LandBranchSetting.Most;
                _mapSettings.LandLoop = LandLoopSetting.Never;
                break;
        }
        _mapGenerator.GenerateMapWithSettings(_mapSettings);
    }
}