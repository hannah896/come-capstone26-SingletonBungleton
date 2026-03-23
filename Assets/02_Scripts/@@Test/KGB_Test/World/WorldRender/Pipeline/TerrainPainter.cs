using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

public class TerrainPainter
{
    private List<string> _loadedKeys = new();
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
            if (node.RoomData != null)
            {
                if (!string.IsNullOrEmpty(node.RoomData.TopTileKey))
                    uniqueTileKeys.Add(node.RoomData.TopTileKey);
                
                // ★ 지면과 절벽을 구분하기 위해 CliffTileKey도 수집 목록에 추가
                if (!string.IsNullOrEmpty(node.RoomData.CliffTileKey))  
                    uniqueTileKeys.Add(node.RoomData.CliffTileKey);
            }
        }

        _loadedKeys = uniqueTileKeys.ToList();

        // 2. [비동기 병렬 로드]
        List<UniTask<TerrainLayer>> loadTasks = new List<UniTask<TerrainLayer>>();
        foreach (var key in _loadedKeys)
        {
            loadTasks.Add(Extensions.LoadAssetAsync<TerrainLayer>(key, AssetCacheType.NonRequired, ct));
        }

        TerrainLayer[] loadedLayers = await UniTask.WhenAll(loadTasks);

        // 3. [인덱스 매핑]
        terrainData.terrainLayers = loadedLayers;
        _layerIndexMap.Clear();

        for (int i = 0; i < loadedLayers.Length; i++)
        {
            _layerIndexMap[_loadedKeys[i]] = i;
        }

        Debug.Log($"🎨 [TerrainPainter] {loadedLayers.Length}개의 텍스처 로드 완료. 페인팅 시작!");

        // 4. [페인팅 준비]
        int alphaWidth = terrainData.alphamapWidth;
        int alphaHeight = terrainData.alphamapHeight;
        int numLayers = loadedLayers.Length;

        if (numLayers == 0) return;

        float[,,] alphamaps = new float[alphaWidth, alphaHeight, numLayers];

        // 5. [알파맵 칠하기] 
        for (int y = 0; y < alphaHeight; y++)
        {
            for (int x = 0; x < alphaWidth; x++)
            {
                // 정규화된 좌표 (0.0 ~ 1.0)
                float normX = (float)x / (alphaWidth - 1);
                float normY = (float)y / (alphaHeight - 1);
                
                int gridX = Mathf.Clamp(Mathf.RoundToInt(normX * logicData.TerrainSize.x), 0, logicData.TerrainSize.x - 1);
                int gridY = Mathf.Clamp(Mathf.RoundToInt(normY * logicData.TerrainSize.y), 0, logicData.TerrainSize.y - 1);

                // ==========================================================
                // ★ 핵심: 경사도(Steepness) 계산 및 텍스처 분배비율 설정
                // ==========================================================
                // ※ TerrainData가 Height를 먼저 적용했다고 가정합니다.
                float steepness = terrainData.GetSteepness(normX, normY);

                // 경사각 기준점 (기획에 맞춰 수정해보세요)
                float minCliffAngle = 25f; // 이 각도부터 절벽 텍스처가 섞이기 시작
                float maxCliffAngle = 45f; // 이 각도 이상이면 100% 절벽 텍스처

                // 지형이 얼마나 절벽인지 가중치(0% ~ 100%) 부여
                float cliffWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(minCliffAngle, maxCliffAngle, steepness));
                float topWeight = 1f - cliffWeight;

                int nodeIndex = logicData.TerritoryWorld[gridX, gridY];
                if (nodeIndex >= 0 && nodeIndex < graphData.Nodes.Count)
                {
                    var node = graphData.Nodes[nodeIndex];
                    string topKey = node.RoomData?.TopTileKey;
                    string cliffKey = node.RoomData?.CliffTileKey;

                    int topIndex = 0;
                    int cliffIndex = 0;

                    if (!string.IsNullOrEmpty(topKey) && _layerIndexMap.TryGetValue(topKey, out int tIdx)) 
                        topIndex = tIdx;
                    
                    if (!string.IsNullOrEmpty(cliffKey) && _layerIndexMap.TryGetValue(cliffKey, out int cIdx)) 
                        cliffIndex = cIdx;
                    else 
                        cliffIndex = topIndex; // 절벽 텍스처가 비어있다면 그냥 땅 텍스처로 대체

                    // 최종적으로 각도에 따른 비율만큼 텍스처 칠해주기
                    alphamaps[y, x, topIndex] += topWeight;
                    alphamaps[y, x, cliffIndex] += cliffWeight;
                }
                else
                {
                    // 영토가 할당되지 않은 바다나 외곽
                    alphamaps[y, x, 0] = 1.0f;
                }
            }

            if (y % 100 == 0) await UniTask.Yield(ct);
        }

        // ---------------------------------------------------------
        // 5.5 [알파맵 스무딩] 
        // ---------------------------------------------------------
        int blurRadius = 1; 
        float[,,] blurredAlphamaps = new float[alphaWidth, alphaHeight, numLayers];

        for (int y = 0; y < alphaHeight; y++)
        {
            for (int x = 0; x < alphaWidth; x++)
            {
                float[] sum = new float[numLayers];
                int count = 0;

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

        alphamaps = blurredAlphamaps;

        // 6. [적용]
        terrainData.SetAlphamaps(0, 0, alphamaps);
        Debug.Log("🎨 [TerrainPainter] 지형 페인팅 완료!");
    }

    public void ReleasePaintedLayers()
    {
        foreach (var key in _loadedKeys)
        {
            Extensions.Release(key);
        }
        _loadedKeys.Clear();
        _layerIndexMap.Clear();
    }
}