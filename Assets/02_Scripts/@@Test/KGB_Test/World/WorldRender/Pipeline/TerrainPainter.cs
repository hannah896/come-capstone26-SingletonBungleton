using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

public class TerrainPainter
{
    private const int BLUR_RADIUS = 2;

    #region Job Structures
    [BurstCompile]
    private struct PaintBaseJob : IJobParallelFor
    {
        public int AlphaWidth;
        public int AlphaHeight;
        public int ChunkSize;
        public int NumLayers;

        public float MinCliffAngle;
        public float MaxCliffAngle;

        public int HeightmapWidth;
        public int HeightmapHeight;
        public float3 TerrainSize;

        [ReadOnly] public NativeArray<float> HeightMap;
        [ReadOnly] public NativeArray<int> TerritoryMap;
        [ReadOnly] public NativeArray<int2> NodeLayerIndices;

        [NativeDisableParallelForRestriction] 
        public NativeArray<float> Alphamap;

        public void Execute(int index)
        {
            int x = index % AlphaWidth;
            int y = index / AlphaWidth;

            int localX = math.clamp((int)math.round((float)x / (AlphaWidth - 1) * (ChunkSize - 1)), 0, ChunkSize - 1);
            int localY = math.clamp((int)math.round((float)y / (AlphaHeight - 1) * (ChunkSize - 1)), 0, ChunkSize - 1);

            float normX = (float)x / (AlphaWidth - 1);
            float normY = (float)y / (AlphaHeight - 1);

            // C++ 엔진을 거치지 않고 Job 내부에서 경사도를 직접 수학적으로 계산 
            float steepness = CalculateSteepness(normX, normY);

            float cliffWeight = math.smoothstep(0f, 1f, math.unlerp(MinCliffAngle, MaxCliffAngle, steepness));
            float topWeight = 1f - cliffWeight;

            int nodeIndex = TerritoryMap[localY * ChunkSize + localX];
            int topIndex = 0;
            int cliffIndex = 0;

            if (nodeIndex >= 0 && nodeIndex < NodeLayerIndices.Length)
            {
                int2 indices = NodeLayerIndices[nodeIndex];
                topIndex = indices.x;
                cliffIndex = indices.y;
            }

            int baseIdx = (y * AlphaWidth + x) * NumLayers;

            for (int l = 0; l < NumLayers; l++)
            {
                Alphamap[baseIdx + l] = 0f;
            }

            if (nodeIndex >= 0 && nodeIndex < NodeLayerIndices.Length)
            {
                Alphamap[baseIdx + topIndex] += topWeight;
                Alphamap[baseIdx + cliffIndex] += cliffWeight;
            }
            else
            {
                Alphamap[baseIdx + 0] = 1.0f;
            }
        }

        private float CalculateSteepness(float normX, float normY)
        {
            float hx = normX * (HeightmapWidth - 1);
            float hy = normY * (HeightmapHeight - 1);

            int x0 = (int)math.floor(hx);
            int y0 = (int)math.floor(hy);

            int xL = math.max(0, x0 - 1);
            int xR = math.min(HeightmapWidth - 1, x0 + 1);
            int yD = math.max(0, y0 - 1);
            int yU = math.min(HeightmapHeight - 1, y0 + 1);

            float hL = HeightMap[y0 * HeightmapWidth + xL];
            float hR = HeightMap[y0 * HeightmapWidth + xR];
            float hD = HeightMap[yD * HeightmapWidth + x0];
            float hU = HeightMap[yU * HeightmapWidth + x0];

            float dxCells = math.max(1f, xR - xL);
            float dyCells = math.max(1f, yU - yD);

            float dx = (hR - hL) * TerrainSize.y / (dxCells * TerrainSize.x / (HeightmapWidth - 1));
            float dy = (hU - hD) * TerrainSize.y / (dyCells * TerrainSize.z / (HeightmapHeight - 1));

            float3 normal = math.normalize(new float3(-dx, 1f, -dy));
            return math.acos(math.clamp(normal.y, -1f, 1f)) * 57.29578f; // Radian to Degree
        }
    }

    [BurstCompile]
    private struct BlurAlphamapJob : IJobParallelFor
    {
        public int AlphaWidth;
        public int AlphaHeight;
        public int NumLayers;
        public int BlurRadius;

        [ReadOnly] public NativeArray<float> InputMap;
        [NativeDisableParallelForRestriction]
        public NativeArray<float> OutputMap;

        public void Execute(int index)
        {
            int x = index % AlphaWidth;
            int y = index / AlphaWidth;
            int baseOutIdx = (y * AlphaWidth + x) * NumLayers;

            float totalWeight = 0f;

            for (int l = 0; l < NumLayers; l++)
            {
                float sum = 0f;
                int count = 0;

                for (int dy = -BlurRadius; dy <= BlurRadius; dy++)
                {
                    for (int dx = -BlurRadius; dx <= BlurRadius; dx++)
                    {
                        int nx = math.clamp(x + dx, 0, AlphaWidth - 1);
                        int ny = math.clamp(y + dy, 0, AlphaHeight - 1);

                        int inIdx = (ny * AlphaWidth + nx) * NumLayers + l;
                        sum += InputMap[inIdx];
                        count++;
                    }
                }

                float avg = sum / count;
                OutputMap[baseOutIdx + l] = avg;
                totalWeight += avg;
            }

            if (totalWeight > 0f)
            {
                for (int l = 0; l < NumLayers; l++)
                {
                    OutputMap[baseOutIdx + l] /= totalWeight;
                }
            }
        }
    }
    #endregion

