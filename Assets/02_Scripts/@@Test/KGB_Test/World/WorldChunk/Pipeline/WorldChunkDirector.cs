using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class WorldChunkDirector : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int _loadDistance = 2; // 플레이어 주변 몇 개의 청크를 로드할지

    private IChunkRenderer _renderDirector;

    [Header("References")]
    private WorldLogicData _logicData;
    private Vector2Int _currentChunkCoord = new Vector2Int(-999, -999);

    // 현재 활성화된 청크들을 관리 (좌표, 청크데이터)
    private Dictionary<Vector2Int, ChunkData> _activeChunks = new();

    public void Initialize(WorldLogicData logicData, IChunkRenderer renderer)
    {
        _renderDirector = renderer;
        _logicData = logicData;
        _activeChunks.Clear();


        Main.Loop.OnUpdate += OnChunkUpdate;
    }

    public async UniTask LoadInitialSpawnAreaAsync(Vector2Int spawnCoord)
    {
        _currentChunkCoord = spawnCoord;
        await UpdateVisibleChunks(spawnCoord);
        Debug.Log("✅ [WorldChunkDirector] 스폰 지역 청크 로딩 100% 완료!");
    }

    private void OnChunkUpdate(float deltaTime)
    {
        // 씬에 있는 메인 카메라를 타겟으로 삼음
        if (_logicData == null || Camera.main == null) return;

        // 카메라의 위치를 기준으로 청크 좌표 계산
        Vector2Int playerCoord = _logicData.GetChunkCoord(
            Mathf.RoundToInt(Camera.main.transform.position.x),
            Mathf.RoundToInt(Camera.main.transform.position.z)
        );

        if (playerCoord != _currentChunkCoord)
        {
            _currentChunkCoord = playerCoord;
            UpdateVisibleChunks(playerCoord).Forget();
        }
    }

    private async UniTask UpdateVisibleChunks(Vector2Int centerCoord)
    {
        HashSet<Vector2Int> requiredCoords = new HashSet<Vector2Int>();

        // 3. 로드 범위 내의 청크 좌표 수집
        for (int x = -_loadDistance; x <= _loadDistance; x++)
        {
            for (int y = -_loadDistance; y <= _loadDistance; y++)
            {
                requiredCoords.Add(centerCoord + new Vector2Int(x, y));
            }
        }   

        // 4. 언로드(Unload): 범위 밖으로 나간 청크 제거
        List<Vector2Int> toRemove = new List<Vector2Int>();
        foreach (var coord in _activeChunks.Keys)
        {
            if (!requiredCoords.Contains(coord))
            {
                toRemove.Add(coord);
            }
        }

        foreach (var coord in toRemove)
        {
            _activeChunks.Remove(coord);
            _renderDirector.UnloadChunk(coord); // 렌더러에게 파괴 요청
        }

        List<UniTask> renderTasks = new List<UniTask>();

        // 5. 로드(Load): 새로 범위에 들어온 청크 생성
        foreach (var coord in requiredCoords)
        {
            if (!_activeChunks.ContainsKey(coord))
            {
                // WorldLogicData의 딕셔너리에서 데이터 추출
                ChunkData chunk = _logicData.GetOrCreateChunk(coord);
                if (chunk != null)
                {
                    _activeChunks.Add(coord, chunk);
                    // 렌더러에게 생성을 요청하고, 그 작업(Task)을 리스트에 담음
                    renderTasks.Add(_renderDirector.AddChunkRenderAsync(chunk));
                }
            }
        }

        await UniTask.WhenAll(renderTasks);
    }

    private void OnDestroy()
    {
        if (Main.Instance != null && Main.Loop != null)
        {
            Main.Loop.OnUpdate -= OnChunkUpdate;
        }
    }
}