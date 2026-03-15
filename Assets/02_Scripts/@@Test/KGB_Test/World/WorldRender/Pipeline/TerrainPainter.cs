using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

public class TerrainPainter
{
    // 로드된 키 목록을 저장해두었다가 나중에 Release 할 때 사용
    private List<string> _loadedKeys = new();

    // Key: RoomData의 TopTileKey, Value: terrainData.terrainLayers 배열의 인덱스
    private Dictionary<string, int> _layerIndexMap = new();

    public async UniTask PaintTerrainAsync(
        TerrainData terrainData,
        WorldLogicData logicData,
        WorldGraphData graphData,
        WorldSettings settings,
        CancellationToken ct)
    {
        Debug.Log("🎨 [TerrainPainter] 지형 페인팅 에셋 로드 시작...");

        // 1. [데이터 수집] 맵 전체에서 사용될 텍스처 키(Key) 중복 없이 수집
        HashSet<string> uniqueTileKeys = new HashSet<string>();
        foreach (var node in graphData.Nodes)
        {
            if (node.RoomData != null && !string.IsNullOrEmpty(node.RoomData.TopTileKey))
            {
                uniqueTileKeys.Add(node.RoomData.TopTileKey);
            }
        }

        _loadedKeys = uniqueTileKeys.ToList();

        // 2. [비동기 병렬 로드] 프레임워크 Extension을 사용하여 TerrainLayer 로드
        List<UniTask<TerrainLayer>> loadTasks = new List<UniTask<TerrainLayer>>();
        foreach (var key in _loadedKeys)
        {
            loadTasks.Add(Extensions.LoadAssetAsync<TerrainLayer>(key, AssetCacheType.NonRequired, ct));
        }

        // 모든 텍스처가 로드될 때까지 대기
        TerrainLayer[] loadedLayers = await UniTask.WhenAll(loadTasks);

        // 3. [인덱스 매핑] TerrainData에 레이어 등록 및 딕셔너리에 인덱스 캐싱
        terrainData.terrainLayers = loadedLayers;
        _layerIndexMap.Clear();

        for (int i = 0; i < loadedLayers.Length; i++)
        {
            _layerIndexMap[_loadedKeys[i]] = i;
        }

        Debug.Log($"🎨 [TerrainPainter] {loadedLayers.Length}개의 텍스처 로드 완료. 페인팅 시작!");

        // 4. [페인팅 준비] 알파맵(Splatmap) 배열 생성
        int alphaWidth = terrainData.alphamapWidth;
        int alphaHeight = terrainData.alphamapHeight;
        int numLayers = loadedLayers.Length;

        // 아무 텍스처도 로드되지 않았다면 페인팅 취소
        if (numLayers == 0) return;

        float[,,] alphamaps = new float[alphaWidth, alphaHeight, numLayers];

        // 5. [알파맵 칠하기] TerritoryWorld 데이터를 기반으로 타일 칠하기
        for (int y = 0; y < alphaHeight; y++)
        {
            for (int x = 0; x < alphaWidth; x++)
            {
                // 알파맵 좌표를 논리 그리드(TerritoryWorld) 좌표로 변환
                float normX = (float)x / alphaWidth;
                float normY = (float)y / alphaHeight;
                int gridX = Mathf.Clamp(Mathf.RoundToInt(normX * logicData.TerrainSize.x), 0, logicData.TerrainSize.x - 1);
                int gridY = Mathf.Clamp(Mathf.RoundToInt(normY * logicData.TerrainSize.y), 0, logicData.TerrainSize.y - 1);

                // 현재 좌표가 속한 구역(Node)의 인덱스 가져오기
                int nodeIndex = logicData.TerritoryWorld[gridX, gridY];
                if (nodeIndex >= 0 && nodeIndex < graphData.Nodes.Count)
                {
                    var node = graphData.Nodes[nodeIndex];
                    string targetKey = node.RoomData?.TopTileKey;

                    // 매핑된 인덱스를 찾아 가중치 1.0(100%) 부여
                    if (!string.IsNullOrEmpty(targetKey) && _layerIndexMap.TryGetValue(targetKey, out int layerIndex))
                    {
                        alphamaps[y, x, layerIndex] = 1.0f;
                    }
                    else
                    {
                        // 해당하는 키가 없거나 방 데이터가 없으면 기본값(0번 레이어)으로 칠함
                        alphamaps[y, x, 0] = 1.0f;
                    }
                }
                else
                {
                    // 영토가 할당되지 않은 곳(바다/절벽 등)은 0번 레이어
                    alphamaps[y, x, 0] = 1.0f;
                }
            }

            // 프레임 드랍(스파이크)을 방지하기 위해 100줄마다 한 프레임 쉼
            if (y % 100 == 0) await UniTask.Yield(ct);
        }

        // ---------------------------------------------------------
        // 5.5 [알파맵 스무딩] 구역 경계선 텍스처 자연스럽게 섞기 (Splatmap Blur)
        // ---------------------------------------------------------
        int blurRadius = 1; // 섞이는 반경 (값이 클수록 텍스처 경계가 넓고 부드럽게 그라데이션 됩니다)
        float[,,] blurredAlphamaps = new float[alphaWidth, alphaHeight, numLayers];

        for (int y = 0; y < alphaHeight; y++)
        {
            for (int x = 0; x < alphaWidth; x++)
            {
                float[] sum = new float[numLayers];
                int count = 0;

                // 주변 픽셀들의 가중치 합산 (Box Blur 기법)
                for (int dy = -blurRadius; dy <= blurRadius; dy++)
                {
                    for (int dx = -blurRadius; dx <= blurRadius; dx++)
                    {
                        int nx = Mathf.Clamp(x + dx, 0, alphaWidth - 1);
                        int ny = Mathf.Clamp(y + dy, 0, alphaHeight - 1);

                        for (int l = 0; l < numLayers; l++)
                        {
                            sum[l] += alphamaps[ny, nx, l];
                        }
                        count++;
                    }
                }

                // 평균값을 내고, 모든 레이어의 가중치 총합이 정확히 1.0이 되도록 정규화(Normalize)
                float totalWeight = 0f;
                for (int l = 0; l < numLayers; l++)
                {
                    float avg = sum[l] / count;
                    blurredAlphamaps[y, x, l] = avg;
                    totalWeight += avg;
                }

                if (totalWeight > 0f)
                {
                    for (int l = 0; l < numLayers; l++)
                    {
                        blurredAlphamaps[y, x, l] /= totalWeight;
                    }
                }
            }
            if (y % 100 == 0) await UniTask.Yield(ct);
        }

        // 완성된 원본 알파맵을 블러 처리된 부드러운 알파맵으로 싹 교체
        alphamaps = blurredAlphamaps;

        // 6. [적용] 터레인에 최종 알파맵 덮어씌우기
        terrainData.SetAlphamaps(0, 0, alphamaps);
        Debug.Log("🎨 [TerrainPainter] 지형 페인팅 완료!");
    }

    /// <summary>
    /// 월드가 파괴될 때 로드했던 TerrainLayer 메모리를 해제합니다.
    /// WorldRenderDirector의 ClearRenderedWorld() 등에서 호출해 주세요.
    /// </summary>
    public void ReleasePaintedLayers()
    {
        foreach (var key in _loadedKeys)
        {
            // 작성자님의 프레임워크 익스텐션 해제 메서드 호출
            Extensions.Release(key);
        }
        _loadedKeys.Clear();
        _layerIndexMap.Clear();
    }
}