    public async UniTask PaintChunkTerrainAsync(
        TerrainData terrainData, ChunkData chunk, WorldGraphData graphData, TerrainLayerPalette palette, CancellationToken ct)
    {
        if (palette == null || palette.Layers == null || palette.Layers.Length == 0) return;

        terrainData.terrainLayers = palette.Layers;

        int alphaWidth = terrainData.alphamapWidth;
        int alphaHeight = terrainData.alphamapHeight;
        int numLayers = palette.Layers.Length;
        int totalPixels = alphaWidth * alphaHeight;
        int chunkSize = chunk.TerritoryMap.GetLength(0);

        int hWidth = terrainData.heightmapResolution;
        int hHeight = terrainData.heightmapResolution;
        float[,] heights2D = terrainData.GetHeights(0, 0, hWidth, hHeight);

        // =========================================================
        // 1. 메모리 직렬화 (2D/3D 배열을 1D NativeArray로 펼치기)
        // =========================================================
        // 높이맵 직렬화  
        NativeArray<float> heightMap = new NativeArray<float>(hWidth * hHeight, Allocator.TempJob);

        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int y = 0; y < hHeight; y++)
        {
            for (int x = 0; x < hWidth; x++)
            {
                heightMap[y * hWidth + x] = heights2D[y, x];
            }
            // 5ms 초과 시 다음 프레임으로 양보
            if (stopwatch.ElapsedMilliseconds > 5)
            {
                await UniTask.Yield(ct);
                stopwatch.Restart();
            }
        }
        // 영토맵 직렬화
        NativeArray<int> territoryMap = new NativeArray<int>(chunkSize * chunkSize, Allocator.TempJob);
        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                territoryMap[y * chunkSize + x] = chunk.TerritoryMap[x, y];
            }
        }
        // 노드별 레이어 인덱스 매핑 생성 (노드 인덱스 -> (TopLayer, CliffLayer) 인덱스)
        int nodeCount = graphData.Nodes.Count;
        NativeArray<int2> nodeLayerIndices = new NativeArray<int2>(nodeCount, Allocator.TempJob);
        for (int i = 0; i < nodeCount; i++)
        {
            var node = graphData.Nodes[i];
            int tIdx = 0, cIdx = 0;
            if (!string.IsNullOrEmpty(node.BiomeData?.TopKey) && palette.IndexMap.TryGetValue(node.BiomeData.TopKey, out int t)) tIdx = t;
            if (!string.IsNullOrEmpty(node.BiomeData?.CliffKey) && palette.IndexMap.TryGetValue(node.BiomeData.CliffKey, out int c)) cIdx = c;
            else cIdx = tIdx;

            nodeLayerIndices[i] = new int2(tIdx, cIdx);
        }

        NativeArray<float> alpha1D = new NativeArray<float>(totalPixels * numLayers, Allocator.TempJob);
        NativeArray<float> blur1D = new NativeArray<float>(totalPixels * numLayers, Allocator.TempJob);

        // =========================================================
        // 2. 멀티코어 Job 스케줄링 및 대기
        // =========================================================
        PaintBaseJob paintJob = new PaintBaseJob
        {
            AlphaWidth = alphaWidth,
            AlphaHeight = alphaHeight,
            ChunkSize = chunkSize,
            NumLayers = numLayers,
            MinCliffAngle = 25f,
            MaxCliffAngle = 45f,
            HeightmapWidth = hWidth,
            HeightmapHeight = hHeight,
            TerrainSize = terrainData.size,
            HeightMap = heightMap,
            TerritoryMap = territoryMap,
            NodeLayerIndices = nodeLayerIndices,
            Alphamap = alpha1D
        };
        JobHandle paintHandle = paintJob.Schedule(totalPixels, 64);

        BlurAlphamapJob blurJob = new BlurAlphamapJob
        {
            AlphaWidth = alphaWidth,
            AlphaHeight = alphaHeight,
            NumLayers = numLayers,
            BlurRadius = BLUR_RADIUS,
            InputMap = alpha1D,
            OutputMap = blur1D
        };
        JobHandle blurHandle = blurJob.Schedule(totalPixels, 64, paintHandle);

        while (!blurHandle.IsCompleted)
        {
            await UniTask.Yield(ct);
        }
        blurHandle.Complete();

        // =========================================================
        // 3. 유니티 포맷(float[,,])으로 복구 및 메모리 해제
        // =========================================================
        float[,,] finalAlphamaps = new float[alphaHeight, alphaWidth, numLayers];            
        for (int y = 0; y < alphaHeight; y++)
        {
            for (int x = 0; x < alphaWidth; x++)
            {
                int baseIdx = (y * alphaWidth + x) * numLayers;
                for (int l = 0; l < numLayers; l++)
                {
                    finalAlphamaps[y, x, l] = blur1D[baseIdx + l];
                }
            }
            if (stopwatch.ElapsedMilliseconds > 5)
            {
                await UniTask.Yield(ct);
                stopwatch.Restart();
            }
        }

        heightMap.Dispose();
        territoryMap.Dispose();
        nodeLayerIndices.Dispose();
        alpha1D.Dispose();
        blur1D.Dispose();

        await UniTask.Yield(ct); // SetAlphamaps 전 메인 스레드 숨 고르기

        terrainData.SetAlphamaps(0, 0, finalAlphamaps);
        Debug.Log($"🎨 [TerrainPainter] 청크 페인팅 완료! ({chunk.ChunkCoord.x}, {chunk.ChunkCoord.y})");
    }
}