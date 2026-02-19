using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// MapGenerator 사용 예제
/// </summary>
public class StoryWorldGenUsageExample : MonoBehaviour
{
    [SerializeField] private WorldGenerator _worldGenerator;
    [SerializeField] private WorldSettings _worldSettings;

    private string _worldSettingLabel = "TestWorldSettings";

    private CancellationTokenSource _cts;

    
    async void Start()
    {
        await UniTask.WaitUntil(() => Main.Instance != null);
        _cts = new CancellationTokenSource();

        if (_worldGenerator == null)
            _worldGenerator = Extensions.GetOrAddComponent<WorldGenerator>(this.gameObject);

        await LoadWorldSettingsAsync(_cts.Token);
        
        // 예제: 다양한 맵 설정으로 생성
        // GenerateExampleMaps();
    }

    #region Load StoryWorldSettings
    private async UniTask LoadWorldSettingsAsync(CancellationToken ct)
    {
        try
        {
            _worldSettings = await Extensions.LoadAssetAsync<WorldSettings>(
                _worldSettingLabel,
                AssetCacheType.Required,
                ct
            );
            Debug.Log("StoryWorldSettings 로드 완료");
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log("StoryWorldSettings 로드 취소됨");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"StoryWorldSettings 로드 실패: {e.Message}");
        }
    }
    #endregion

    void Update()
    {
        // 테스트: 키 입력으로 다른 설정의 맵 생성
        
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            GenerateWorldByKey(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            GenerateWorldByKey(2);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            GenerateWorldByKey(3);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            GenerateWorldByKey(4);
        }else if(Input.GetKeyDown(KeyCode.Alpha5))
        {
            GenerateWorldByKey(5);
        }else if(Input.GetKeyDown(KeyCode.Alpha6))
        {
            GenerateWorldByKey(6);
        }
        
    }


    /// <summary>
    /// 키다운 맵 생성 예제
    /// </summary>
    public void GenerateWorldByKey(int difficulty)
    {
        _worldSettings.WorldSize = WorldSize.Huge;
        switch (difficulty)
        {
            case 1: // 가지 : 최대, 순환 : 항상
                Debug.Log("가지 : 최대, 순환 : 항상");
                _worldSettings.WorldBranch = WorldBranchSetting.Most;
                _worldSettings.WorldLoop = WorldLoopSetting.Always;
                break;

            case 2: // 가지 : 절대, 순환 : 절대
                Debug.Log("가지 : 절대, 순환 : 절대");
                _worldSettings.WorldBranch = WorldBranchSetting.Never;
                _worldSettings.WorldLoop = WorldLoopSetting.Never;
                break;

            case 3: // 가지 : 기본, 순환 : 항상
                Debug.Log("가지 : 기본, 순환 : 항상");
                _worldSettings.WorldBranch = WorldBranchSetting.Default;
                _worldSettings.WorldLoop = WorldLoopSetting.Always;
                break;

            case 4: // 가지 최대, 순환 : 기본
                Debug.Log("가지 : 최대, 순환 : 기본");
                _worldSettings.WorldBranch = WorldBranchSetting.Most;
                _worldSettings.WorldLoop = WorldLoopSetting.Default;
                break;

            case 5: // 가지 : 절대, 순환 : 항상
                Debug.Log("가지 : 절대, 순환 : 항상");
                _worldSettings.WorldBranch = WorldBranchSetting.Never;
                _worldSettings.WorldLoop = WorldLoopSetting.Always;
                break;

            case 6: // 가지 : 최대, 순환 : 절대
                Debug.Log("가지 : 최대, 순환 : 절대");
                _worldSettings.WorldBranch = WorldBranchSetting.Most;
                _worldSettings.WorldLoop = WorldLoopSetting.Never;
                break;

            default: // 가지 : 최대, 순환 : 절대
                _worldSettings.WorldBranch = WorldBranchSetting.Most;
                _worldSettings.WorldLoop = WorldLoopSetting.Never;
                break;
        }
        _worldGenerator.GenerateWorldWithSettings(_worldSettings);
    }
}