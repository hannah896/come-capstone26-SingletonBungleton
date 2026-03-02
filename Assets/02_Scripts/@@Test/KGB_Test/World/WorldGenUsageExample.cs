using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// MapGenerator 사용 예제
/// </summary>
public class WorldGenUsageExample : MonoBehaviour
{
    [SerializeField] private WorldLogicDirector _worldLogicDirector;
    [SerializeField] private WorldRenderDirector _worldRenderDirector;
    [SerializeField] private WorldSettings _worldSettings;

    [SerializeField] private int _lastPressedDifficulty = -1; // 마지막으로 누른 키 번호
    [SerializeField] private int _currentSeed = 0;            // 현재 유지 중인 시드값

    private string _worldSettingLabel = "TestWorldSettings";

    private CancellationTokenSource _cts;


    void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }


    async void Start()
    {
        await UniTask.WaitUntil(() => Main.Instance != null);
        _cts = new CancellationTokenSource();

        if (_worldLogicDirector == null)
            _worldLogicDirector = Extensions.GetOrAddComponent<WorldLogicDirector>(this.gameObject);
        if (_worldRenderDirector == null)
            _worldRenderDirector = Extensions.GetOrAddComponent<WorldRenderDirector>(this.gameObject);

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
            GenerateWorldByKey(1).Forget();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            GenerateWorldByKey(2).Forget();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            GenerateWorldByKey(3).Forget();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            GenerateWorldByKey(4).Forget();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            GenerateWorldByKey(5).Forget();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            GenerateWorldByKey(6).Forget();
        }

    }


    /// <summary>
    /// 키다운 맵 생성 예제
    /// </summary>
    public async UniTask GenerateWorldByKey(int difficulty)
    {

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource(); // 새 스위치 발급
        try
        {
            
            
            if (_lastPressedDifficulty == difficulty)
            {
                // 같은 키를 연달아 누름 -> 기존 시드 유지
                Debug.Log($"동일한 키({difficulty}) 입력됨. 이전 시드({_currentSeed}) 재사용.");
            }
            else
            {
                // 다른 키를 누름 -> 새로운 무작위 시드 발급
                // (System.Random의 시드로 사용하기 위해 int 범위 내에서 난수 생성)
                _currentSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                _lastPressedDifficulty = difficulty;
                Debug.Log($"새로운 키({difficulty}) 입력됨. 새 무작위 시드({_currentSeed}) 발급.");
            }

            _worldSettings.WorldSeed = _currentSeed;
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

            await _worldLogicDirector.GenerateWorldLogicWithSettings(_worldSettings, _cts.Token);

            var graphData = _worldLogicDirector.GetWorldGraphData();
            var logicData = _worldLogicDirector.GetWorldLogicData();
            var disposeDatas = _worldLogicDirector.GetWorldDisposeDatas();

            await _worldRenderDirector.RenderWorldAsync(logicData, graphData, disposeDatas, _worldSettings, _cts.Token);
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log("맵 생성 취소됨");
        }
    }
}