using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

public class TerrainPainter
{
    // 필요에 따라 블러 강도 조절
    private const int BLUR_RADIUS = 2;

    /// <summary>
    /// 단일 청크 데이터를 받아 알파맵(스플랫맵)을 칠합니다.
    /// 에셋 로딩은 이미 TerrainLayerLoader가 끝냄
    /// </summary>
    public async UniTask PaintChunkTerrainAsync(
        TerrainData terrainData,
        ChunkData chunk,
        WorldGraphData graphData,
        TerrainLayerPalette palette, // ★ 물감 통(Loader)을 매개변수로 받음
        CancellationToken ct)
    {
        if (palette == null || palette.Layers == null || palette.Layers.Length == 0) return;

        // 1. 캔버스에 물감 세팅
        terrainData.terrainLayers = palette.Layers;

        int alphaWidth = terrainData.alphamapWidth;
        int alphaHeight = terrainData.alphamapHeight;
        int numLayers = palette.Layers.Length;
        int chunkSize = chunk.TerritoryMap.GetLength(0);

        float[,,] alphamaps = new float[alphaWidth, alphaHeight, numLayers];

        // =========================================================
        // 2. 1차 페인팅 로직 (경사도 및 바이옴 기반 칠하기)
        // =========================================================
        for (int y = 0; y < alphaHeight; y++)
        {
            for (int x = 0; x < alphaWidth; x++)
            {
                int localX = Mathf.Clamp(Mathf.RoundToInt((float)x / (alphaWidth - 1) * (chunkSize - 1)), 0, chunkSize - 1);
                int localY = Mathf.Clamp(Mathf.RoundToInt((float)y / (alphaHeight - 1) * (chunkSize - 1)), 0, chunkSize - 1);

                float normX = (float)x / (alphaWidth - 1);
                float normY = (float)y / (alphaHeight - 1);
                float steepness = terrainData.GetSteepness(normX, normY);

                float minCliffAngle = 25f;
                float maxCliffAngle = 45f;
                float cliffWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(minCliffAngle, maxCliffAngle, steepness));
                float topWeight = 1f - cliffWeight;

                int nodeIndex = chunk.TerritoryMap[localX, localY];

                if (nodeIndex >= 0 && nodeIndex < graphData.Nodes.Count)
                {
                    var node = graphData.Nodes[nodeIndex];
                    string topKey = node.BiomeData?.TopKey;
                    string cliffKey = node.BiomeData?.CliffKey;

                    int topIndex = 0;
                    int cliffIndex = 0;

                    // ★ 로더가 가지고 있는 인덱스 맵을 참조
                    if (!string.IsNullOrEmpty(topKey) && palette.IndexMap.TryGetValue(topKey, out int tIdx))
                        topIndex = tIdx;
                    if (!string.IsNullOrEmpty(cliffKey) && palette.IndexMap.TryGetValue(cliffKey, out int cIdx))
                        cliffIndex = cIdx;
                    else
                        cliffIndex = topIndex;

                    alphamaps[y, x, topIndex] += topWeight;
                    alphamaps[y, x, cliffIndex] += cliffWeight;
                }
                else
                {
                    alphamaps[y, x, 0] = 1.0f; // 바다 및 경계선
                }
            }
            if (y % 16 == 0) await UniTask.Yield(ct);
        }

        // =========================================================
        // 3. 2차 블러(스무딩) 처리 로직
        // =========================================================
        float[,,] blurredAlphamaps = new float[alphaWidth, alphaHeight, numLayers];

        float[] sum = new float[numLayers];
        for (int y = 0; y < alphaHeight; y++)
        {
            for (int x = 0; x < alphaWidth; x++)
            {
                System.Array.Clear(sum, 0, numLayers);
                int count = 0;

                for (int dy = -BLUR_RADIUS; dy <= BLUR_RADIUS; dy++)
                {
                    for (int dx = -BLUR_RADIUS; dx <= BLUR_RADIUS; dx++)
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
            // 프레임 드랍 방지
            if (y % 16 == 0) await UniTask.Yield(ct);
        }

        alphamaps = blurredAlphamaps;

        // 4. [적용]
        terrainData.SetAlphamaps(0, 0, alphamaps);
        Debug.Log($"🎨 [TerrainPainter] 청크 페인팅 완료! ({chunk.ChunkCoord.x}, {chunk.ChunkCoord.y})");
    }
}