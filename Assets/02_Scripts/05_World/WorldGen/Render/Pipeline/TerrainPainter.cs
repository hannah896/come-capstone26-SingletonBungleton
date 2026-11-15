using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

public class TerrainPainter
{
    private const int BLUR_RADIUS = 2;

    private readonly Stack<float[,,]> _alphamapPool = new();

    private float[,,] RentAlphamapBuffer(int height, int width, int layers)
    {
        while (_alphamapPool.Count > 0)
        {
            float[,,] pooled = _alphamapPool.Pop();
            if (pooled != null &&
                pooled.GetLength(0) == height &&
                pooled.GetLength(1) == width &&
                pooled.GetLength(2) == layers)
            {
                return pooled;
            }
        }

        return new float[height, width, layers];
    }

    private void ReturnAlphamapBuffer(float[,,] buffer)
    {
        if (buffer != null)
        {
            _alphamapPool.Push(buffer);
        }
    }

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

        public float2 ChunkWorldPos;

        [ReadOnly] public NativeArray<float> HeightMap;
        [ReadOnly] public NativeArray<int> TerritoryMap;
        [ReadOnly] public NativeArray<int3> NodeLayerIndices;

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

            // ★ 1. 현재 픽셀의 절대적인 월드 좌표 계산
            float worldX = ChunkWorldPos.x + normX * TerrainSize.x;
            float worldZ = ChunkWorldPos.y + normY * TerrainSize.z;

            // ★ 2. 매크로 노이즈(펄린 노이즈) 계산
            float noiseScale = 0.05f; // 숫자가 작을수록 구름(얼룩)이 거대해짐
            float rawNoise = noise.cnoise(new float2(worldX, worldZ) * noiseScale);
            float macroNoise = math.unlerp(-1f, 1f, rawNoise); // -1~1 값을 0~1로 변환

            float steepness = CalculateSteepness(normX, normY);
            float cliffWeight = math.smoothstep(0f, 1f, math.unlerp(MinCliffAngle, MaxCliffAngle, steepness));
            float totalTopWeight = 1f - cliffWeight;

            // ★ 4. 노이즈를 바탕으로 평지 물감(TopWeight)을 2개로 쪼개기
            float mainTopWeight = totalTopWeight * macroNoise;
            float altTopWeight = totalTopWeight * (1f - macroNoise);

            int nodeIndex = TerritoryMap[localY * ChunkSize + localX];
            int topIndex = 0;
            int altIndex = 0;
            int cliffIndex = 0;

            if (nodeIndex >= 0 && nodeIndex < NodeLayerIndices.Length)
            {
                int3 indices = NodeLayerIndices[nodeIndex];
                topIndex = indices.x;
                altIndex = indices.y; // AltIndex 꺼내기
                cliffIndex = indices.z;
            }

            int baseIdx = (y * AlphaWidth + x) * NumLayers;

            for (int l = 0; l < NumLayers; l++)
            {
                Alphamap[baseIdx + l] = 0f;
            }

