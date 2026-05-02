using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// MapGenerator 사용 예제
/// </summary>
public class WorldGenManager : MonoBehaviour
{
    [SerializeField] private WorldGraphDirector _worldGraphDirector;
    [SerializeField] private WorldChunkDirector _worldChunkDirector;
    [SerializeField] private WorldRenderDirector _worldRenderDirector;
    [SerializeField] private WorldSettings _worldSettings;

    [SerializeField] private GameObject _demoPlayer;

    [SerializeField] private int _lastPressedDifficulty = -1; // 마지막으로 누른 키 번호
    [SerializeField] private int _currentSeed = 0;            // 현재 유지 중인 시드값

    private string _worldSettingLabel = "TestWorldSettings";

    private CancellationTokenSource _cts;
    private bool _isWorldSettingsLoaded;

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
        if (_worldGraphDirector == null)
            _worldGraphDirector = Extensions.GetOrAddComponent<WorldGraphDirector>(this.gameObject);
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
            _isWorldSettingsLoaded = (_worldSettings != null);
            Debug.Log("StoryWorldSettings 로드 완료");
        }
        catch (System.OperationCanceledException)
        {
            _isWorldSettingsLoaded = false;
            Debug.Log("StoryWorldSettings 로드 취소됨");
        }
        catch (System.Exception e)
        {
            _isWorldSettingsLoaded = false;
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

        if (!_isWorldSettingsLoaded || _worldSettings == null)
        {
            Debug.LogWarning("WorldSettings가 아직 로드되지 않았습니다.");
            return;
        }

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
            await _worldGraphDirector.GenerateWorldLogicWithSettings(_worldSettings, _cts.Token);
            Debug.Log($"월드 그래프 생성 완료. 시드: {_worldSettings.WorldSeed}");

            var graphData = _worldGraphDirector.GetWorldGraphData();
            var logicData = _worldGraphDirector.GetWorldLogicData();

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
            int startingX = Mathf.RoundToInt(mapSize.x * 0.5f); // 기본값: 맵 중앙
            int startingZ = Mathf.RoundToInt(mapSize.y * 0.5f); // 기본값: 맵 중앙

            bool foundStartRegion = false;

            // 1. 그래프 노드들을 뒤져서 StartRegion을 찾습니다.
            foreach (var node in graphData.Nodes)
            {
                // (주의) RegionData에 정의된 타입이나 이름 프로퍼티에 맞게 수정해주세요!
                // 예: node.RegionData.RegionType == RegionType.Start
                if (node.RegionData.RegionName.Contains("Start"))
                {
                    if (node.OwnedTiles.Count > 0)
                    {
                        // 2. StartRegion에 속한 타일들의 평균 위치(무게중심)를 계산하여 스폰 지점으로 삼습니다.
                        long sumX = 0;
                        long sumY = 0;
                        foreach (Vector2Int tile in node.OwnedTiles)
                        {
                            sumX += tile.x;
                            sumY += tile.y;
                        }
                        startingX = (int)(sumX / node.OwnedTiles.Count);
                        startingZ = (int)(sumY / node.OwnedTiles.Count);
                        foundStartRegion = true;
                        break;
                    }
                }
            }

            if (!foundStartRegion)
            {
                Debug.LogWarning("🚨 StartRegion을 찾지 못해 맵 중앙 좌표를 사용합니다.");
            }
            else
            {
                Debug.Log($"✅ StartRegion 탐색 성공! 스폰 타일 좌표: ({startingX}, {startingZ})");
            }

            // 찾아낸 타일 좌표를 바탕으로 청크 좌표를 계산합니다.
            Vector2Int spawnChunkCoord = logicData.GetChunkCoord(startingX, startingZ);

            // ==========================================================
            // 4단계: 플레이어 탐색 및 StartRegion으로 
            // ==========================================================
            if (_demoPlayer == null)
            {
                foreach (GameObject rootObj in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (rootObj.CompareTag("Player"))
                    {
                        _demoPlayer = rootObj;
                        break;
                    }
                }
            }

            if (_demoPlayer != null)
            {
                // 청크를 부르기 전에 플레이어부터 지정된 위치로 옮깁니다.
                _demoPlayer.transform.position = new Vector3(startingX, 10f, startingZ);
                _demoPlayer.SetActive(true);

                // ChunkDirector에게 타겟 갱신
                _worldChunkDirector.SetTarget(_demoPlayer.transform);
                Debug.Log("🎯 플레이어 StartRegion 자동 탐색 및 안전 스폰 완료!");
            }
            else
            {
                Debug.LogWarning("🚨 하이어라키에 'Player' 태그를 가진 오브젝트가 없습니다!");
            }

            // ==========================================================
            // 5단계: 청크 디렉터 초기화 및 스폰 지역 확정 렌더링 대기
            // ==========================================================
            _worldChunkDirector.Initialize(logicData, _worldRenderDirector);

            // 이제 플레이어가 제자리에 있으니, 해당 위치의 청크를 로딩합니다.
            await _worldChunkDirector.LoadInitialSpawnAreaAsync(spawnChunkCoord);

        }
        catch (System.OperationCanceledException)
        {
            Debug.Log("맵 생성 취소됨");
        }
    }
}