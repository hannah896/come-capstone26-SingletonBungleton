using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// MapGenerator 사용 예제
/// </summary>
public class WorldGenUsageExample : MonoBehaviour
{
    [SerializeField] private WorldGraphDirector _worldLogicDirector;
    [SerializeField] private WorldChunkDirector _worldChunkDirector;
    [SerializeField] private WorldRenderDirector _worldRenderDirector;
    [SerializeField] private WorldSettings _worldSettings;

    [SerializeField] private GameObject _demoPlayer;

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

        // 3개의 디렉터 모두 컴포넌트 유무 확인 및 부착
        if (_worldLogicDirector == null)
            _worldLogicDirector = Extensions.GetOrAddComponent<WorldGraphDirector>(this.gameObject);
        if (_worldRenderDirector == null)
            _worldRenderDirector = Extensions.GetOrAddComponent<WorldRenderDirector>(this.gameObject);
        if (_worldChunkDirector == null)
            _worldChunkDirector = Extensions.GetOrAddComponent<WorldChunkDirector>(this.gameObject);

        await LoadWorldSettingsAsync(_cts.Token);
        
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
        if (Input.GetKeyDown(KeyCode.Alpha1)) GenerateWorldByKey(1).Forget();
        else if (Input.GetKeyDown(KeyCode.Alpha2)) GenerateWorldByKey(2).Forget();
        else if (Input.GetKeyDown(KeyCode.Alpha3)) GenerateWorldByKey(3).Forget();
        else if (Input.GetKeyDown(KeyCode.Alpha4)) GenerateWorldByKey(4).Forget();
        else if (Input.GetKeyDown(KeyCode.Alpha5)) GenerateWorldByKey(5).Forget();
        else if (Input.GetKeyDown(KeyCode.Alpha6)) GenerateWorldByKey(6).Forget();
    }

    /// <summary>
    /// 키다운 맵 생성 예제
    /// </summary>
    public async UniTask GenerateWorldByKey(int difficulty)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            if (_lastPressedDifficulty == difficulty)
            {
                Debug.Log($"동일한 키({difficulty}) 입력됨. 이전 시드({_currentSeed}) 재사용.");
            }
            else
            {
                _currentSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                _lastPressedDifficulty = difficulty;
                Debug.Log($"새로운 키({difficulty}) 입력됨. 새 무작위 시드({_currentSeed}) 발급.");
            }

            _worldSettings.WorldSeed = _currentSeed;
            _worldSettings.WorldSize = WorldSize.Large;

            switch (difficulty)
            {
                case 1:
                    _worldSettings.WorldBranch = WorldBranchSetting.Most;
                    _worldSettings.WorldLoop = WorldLoopSetting.Always;
                    break;
                case 2:
                    _worldSettings.WorldBranch = WorldBranchSetting.Never;
                    _worldSettings.WorldLoop = WorldLoopSetting.Never;
                    break;
                case 3:
                    _worldSettings.WorldBranch = WorldBranchSetting.Default;
                    _worldSettings.WorldLoop = WorldLoopSetting.Always;
                    break;
                case 4:
                    _worldSettings.WorldBranch = WorldBranchSetting.Most;
                    _worldSettings.WorldLoop = WorldLoopSetting.Default;
                    break;
                case 5:
                    _worldSettings.WorldBranch = WorldBranchSetting.Never;
                    _worldSettings.WorldLoop = WorldLoopSetting.Always;
                    break;
                case 6:
                default:
                    _worldSettings.WorldBranch = WorldBranchSetting.Most;
                    _worldSettings.WorldLoop = WorldLoopSetting.Never;
                    break;
            }

            // ==========================================================
            // 1단계: 논리 데이터 생성 및 청크 분할
            // ==========================================================
            await _worldLogicDirector.GenerateWorldLogicWithSettings(_worldSettings, _cts.Token);
            Debug.Log($"월드 그래프 생성 완료. 시드: {_worldSettings.WorldSeed}");

            var graphData = _worldLogicDirector.GetWorldGraphData();
            var logicData = _worldLogicDirector.GetWorldLogicData();

            // ==========================================================
            // 2단계: 렌더 디렉터 초기화 및 에셋 로드 
            // ==========================================================
            // 이전 맵의 렌더링된 청크를 싹 밀어줍니다.
            _worldRenderDirector.ClearAllChunks();
            await _worldRenderDirector.InitializeAsync(_worldSettings, graphData, _cts.Token);

            // ==========================================================
            // 3단계: 플레이어 스폰 좌표 계산
            // ==========================================================
            Vector2Int mapSize = _worldSettings.GetWorldSize();
            int centerX = Mathf.RoundToInt(mapSize.x * 0.5f);
            int centerZ = Mathf.RoundToInt(mapSize.y * 0.5f);

            // 플레이어가 서 있을 곳의 청크 좌표를 알아냅니다.
            Vector2Int spawnChunkCoord = logicData.GetChunkCoord(centerX, centerZ);

            // ==========================================================
            // 4단계: 청크 디렉터 초기화 및 스폰 지역 확정 렌더링 대기
            // ==========================================================
            _worldChunkDirector.Initialize(logicData, _worldRenderDirector);

            await _worldChunkDirector.LoadInitialSpawnAreaAsync(spawnChunkCoord);

            // ==========================================================
            // 5단계: 비활성화된 플레이어 자동 탐색 및 소환
            // ==========================================================

            // 5-1. 플레이어를 아직 못 찾았다면 씬에서 찾아옵니다.
            if (_demoPlayer == null)
            {
                // 유니티의 SceneManager를 이용해 하이어라키 최상위(Root)에 있는 오브젝트들을 모두 뒤집니다.
                // (이 방식을 쓰면 비활성화(SetActive(false)) 되어 있는 객체도 찾아낼 수 있습니다!)
                foreach (GameObject rootObj in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (rootObj.CompareTag("Player"))
                    {
                        _demoPlayer = rootObj;
                        break;
                    }
                }
            }

            // 5-2. 찾아낸 플레이어를 스폰 위치로 옮기고 켭니다.
            if (_demoPlayer != null)
            {
                _demoPlayer.transform.position = new Vector3(centerX, 10f, centerZ);
                _demoPlayer.SetActive(true);
                Debug.Log("🎯 플레이어 자동 탐색 및 안전 스폰 완료!");
            }
            else
            {
                // 혹시라도 태그 설정을 깜빡하셨을 때를 대비한 경고
                Debug.LogWarning("🚨 하이어라키에 'Player' 태그를 가진 오브젝트가 없습니다!");
            }

        }
        catch (System.OperationCanceledException)
        {
            Debug.Log("맵 생성 취소됨");
        }
    }
}