            if (nodeIndex >= 0 && nodeIndex < NodeLayerIndices.Length)
            {
                Alphamap[baseIdx + topIndex] += mainTopWeight;
                Alphamap[baseIdx + altIndex] += altTopWeight;
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
        float maxHeight = terrainData.size.y;
        bool canUseChunkHeights = chunk.HeightMap != null &&
                                  chunk.HeightMap.GetLength(0) == hWidth &&
                                  chunk.HeightMap.GetLength(1) == hHeight &&
                                  maxHeight > 0f;
        float[,] heights2D = canUseChunkHeights ? null : terrainData.GetHeights(0, 0, hWidth, hHeight);

        // =========================================================
        // 1. 메모리 직렬화 (2D/3D 배열을 1D NativeArray로 펼치기)
        // =========================================================
        NativeArray<float> heightMap = new NativeArray<float>(hWidth * hHeight, Allocator.TempJob);
        NativeArray<int> territoryMap = new NativeArray<int>(chunkSize * chunkSize, Allocator.TempJob);
        int nodeCount = graphData.Nodes.Count;
        NativeArray<int3> nodeLayerIndices = new NativeArray<int3>(nodeCount, Allocator.TempJob);
        NativeArray<float> alpha1D = new NativeArray<float>(totalPixels * numLayers, Allocator.TempJob);
        NativeArray<float> blur1D = new NativeArray<float>(totalPixels * numLayers, Allocator.TempJob);
        JobHandle blurHandle = default;
        float[,,] finalAlphamaps = null;

        try
        {
            // 높이맵 직렬화
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            if (canUseChunkHeights)
            {
                float invMaxHeight = 1f / maxHeight;
                for (int y = 0; y < hHeight; y++)
                {
                    for (int x = 0; x < hWidth; x++)
                    {
                        heightMap[y * hWidth + x] = Mathf.Clamp01(chunk.HeightMap[x, y] * invMaxHeight);
                    }
                }
            }
            else
            {
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
            }

            // 영토맵 직렬화
            for (int y = 0; y < chunkSize; y++)
            {
                for (int x = 0; x < chunkSize; x++)
                {
                    territoryMap[y * chunkSize + x] = chunk.TerritoryMap[x, y];
                }
            }

            // 노드별 레이어 인덱스 매핑 생성 (노드 인덱스 -> (TopLayer, CliffLayer) 인덱스)
            for (int i = 0; i < nodeCount; i++)
            {
                var node = graphData.Nodes[i];
                int tIdx = 0, aIdx = 0, cIdx = 0;

                if (!string.IsNullOrEmpty(node.BiomeData?.TopKey) && palette.IndexMap.TryGetValue(node.BiomeData.TopKey, out int t)) tIdx = t;

                // ★ AltKey 매핑 (에디터에서 비워뒀다면 자동으로 TopKey로 덮어씌워서 에러 방지)
                if (!string.IsNullOrEmpty(node.BiomeData?.AltKey) && palette.IndexMap.TryGetValue(node.BiomeData.AltKey, out int a)) aIdx = a;
                else aIdx = tIdx;

                if (!string.IsNullOrEmpty(node.BiomeData?.CliffKey) && palette.IndexMap.TryGetValue(node.BiomeData.CliffKey, out int c)) cIdx = c;
                else cIdx = tIdx;

                // int3로 포장
                nodeLayerIndices[i] = new int3(tIdx, aIdx, cIdx);
            }

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

                ChunkWorldPos = new float2(chunk.ChunkCoord.x * chunkSize, chunk.ChunkCoord.y * chunkSize),

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
            blurHandle = blurJob.Schedule(totalPixels, 64, paintHandle);

            while (!blurHandle.IsCompleted)
            {
                await UniTask.Yield(ct);
            }
            blurHandle.Complete();

            // =========================================================
            // 3. 유니티 포맷(float[,,])으로 복구 및 재사용 버퍼 활용
            // =========================================================
            finalAlphamaps = RentAlphamapBuffer(alphaHeight, alphaWidth, numLayers);
            stopwatch.Restart();
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
        }
        finally
        {
            blurHandle.Complete();
            if (heightMap.IsCreated) heightMap.Dispose();
            if (territoryMap.IsCreated) territoryMap.Dispose();
            if (nodeLayerIndices.IsCreated) nodeLayerIndices.Dispose();
            if (alpha1D.IsCreated) alpha1D.Dispose();
            if (blur1D.IsCreated) blur1D.Dispose();
        }

        try
        {
            await UniTask.Yield(ct); // SetAlphamaps 전 메인 스레드 숨 고르기
            terrainData.SetAlphamaps(0, 0, finalAlphamaps);
        }
        finally
        {
            ReturnAlphamapBuffer(finalAlphamaps);
        }
    }
}