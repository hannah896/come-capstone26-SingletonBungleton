using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
//TODO: 맵 크기
/// <summary>
/// 3 단계: 보로노이 분할 및 자연스러운 경계 처리를 담당하는 클래스
/// </summary>
public class TerritoryBuilder : IGraphPipelineStage
{
    #region Constants

    private const float DEFAULT_SEPARATION_GAP = 15.0f;

    private const int NOISE_OCTAVES = 3;
    private const float NOISE_AMPLITUDE_DECAY = 0.5f;
    private const float NOISE_FREQUENCY_GROWTH = 2f;
    private const float OCEAN_NOISE_SCALE = 0.1f;

    public const int OCEAN_MARKER = -1;
    public const int BORDER_MARKER = -2;
    #endregion

    #region Fields
    private PartitionSettings _partiSettings;
    private WorldSettings _worldSettings;
    private WorldGraphData _graphResult;
    private WorldLogicData _worldLogicData; // 데이터 바구니 추가
    private System.Random _prng;
    private CancellationToken _ct;

    private SpatialGrid<NodeSpatialData> _nodeSpatialGrid;
    private List<NodeSpatialData> _nodeSpatialCache;        // 노드의 공간 데이터를 캐싱하는 리스트 (인덱스 기반 접근용)

    // 8방향 (영토 테두리 및 연결선 분리 탐색용)
    private static readonly int[] _dx8 = { -1, 1, 0, 0, -1, -1, 1, 1 };
    private static readonly int[] _dy8 = { 0, 0, -1, 1, -1, 1, -1, 1 };

    #endregion


    #region Nested Classes & Job 구조체
    /// <summary>
    /// 노드의 공간 데이터를 캐싱하는 구조체
    /// </summary>
    private class NodeSpatialData
    {
        public int Index;
        public Vector2 Position;
        public float RegionNoiseFactor;
        public RegionData RegionData;
        public float TerritoryWeight; // ★ 추가: 영토 확장 가중치: 노드가 많은 지역에 속할수록 영토가 넓어지는 효과 (밀집 지역 확장, 고립 지역 축소)

        // ★ 변경점: 포지션을 외부에서 주입받도록 수정
        public NodeSpatialData(int index, Node node, Vector2 position, float territoryWeight)
        {
            Index = index;
            Position = position;
            RegionData = node.RegionData;
            // 노드별 노이즈 팩터를 미리 계산
            RegionNoiseFactor = Mathf.Sin(index * 0.7f) * 0.5f + 0.5f;
            TerritoryWeight = territoryWeight;
        }
    }

    /// <summary>
    /// Burst Job 내부에서 사용하기 위한 블리터블(Unmanaged) 공간 데이터 구조체
    /// </summary>
    private struct SpatialPointData
    {
        public int NodeIndex;
        public float2 Position;
        public float RegionNoiseFactor;
        public float TerritoryWeight;
    }

    // 시드 동일 = 결과 동일을 보장하기 위해 엄격 모드 고정 (LandformJobs.HeightGenerationJob 주석 참고)
    [BurstCompile(FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.High, CompileSynchronously = true)]
    private struct NoiseGenerationJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<float> NoiseMap;

        public int Width;
        public float OffsetX;
        public float OffsetY;
        public float NoiseScale;
        public int Octaves;
        public float AmplitudeDecay;
        public float FrequencyGrowth;
        public float NoiseStrength;

        public void Execute(int index)
        {
            // 1차원 인덱스를 x, y 2차원 좌표로 변환
            int x = index % Width;
            int y = index / Width;

            float sampleX = (x + OffsetX) * NoiseScale;
            float sampleY = (y + OffsetY) * NoiseScale;

            float noiseResult = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float maxValue = 0f;

            for (int octave = 0; octave < Octaves; octave++)
            {
                // ★ 핵심: Mathf.PerlinNoise 대신 Unity.Mathematics의 cnoise를 사용!
                // cnoise는 -1 ~ 1 범위를 반환하므로, 기존 PerlinNoise와 똑같이 0 ~ 1 범위로 매핑해 줍니다.
                float rawNoise = noise.cnoise(new float2(sampleX * frequency, sampleY * frequency));
                float mappedNoise = (rawNoise + 1f) * 0.5f;

                noiseResult += mappedNoise * amplitude;
                maxValue += amplitude;
                amplitude *= AmplitudeDecay;
                frequency *= FrequencyGrowth;
            }

            // 계산된 최종 값을 배열에 저장
            NoiseMap[index] = (noiseResult / maxValue) * NoiseStrength;
        }
    }

    /// <summary>
    /// Phase 3: 도메인 워핑 + 보로노이 분할 + 비연결 지역 분리 병렬 Job
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.High, CompileSynchronously = true)]
    private struct AssignTerritoryJob : IJobParallelFor
    {
        public int Width;
        public int Height;
        public int GridW;
        public int GridH;
        public int CellSize;
        public int NodeCount;
        public float NoiseStrength;

        [ReadOnly] public NativeArray<float> NoiseMap;
        [ReadOnly] public NativeArray<float> NodeRadius;
        [ReadOnly] public NativeArray<int2> GridCellRanges;
        [ReadOnly] public NativeArray<SpatialPointData> GridBucketItems;
        [ReadOnly] public NativeArray<byte> ShouldSeparateMatrix;

        [WriteOnly] public NativeArray<int> TerritoryMap;

        public void Execute(int index)
        {
            int x = index % Width;
            int y = index / Width;

            // 도메인 워핑 (cnoise는 -1 ~ 1 범위 반환)
            float warpFreq = 0.035f;
            float warpStrength = 15.0f;

            float warpX = noise.cnoise(new float2(x * warpFreq + 12.3f, y * warpFreq + 45.6f)) * warpStrength;
            float warpY = noise.cnoise(new float2(x * warpFreq + 78.9f, y * warpFreq + 12.3f)) * warpStrength;
            float2 tilePos = new float2(x + warpX, y + warpY);

            int idx1 = -1;
            float dist1 = float.MaxValue;
            int idx2 = -1;
            float dist2 = float.MaxValue;

            float noiseOffset = NoiseMap[index];

            int gx = x / CellSize;
            int gy = y / CellSize;
            if (gx >= 0 && gx < GridW && gy >= 0 && gy < GridH)
            {
                int2 range = GridCellRanges[gy * GridW + gx];
                int start = range.x;
                int end = start + range.y;

                for (int i = start; i < end; i++)
                {
                    SpatialPointData spatialData = GridBucketItems[i];
                    float distance = math.distance(tilePos, spatialData.Position);
                    float weightedDistance = distance - spatialData.TerritoryWeight;

                    if (distance - NoiseStrength > dist2) continue;

                    float distortedDistance = weightedDistance + (noiseOffset * spatialData.RegionNoiseFactor);

                    if (distortedDistance < dist1)
                    {
                        if (idx1 != spatialData.NodeIndex && idx1 != -1)
                        {
                            dist2 = dist1;
                            idx2 = idx1;
                        }

                        dist1 = distortedDistance;
                        idx1 = spatialData.NodeIndex;
                    }
                    else if (distortedDistance < dist2 && spatialData.NodeIndex != idx1)
                    {
                        dist2 = distortedDistance;
                        idx2 = spatialData.NodeIndex;
                    }
                }
            }

            int finalOwner = OCEAN_MARKER;
            if (idx1 != -1 && dist1 <= NodeRadius[idx1])
            {
                float dynamicSeparationGap = DEFAULT_SEPARATION_GAP + (noiseOffset * 2f);
                bool shouldSeparate = false;

                if (idx2 != -1 && (dist2 - dist1) < dynamicSeparationGap)
                {
                    shouldSeparate = ShouldSeparateMatrix[idx1 * NodeCount + idx2] != 0;
                }

                if (!shouldSeparate)
                {
                    finalOwner = idx1;
                }
            }

            TerritoryMap[index] = finalOwner;
        }
    }

    /// <summary>
    /// Phase 4: 고립된 섬(Orphan) 타일 병합 병렬 Job
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.High, CompileSynchronously = true)]
    private struct CleanUpOrphanTilesJob : IJobParallelFor
    {
        public int Width;
        public int Height;

        [ReadOnly] public NativeArray<int> ReadTerritory;
        [WriteOnly] public NativeArray<int> WriteTerritory;

        public void Execute(int index)
        {
            int x = index % Width;
            int y = index / Width;

            int currentOwner = ReadTerritory[index];

            // 맵 가장자리 또는 바다는 정화 대상에서 제외
            if (x <= 0 || x >= Width - 1 || y <= 0 || y >= Height - 1 || currentOwner == OCEAN_MARKER)
            {
                WriteTerritory[index] = currentOwner;
                return;
            }

            // 8방향 이웃 소유자 수집 (기존 탐색 순서: (-1,-1),(0,-1),(1,-1),(-1,0),(1,0),(-1,1),(0,1),(1,1))
            int n0 = ReadTerritory[(y - 1) * Width + (x - 1)];
            int n1 = ReadTerritory[(y - 1) * Width + x];
            int n2 = ReadTerritory[(y - 1) * Width + (x + 1)];
            int n3 = ReadTerritory[y * Width + (x - 1)];
            int n4 = ReadTerritory[y * Width + (x + 1)];
            int n5 = ReadTerritory[(y + 1) * Width + (x - 1)];
            int n6 = ReadTerritory[(y + 1) * Width + x];
            int n7 = ReadTerritory[(y + 1) * Width + (x + 1)];

            int sameOwnerCount = 0;
            if (n0 == currentOwner) sameOwnerCount++;
            if (n1 == currentOwner) sameOwnerCount++;
            if (n2 == currentOwner) sameOwnerCount++;
            if (n3 == currentOwner) sameOwnerCount++;
            if (n4 == currentOwner) sameOwnerCount++;
            if (n5 == currentOwner) sameOwnerCount++;
            if (n6 == currentOwner) sameOwnerCount++;
            if (n7 == currentOwner) sameOwnerCount++;

            if (sameOwnerCount > 2)
            {
                WriteTerritory[index] = currentOwner;
                return;
            }

            // 주변 8칸 중 바다를 제외한 최다 빈도 소유자 탐색 (동률 시 먼저 등장한 방향 우선 유지)
            int bestOwner = currentOwner;
            int bestCount = 0;

            CheckCandidate(n0, n0, n1, n2, n3, n4, n5, n6, n7, ref bestOwner, ref bestCount);
            CheckCandidate(n1, n0, n1, n2, n3, n4, n5, n6, n7, ref bestOwner, ref bestCount);
            CheckCandidate(n2, n0, n1, n2, n3, n4, n5, n6, n7, ref bestOwner, ref bestCount);
            CheckCandidate(n3, n0, n1, n2, n3, n4, n5, n6, n7, ref bestOwner, ref bestCount);
            CheckCandidate(n4, n0, n1, n2, n3, n4, n5, n6, n7, ref bestOwner, ref bestCount);
            CheckCandidate(n5, n0, n1, n2, n3, n4, n5, n6, n7, ref bestOwner, ref bestCount);
            CheckCandidate(n6, n0, n1, n2, n3, n4, n5, n6, n7, ref bestOwner, ref bestCount);
            CheckCandidate(n7, n0, n1, n2, n3, n4, n5, n6, n7, ref bestOwner, ref bestCount);

            WriteTerritory[index] = bestOwner;
        }

        private static void CheckCandidate(
            int candidate,
            int n0, int n1, int n2, int n3, int n4, int n5, int n6, int n7,
            ref int bestOwner, ref int bestCount)
        {
            if (candidate == OCEAN_MARKER) return;

            int count = 0;
            if (n0 == candidate) count++;
            if (n1 == candidate) count++;
            if (n2 == candidate) count++;
            if (n3 == candidate) count++;
            if (n4 == candidate) count++;
            if (n5 == candidate) count++;
            if (n6 == candidate) count++;
            if (n7 == candidate) count++;

            if (count > bestCount)
            {
                bestCount = count;
                bestOwner = candidate;
            }
        }
    }

    /// <summary>
    /// Phase 5-1: 맵 가장자리 해안선 절삭 병렬 Job
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.High, CompileSynchronously = true)]
    private struct ApplyOceanEdgeJob : IJobParallelFor
    {
        public int Width;
        public int Height;
        public float HalfWidth;
        public float HalfHeight;
        public float NoiseStrength;
        public float ThresholdLimit;

        [ReadOnly] public NativeArray<float> NoiseMap;
        public NativeArray<int> TerritoryMap;

        public void Execute(int index)
        {
            int x = index % Width;
            int y = index / Width;

            float edgeDistanceX = math.min((float)x, (float)(Width - 1 - x)) / HalfWidth;
            float edgeDistanceY = math.min((float)y, (float)(Height - 1 - y)) / HalfHeight;
            float edgeDistance = math.min(edgeDistanceX, edgeDistanceY);

            float oceanNoise = (NoiseMap[index] / NoiseStrength) * OCEAN_NOISE_SCALE;

            if (edgeDistance + oceanNoise < ThresholdLimit)
            {
                TerritoryMap[index] = OCEAN_MARKER;
            }
        }
    }

    /// <summary>
    /// Phase 5-3: 영토 경계선 타일 마킹 병렬 Job
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.High, CompileSynchronously = true)]
    private struct MarkBorderTilesJob : IJobParallelFor
    {
        public int Width;
        public int Height;

        [ReadOnly] public NativeArray<int> TerritoryMap;
        [WriteOnly] public NativeArray<int> BorderMap;

        public void Execute(int index)
        {
            int x = index % Width;
            int y = index / Width;

            if (x <= 0 || x >= Width - 1 || y <= 0 || y >= Height - 1)
            {
                BorderMap[index] = 0;
                return;
            }

            int currentRegion = TerritoryMap[index];
            if (currentRegion == OCEAN_MARKER)
            {
                BorderMap[index] = 0;
                return;
            }

            int left = TerritoryMap[index - 1];
            int right = TerritoryMap[index + 1];
            int down = TerritoryMap[index - Width];
            int up = TerritoryMap[index + Width];

            bool isBorder =
                (left != currentRegion && left != OCEAN_MARKER) ||
                (right != currentRegion && right != OCEAN_MARKER) ||
                (down != currentRegion && down != OCEAN_MARKER) ||
                (up != currentRegion && up != OCEAN_MARKER);

            BorderMap[index] = isBorder ? BORDER_MARKER : 0;
        }
    }

    /// <summary>
    /// Phase 7-1: 해안선(4방향) 및 지역 경계(8방향) 거리 BFS 및 노드별 최대 깊이 산출 Job
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.High, CompileSynchronously = true)]
    private struct InfluenceBfsJob : IJob
    {
        public int Width;
        public int Height;

        [ReadOnly] public NativeArray<int> TerritoryMap;
        [ReadOnly] public NativeArray<int> NodeRegionIds;

        public NativeArray<int> QueueOcean;
        public NativeArray<int> QueueEdge;
        public NativeArray<byte> VisitedOcean;
        public NativeArray<byte> VisitedEdge;

        public NativeArray<int> DistanceToOcean;
        public NativeArray<float> DistanceToEdge;
        public NativeArray<int> NodeMaxOceanDepth;
        public NativeArray<float> NodeMaxEdgeDepth;

        public void Execute()
        {
            int headOcean = 0;
            int tailOcean = 0;
            int headEdge = 0;
            int tailEdge = 0;

            for (int i = 0; i < NodeMaxOceanDepth.Length; i++)
            {
                NodeMaxOceanDepth[i] = 1;
                NodeMaxEdgeDepth[i] = 1f;
            }

            // 1. 초기 시드(바다 및 지역 경계선) 큐 삽입 (기존 (x, y) 순회 순서 동일 유지)
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    int idx = y * Width + x;
                    int ownerNode = TerritoryMap[idx];

                    if (ownerNode == OCEAN_MARKER)
                    {
                        DistanceToOcean[idx] = 0;
                        VisitedOcean[idx] = 1;
                        QueueOcean[tailOcean++] = idx;
                        DistanceToEdge[idx] = 0f;
                    }
                    else
                    {
                        DistanceToOcean[idx] = int.MaxValue;
                        DistanceToEdge[idx] = float.MaxValue;

                        int myRegion = NodeRegionIds[ownerNode];
                        bool isBorder = false;

                        for (int d = 0; d < 8; d++)
                        {
                            int nx = x + GetDx8(d);
                            int ny = y + GetDy8(d);

                            if (nx < 0 || nx >= Width || ny < 0 || ny >= Height)
                            {
                                isBorder = true;
                                break;
                            }

                            int neighborNode = TerritoryMap[ny * Width + nx];
                            if (neighborNode == OCEAN_MARKER || NodeRegionIds[neighborNode] != myRegion)
                            {
                                isBorder = true;
                                break;
                            }
                        }

                        if (isBorder)
                        {
                            DistanceToEdge[idx] = 0f;
                            VisitedEdge[idx] = 1;
                            QueueEdge[tailEdge++] = idx;
                        }
                    }
                }
            }

            // 2-1. 해안선 4방향 BFS
            while (headOcean < tailOcean)
            {
                int currIdx = QueueOcean[headOcean++];
                int cx = currIdx % Width;
                int cy = currIdx / Width;
                int currDist = DistanceToOcean[currIdx];

                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + GetDx8(d);
                    int ny = cy + GetDy8(d);

                    if (nx >= 0 && nx < Width && ny >= 0 && ny < Height)
                    {
                        int nIdx = ny * Width + nx;
                        if (VisitedOcean[nIdx] == 0 && TerritoryMap[nIdx] >= 0)
                        {
                            VisitedOcean[nIdx] = 1;
                            DistanceToOcean[nIdx] = currDist + 1;
                            QueueOcean[tailOcean++] = nIdx;
                        }
                    }
                }
            }

            // 2-2. 지역 경계선 8방향 BFS
            while (headEdge < tailEdge)
            {
                int currIdx = QueueEdge[headEdge++];
                int cx = currIdx % Width;
                int cy = currIdx / Width;
                float currDist = DistanceToEdge[currIdx];

                int myOwner = TerritoryMap[currIdx];
                int myRegion = NodeRegionIds[myOwner];

                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + GetDx8(d);
                    int ny = cy + GetDy8(d);

                    if (nx >= 0 && nx < Width && ny >= 0 && ny < Height)
                    {
                        int nIdx = ny * Width + nx;
                        if (VisitedEdge[nIdx] == 0)
                        {
                            int neighborNode = TerritoryMap[nIdx];
                            if (neighborNode != OCEAN_MARKER && NodeRegionIds[neighborNode] == myRegion)
                            {
                                VisitedEdge[nIdx] = 1;
                                DistanceToEdge[nIdx] = currDist + GetDist8(d);
                                QueueEdge[tailEdge++] = nIdx;
                            }
                        }
                    }
                }
            }

            // 3. 각 노드별 최대 깊이 계산
            int totalLength = Width * Height;
            for (int idx = 0; idx < totalLength; idx++)
            {
                int owner = TerritoryMap[idx];
                if (owner >= 0)
                {
                    int oceanDist = DistanceToOcean[idx];
                    if (oceanDist > NodeMaxOceanDepth[owner])
                        NodeMaxOceanDepth[owner] = oceanDist;

                    float edgeDist = DistanceToEdge[idx];
                    if (edgeDist > NodeMaxEdgeDepth[owner])
                        NodeMaxEdgeDepth[owner] = edgeDist;
                }
            }
        }

        private static int GetDx8(int d)
        {
            switch (d)
            {
                case 0: return -1;
                case 1: return 1;
                case 2: return 0;
                case 3: return 0;
                case 4: return -1;
                case 5: return -1;
                case 6: return 1;
                default: return 1;
            }
        }

        private static int GetDy8(int d)
        {
            switch (d)
            {
                case 0: return 0;
                case 1: return 0;
                case 2: return -1;
                case 3: return 1;
                case 4: return -1;
                case 5: return 1;
                case 6: return -1;
                default: return 1;
            }
        }

        private static float GetDist8(int d)
        {
            return d < 4 ? 1.0f : 1.414f;
        }
    }

    /// <summary>
    /// Phase 7-2: 거리 정규화 및 SmoothStep 가중치 병렬 계산 Job
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.High, CompileSynchronously = true)]
    private struct ComputeInfluenceWeightsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> TerritoryMap;
        [ReadOnly] public NativeArray<int> DistanceToOcean;
        [ReadOnly] public NativeArray<float> DistanceToEdge;
        [ReadOnly] public NativeArray<int> NodeMaxOceanDepth;
        [ReadOnly] public NativeArray<float> NodeMaxEdgeDepth;

        [WriteOnly] public NativeArray<InfluenceData> CoastlineData;
        [WriteOnly] public NativeArray<InfluenceData> RegionEdgeData;

        public void Execute(int index)
        {
            int owner = TerritoryMap[index];
            if (owner >= 0)
            {
                int oceanDist = DistanceToOcean[index];
                float edgeDist = DistanceToEdge[index];

                float currentMaxOceanDepth = math.max(1f, (float)NodeMaxOceanDepth[owner]);
                float coastPercentage = (float)oceanDist / currentMaxOceanDepth;

                float currentMaxEdgeDepth = math.max(1f, NodeMaxEdgeDepth[owner]);
                float edgePercentage = edgeDist / currentMaxEdgeDepth;

                CoastlineData[index] = new InfluenceData
                {
                    Distance = oceanDist,
                    Weight = SmoothStepRange(0.1f, 1.0f, coastPercentage)
                };

                RegionEdgeData[index] = new InfluenceData
                {
                    Distance = edgeDist,
                    Weight = SmoothStepRange(0.1f, 1.0f, edgePercentage)
                };
            }
            else
            {
                CoastlineData[index] = new InfluenceData { Distance = 0f, Weight = 0f };
                RegionEdgeData[index] = new InfluenceData { Distance = 0f, Weight = 0f };
            }
        }

        // Mathf.SmoothStep(from, to, Mathf.Clamp01(t))와 동일한 보간 연산
        private static float SmoothStepRange(float from, float to, float t)
        {
            t = math.clamp(t, 0f, 1f);
            t = t * t * (3f - 2f * t);
            return math.lerp(from, to, t);
        }
    }
    #endregion


    public void Initialize(WorldSettings settings)
    {
        _worldSettings = settings;
        _prng = new System.Random(settings.WorldSeed + (int)WorldSeedChannel.TerritoryBuilder);
    }
    /// <summary>
    /// 맵 분할 메인 파이프라인
    /// </summary>
    public async UniTask ExecuteAsync(
        WorldGenContext ctx,
        CancellationToken ct)
    {
        try
        {
            _partiSettings = _worldSettings.PartitionSettings;
            _graphResult = ctx.GraphData;
            _worldLogicData = ctx.LogicData;
            _ct = ct;

            //await FitNodesToTileGridAsync();

            // 최근접 노드 검색 최적화용 공간 그리드 구성
            await SpatialGridAsync();

            // 노이즈 맵 생성 - 타일별로 다중 옥타브 펄린 노이즈로 타일간 거리 왜곡
            await GenerateNoiseWorldAsync();

            // 보로노이 분할 + 노이즈 왜곡 + 연결된 지역 분리 로직으로 타일에 영토 할당
            await AssignTerritoryAsync();

            // 노이즈로 인해 발생한 고립된 섬(Orphan) 타일들을 주변 영토로 병합
            await CleanUpOrphanTilesAsync();

            // 해안선 처리 후 필수 육로를 복원하고, 최종 영토를 기준으로 경계를 표시합니다.
            await ProcessBordersAsync();

            // 타일 데이터를 각 노드의 RegionData에 할당하여 소유 타일 목록 구축
            await AssignOwnedTilesToRegionsAsync();

            TryAssignStartRegionSpawnTile();

            // 영향력 맵 생성 - 각 타일이 해안선과 영토 경계선에서 얼마나 떨어져 있는지 계산하여 영향력 값으로 저장
            await GenerateInfluenceMapAsync();

        }
        catch (System.OperationCanceledException)
        {
            Debug.LogWarning("Tile partitioning 이 취소되었습니다.");
            throw;
        }
        finally
        {
            // 메모리 정리
            _nodeSpatialGrid?.Clear();
            _nodeSpatialCache?.Clear();
        }
    }

    /// <summary>
    /// 공간 분할 그리드 초기화
    /// 터레인 픽셀 별 최근접 노드 검색 최적화를 위해 노드 위치를 공간 그리드에 등록
    /// </summary>
    /// <returns></returns>
    #region Phase 1: Spatial Grid Initialization
    /// <summary>
    /// 공간 분할 그리드 초기화
    /// 터레인 픽셀 별 최근접 노드 검색 최적화를 위해 노드 위치를 공간 그리드에 등록
    /// </summary>
    private async UniTask SpatialGridAsync()
    {
        var nodes = _graphResult.Nodes;
        if (nodes == null || nodes.Count == 0) return;

        float mapArea = _worldLogicData.TerrainSize.x * _worldLogicData.TerrainSize.y;
        float avgAreaPerNode = mapArea / nodes.Count;
        float baseRadius = Mathf.Sqrt(avgAreaPerNode / Mathf.PI);
        float maxTerritoryRadius = baseRadius * 2.0f;

        int cellSize = Mathf.Max(10, Mathf.CeilToInt(maxTerritoryRadius * 2f));

        _nodeSpatialGrid = new SpatialGrid<NodeSpatialData>(
            _worldLogicData.TerrainSize.x,
            _worldLogicData.TerrainSize.y,
            cellSize
        );

        float minScale = nodes.Min(n => n.RegionData != null ? n.RegionData.TerritoryScale : 1f);
        float expansionPower = 10.0f;

        _nodeSpatialCache = new List<NodeSpatialData>(nodes.Count);

        // 1. 메인 노드(거점) 등록
        for (int i = 0; i < nodes.Count; i++)
        {
            float scale = nodes[i].RegionData != null ? nodes[i].RegionData.TerritoryScale : 1f;
            float ratio = scale / minScale;

            float weight = (Mathf.Sqrt(ratio) - 1f) * expansionPower;

            var spatialData = new NodeSpatialData(i, nodes[i], nodes[i].Position, weight);
            _nodeSpatialCache.Add(spatialData);
            _nodeSpatialGrid.AddToNeighbors(spatialData, nodes[i].Position);
        }

        // ★ 변경점: 2. 연결된 노드들 사이에 "가상의 땅(Bridge Point)"들을 징검다리처럼 삽입합니다.
        if (_graphResult.NodeConnections != null)
        {
            float bridgeInterval = baseRadius * 0.4f; // 징검다리 간격 (촘촘하게)

            foreach (var conn in _graphResult.NodeConnections)
            {
                int pIdx = nodes.IndexOf(conn.ParentNode);
                int cIdx = nodes.IndexOf(conn.ChildNode);
                if (pIdx == -1 || cIdx == -1) continue;

                float dist = Vector2.Distance(conn.ParentNode.Position, conn.ChildNode.Position);
                int steps = Mathf.CeilToInt(dist / bridgeInterval);

                for (int step = 1; step < steps; step++)
                {
                    float t = (float)step / steps;
                    Vector2 bridgePos = Vector2.Lerp(conn.ParentNode.Position, conn.ChildNode.Position, t);

                    // 다리의 소유권은 절반을 기준으로 각각 부모/자식에게 부여
                    int ownerIdx = (t < 0.5f) ? pIdx : cIdx;
                    Node ownerNode = nodes[ownerIdx];

                    float scale = ownerNode.RegionData != null ? ownerNode.RegionData.TerritoryScale : 1f;
                    float ratio = scale / Mathf.Max(0.01f, minScale);
                    
                    // 이어지는 다리는 중앙부가 살짝 얇아지도록 가중치에서 페널티를 줍니다.
                    float bridgeWeight = ((Mathf.Sqrt(ratio) - 1f) * expansionPower) - (baseRadius * 0.25f);

                    var bridgeData = new NodeSpatialData(ownerIdx, ownerNode, bridgePos, bridgeWeight);
                    _nodeSpatialGrid.AddToNeighbors(bridgeData, bridgePos);
                }
            }
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }
    #endregion

    /// <summary>
    /// 노이즈 맵 생성 - 타일별로 다중 옥타브 펄린 노이즈 계산하여 
    /// 이후 타일 간 거리 계산 시 노이즈로 왜곡하여 자연스러운 경계 형태 유도
    /// </summary>
    /// <returns></returns>
    #region Phase 2: Noise Map Generation
    private async UniTask GenerateNoiseWorldAsync()
    {
        int width = _worldLogicData.TerrainSize.x;
        int height = _worldLogicData.TerrainSize.y;
        int totalLength = width * height;

        float offsetX = _prng.Next(-10000, 10000);
        float offsetY = _prng.Next(-10000, 10000);

        NativeArray<float> nativeNoise = new NativeArray<float>(totalLength, Allocator.TempJob);
        JobHandle handle = default;

        try
        {
            NoiseGenerationJob job = new NoiseGenerationJob
            {
                NoiseMap = nativeNoise,
                Width = width,
                OffsetX = offsetX,
                OffsetY = offsetY,
                NoiseScale = _partiSettings.noiseScale,
                Octaves = NOISE_OCTAVES,
                AmplitudeDecay = NOISE_AMPLITUDE_DECAY,
                FrequencyGrowth = NOISE_FREQUENCY_GROWTH,
                NoiseStrength = _partiSettings.noiseStrength
            };

            handle = job.Schedule(totalLength, 64);

            while (!handle.IsCompleted)
            {
                await UniTask.Yield(_ct);
            }
            handle.Complete();

            int index = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    _worldLogicData.NoiseWorld[x, y] = nativeNoise[index++];
                }
            }
        }
        finally
        {
            handle.Complete();
            if (nativeNoise.IsCreated) nativeNoise.Dispose();
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }
    #endregion

    /// <summary>
    /// 터레인 타일에 가장 가까운 노드를 찾아서 영토 할당 - 보로노이 분할 + 노이즈 왜곡 + 연결된 지역 분리 로직 포함
    /// </summary>
    /// <returns></returns>
    #region Phase 3: AssignTerritoryAsync
    private async UniTask AssignTerritoryAsync()
    {
        var nodes = _graphResult.Nodes;
        if (nodes == null || nodes.Count == 0)
        {
            InitializeOceanWorld();
            return;
        }

        int width = _worldLogicData.TerrainSize.x;
        int height = _worldLogicData.TerrainSize.y;
        int totalLength = width * height;
        int nodeCount = nodes.Count;

        HashSet<(RegionData, RegionData)> connectedRegions = BuildConnectedRegionsSet();

        float mapArea = width * height;
        float avgAreaPerNode = mapArea / nodeCount;
        float baseRadius = Mathf.Sqrt(avgAreaPerNode / Mathf.PI);

        float avgScale = nodes.Average(n => (n.RegionData != null) ? n.RegionData.TerritoryScale : 1f);

        // 공간 그리드(SpatialGrid) 버킷을 1차원 NativeArray로 평탄화
        int cellCount = _nodeSpatialGrid.CellCount;
        int totalBucketItems = 0;
        for (int c = 0; c < cellCount; c++)
        {
            totalBucketItems += _nodeSpatialGrid.GetBucketByCellIndex(c).Count;
        }

        NativeArray<float> nativeNoise = new NativeArray<float>(totalLength, Allocator.TempJob);
        NativeArray<float> nativeNodeRadius = new NativeArray<float>(nodeCount, Allocator.TempJob);
        NativeArray<int2> nativeCellRanges = new NativeArray<int2>(cellCount, Allocator.TempJob);
        NativeArray<SpatialPointData> nativeBucketItems = new NativeArray<SpatialPointData>(Mathf.Max(1, totalBucketItems), Allocator.TempJob);
        NativeArray<byte> nativeShouldSeparate = new NativeArray<byte>(nodeCount * nodeCount, Allocator.TempJob);
        NativeArray<int> nativeTerritory = new NativeArray<int>(totalLength, Allocator.TempJob);
        JobHandle handle = default;

        float radiusSum = 0f;
        int landCount = 0;

        try
        {
            for (int i = 0; i < nodeCount; i++)
            {
                float scale = (nodes[i].RegionData != null) ? nodes[i].RegionData.TerritoryScale : 1f;
                float countScale = Mathf.Sqrt(scale / Mathf.Max(0.1f, avgScale));
                float radius = Mathf.Clamp(baseRadius * countScale, baseRadius * 0.4f, baseRadius * 2.2f);
                nativeNodeRadius[i] = radius;
                radiusSum += radius;
            }

            // 노드 쌍별 분리 여부 행렬 사전 구축
            for (int i = 0; i < nodeCount; i++)
            {
                RegionData r1 = nodes[i].RegionData;
                for (int j = 0; j < nodeCount; j++)
                {
                    RegionData r2 = nodes[j].RegionData;
                    bool separate = (r1 != r2) && !connectedRegions.Contains((r1, r2));
                    nativeShouldSeparate[i * nodeCount + j] = separate ? (byte)1 : (byte)0;
                }
            }

            // SpatialGrid 평탄화
            int cursor = 0;
            for (int c = 0; c < cellCount; c++)
            {
                var bucket = _nodeSpatialGrid.GetBucketByCellIndex(c);
                int count = bucket.Count;
                nativeCellRanges[c] = new int2(cursor, count);
                for (int k = 0; k < count; k++)
                {
                    var item = bucket[k];
                    nativeBucketItems[cursor++] = new SpatialPointData
                    {
                        NodeIndex = item.Index,
                        Position = new float2(item.Position.x, item.Position.y),
                        RegionNoiseFactor = item.RegionNoiseFactor,
                        TerritoryWeight = item.TerritoryWeight
                    };
                }
            }

            // NoiseWorld 복사
            int idx = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    nativeNoise[idx++] = _worldLogicData.NoiseWorld[x, y];
                }
            }

            var job = new AssignTerritoryJob
            {
                Width = width,
                Height = height,
                GridW = _nodeSpatialGrid.GridWidth,
                GridH = _nodeSpatialGrid.GridHeight,
                CellSize = _nodeSpatialGrid.CellSize,
                NodeCount = nodeCount,
                NoiseStrength = _partiSettings.noiseStrength,
                NoiseMap = nativeNoise,
                NodeRadius = nativeNodeRadius,
                GridCellRanges = nativeCellRanges,
                GridBucketItems = nativeBucketItems,
                ShouldSeparateMatrix = nativeShouldSeparate,
                TerritoryMap = nativeTerritory
            };

            handle = job.Schedule(totalLength, 64);

            while (!handle.IsCompleted)
            {
                await UniTask.Yield(_ct);
            }
            handle.Complete();

            idx = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int owner = nativeTerritory[idx++];
                    _worldLogicData.TerritoryWorld[x, y] = owner;
                    if (owner != OCEAN_MARKER) landCount++;
                }
            }
        }
        finally
        {
            handle.Complete();
            if (nativeNoise.IsCreated) nativeNoise.Dispose();
            if (nativeNodeRadius.IsCreated) nativeNodeRadius.Dispose();
            if (nativeCellRanges.IsCreated) nativeCellRanges.Dispose();
            if (nativeBucketItems.IsCreated) nativeBucketItems.Dispose();
            if (nativeShouldSeparate.IsCreated) nativeShouldSeparate.Dispose();
            if (nativeTerritory.IsCreated) nativeTerritory.Dispose();
        }

        float avgRadius = nodeCount > 0 ? radiusSum / nodeCount : 0f;
        Debug.Log($"[TilePartitioner] 땅 타일: {landCount}개 / 바다 타일: {mapArea - landCount}개 | 평균 반경: {avgRadius:F2}");

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }

    /// <summary>
    /// 모든 타일을 바다로 초기화
    /// </summary>
    private void InitializeOceanWorld()
    {
        for (int x = 0; x < _worldLogicData.TerrainSize.x; x++)
        {
            for (int y = 0; y < _worldLogicData.TerrainSize.y; y++)
            {
                _worldLogicData.TerritoryWorld[x, y] = OCEAN_MARKER;
            }
        }
    }

    /// <summary>
    /// 연결된 Region 쌍의 집합 생성
    /// </summary>
    private HashSet<(RegionData, RegionData)> BuildConnectedRegionsSet()
    {
        var connectedRegions = new HashSet<(RegionData, RegionData)>();

        foreach (var conn in _graphResult.NodeConnections)
        {
            if (conn.ParentNode.RegionData != conn.ChildNode.RegionData)
            {
                connectedRegions.Add((conn.ParentNode.RegionData, conn.ChildNode.RegionData));
                connectedRegions.Add((conn.ChildNode.RegionData, conn.ParentNode.RegionData));
            }
        }

        return connectedRegions;
    }
    #endregion

    #region Phase 4: Orphan Tile Cleanup
    /// <summary>
    /// 노이즈로 인해 발생한 고립된 섬(Orphan) 타일들을 주변 영토로 병합
    /// </summary>
    private async UniTask CleanUpOrphanTilesAsync()
    {
        int width = _worldLogicData.TerrainSize.x;
        int height = _worldLogicData.TerrainSize.y;
        int totalLength = width * height;

        NativeArray<int> readTerritory = new NativeArray<int>(totalLength, Allocator.TempJob);
        NativeArray<int> writeTerritory = new NativeArray<int>(totalLength, Allocator.TempJob);
        JobHandle handle = default;
        int cleanedCount = 0;

        try
        {
            int idx = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    readTerritory[idx++] = _worldLogicData.TerritoryWorld[x, y];
                }
            }

            var job = new CleanUpOrphanTilesJob
            {
                Width = width,
                Height = height,
                ReadTerritory = readTerritory,
                WriteTerritory = writeTerritory
            };

            handle = job.Schedule(totalLength, 64);

            while (!handle.IsCompleted)
            {
                await UniTask.Yield(_ct);
            }
            handle.Complete();

            idx = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int newOwner = writeTerritory[idx++];
                    if (_worldLogicData.TerritoryWorld[x, y] != newOwner)
                    {
                        _worldLogicData.TerritoryWorld[x, y] = newOwner;
                        cleanedCount++;
                    }
                }
            }
        }
        finally
        {
            handle.Complete();
            if (readTerritory.IsCreated) readTerritory.Dispose();
            if (writeTerritory.IsCreated) writeTerritory.Dispose();
        }

        Debug.Log($"[TerritoryBuilder] 알박기 타일 정화 완료: {cleanedCount}개 타일 수정됨");

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }

    #endregion

    #region Phase 5: Border and Ocean Processing
    private async UniTask ProcessBordersAsync()
    {
        int width = _worldLogicData.TerrainSize.x;
        int height = _worldLogicData.TerrainSize.y;
        int totalLength = width * height;

        NativeArray<float> nativeNoise = new NativeArray<float>(totalLength, Allocator.TempJob);
        NativeArray<int> nativeTerritory = new NativeArray<int>(totalLength, Allocator.TempJob);
        JobHandle handle = default;

        try
        {
            int idx = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    nativeNoise[idx] = _worldLogicData.NoiseWorld[x, y];
                    nativeTerritory[idx] = _worldLogicData.TerritoryWorld[x, y];
                    idx++;
                }
            }

            var job = new ApplyOceanEdgeJob
            {
                Width = width,
                Height = height,
                HalfWidth = width * 0.5f,
                HalfHeight = height * 0.5f,
                NoiseStrength = _partiSettings.noiseStrength,
                ThresholdLimit = 1f - _partiSettings.oceanThreshold,
                NoiseMap = nativeNoise,
                TerritoryMap = nativeTerritory
            };

            handle = job.Schedule(totalLength, 64);

            while (!handle.IsCompleted)
            {
                await UniTask.Yield(_ct);
            }
            handle.Complete();

            idx = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    _worldLogicData.TerritoryWorld[x, y] = nativeTerritory[idx++];
                }
            }
        }
        finally
        {
            handle.Complete();
            if (nativeNoise.IsCreated) nativeNoise.Dispose();
            if (nativeTerritory.IsCreated) nativeTerritory.Dispose();
        }

        // 노이즈/바다/고립 타일 정리가 끝난 뒤 보호하므로 이후 단계에서 육로가 다시 깎이지 않습니다.
        await PreserveRequiredConnectionsAsync();

        if (_partiSettings.borderWidth > 0)
        {
            await MarkBorderTilesAsync();
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }

    /// <summary>
    /// 연결선의 캡슐 영역과 지역 중심부를 보장합니다. 소유권은 연결 양 끝 지역에만 배분합니다.
    /// </summary>
    private async UniTask PreserveRequiredConnectionsAsync()
    {
        var nodes = _graphResult.Nodes;
        if (nodes == null || nodes.Count == 0) return;

        int width = _worldLogicData.TerrainSize.x;
        int height = _worldLogicData.TerrainSize.y;
        // 셀 중심의 오차를 더해 대각선 육로도 요청한 폭을 확보합니다.
        float radius = Mathf.Max(2, _partiSettings.minimumConnectionWidth) * 0.5f + Mathf.Sqrt(0.5f);
        var protectedSources = new int[width, height];
        var sources = new List<(int parent, int child)>();
        var indices = new Dictionary<Node, int>(nodes.Count);
        for (int i = 0; i < nodes.Count; i++) indices.Add(nodes[i], i);

        for (int i = 0; i < nodes.Count; i++)
            await PreserveConnectionAsync(nodes[i].Position, nodes[i].Position, i, i, radius, protectedSources, sources);

        foreach (var edge in _graphResult.NodeConnections)
        {
            _ct.ThrowIfCancellationRequested();
            if (!indices.TryGetValue(edge.ParentNode, out int parent) ||
                !indices.TryGetValue(edge.ChildNode, out int child)) continue;

            await PreserveConnectionAsync(edge.ParentNode.Position, edge.ChildNode.Position,
                parent, child, radius, protectedSources, sources);
        }

        await SeparateUnrelatedConnectionsAsync(indices, protectedSources);
    }

    private async UniTask SeparateUnrelatedConnectionsAsync(Dictionary<Node, int> indices, int[,] protectedSources)
    {
        var nodes = _graphResult.Nodes;
        var connected = new bool[nodes.Count, nodes.Count];
        foreach (var edge in _graphResult.NodeConnections)
        {
            if (!indices.TryGetValue(edge.ParentNode, out int parent) ||
                !indices.TryGetValue(edge.ChildNode, out int child)) continue;
            connected[parent, child] = connected[child, parent] = true;
        }

        int width = _worldLogicData.TerrainSize.x;
        int height = _worldLogicData.TerrainSize.y;
        int processed = 0;
        int batch = Mathf.Max(1, _partiSettings.batchSize);
        for (int x = 0; x < width; x++)
        {
            _ct.ThrowIfCancellationRequested();
            for (int y = 0; y < height; y++)
            {
                if (protectedSources[x, y] != 0)
                {
                    int owner = _worldLogicData.TerritoryWorld[x, y];
                    for (int d = 0; d < _dx8.Length; d++)
                    {
                        int nx = x + _dx8[d], ny = y + _dy8[d];
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                        int neighbor = _worldLogicData.TerritoryWorld[nx, ny];
                        if (neighbor < 0 || neighbor == owner || connected[owner, neighbor] ||
                            nodes[owner].RegionData == nodes[neighbor].RegionData) continue;

                        // 보호 영역 옆에 다른 분기의 땅이 남아 우회로가 생기지 않도록 바다 한 칸을 확보합니다.
                        if (protectedSources[nx, ny] != 0)
                            throw new InvalidOperationException($"필수 육로가 다른 분기와 겹칩니다: {owner}, {neighbor}");
                        _worldLogicData.TerritoryWorld[nx, ny] = OCEAN_MARKER;
                    }

                    if (++processed >= batch)
                    {
                        processed = 0;
                        await UniTask.Yield(_ct);
                    }
                }
            }
        }
    }

    private async UniTask PreserveConnectionAsync(Vector2 from, Vector2 to, int parent, int child,
        float radius, int[,] protectedSources, List<(int parent, int child)> sources)
    {
        int width = _worldLogicData.TerrainSize.x;
        int height = _worldLogicData.TerrainSize.y;
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(from.x, to.x) - radius));
        int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(from.x, to.x) + radius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(from.y, to.y) - radius));
        int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(from.y, to.y) + radius));
        Vector2 direction = to - from;
        float lengthSquared = direction.sqrMagnitude;
        float radiusSquared = radius * radius;
        int processed = 0;
        int batch = Mathf.Max(1, _partiSettings.batchSize);
        sources.Add((parent, child));
        int source = sources.Count;

        for (int x = minX; x <= maxX; x++)
        {
            _ct.ThrowIfCancellationRequested();
            for (int y = minY; y <= maxY; y++)
            {
                Vector2 point = new Vector2(x, y);
                float t = lengthSquared > 0f ? Mathf.Clamp01(Vector2.Dot(point - from, direction) / lengthSquared) : 0f;
                float distanceSquared = (point - (from + direction * t)).sqrMagnitude;
                if (distanceSquared <= radiusSquared)
                {
                    int owner = t < 0.5f ? parent : child;
                    int previousSource = protectedSources[x, y];
                    if (previousSource != 0)
                    {
                        var previous = sources[previousSource - 1];
                        // 분기 교차부는 공통 지역에 맡겨 자식 지역이 다른 육로의 폭을 빼앗지 않게 합니다.
                        if ((previous.parent == parent && previous.child == child) ||
                            (previous.parent == child && previous.child == parent)) owner = _worldLogicData.TerritoryWorld[x, y];
                        else if (previous.parent == parent || previous.parent == child) owner = previous.parent;
                        else if (previous.child == parent || previous.child == child) owner = previous.child;
                        else throw new InvalidOperationException("서로 연결되지 않은 필수 육로가 겹칩니다.");
                    }
                    else protectedSources[x, y] = source;

                    _worldLogicData.TerritoryWorld[x, y] = owner;
                }

                if (++processed >= batch)
                {
                    processed = 0;
                    await UniTask.Yield(_ct);
                }
            }
        }
    }

    private async UniTask MarkBorderTilesAsync()
    {
        int width = _worldLogicData.TerrainSize.x;
        int height = _worldLogicData.TerrainSize.y;
        int totalLength = width * height;

        _worldLogicData.BorderWorld = new int[width, height];

        NativeArray<int> nativeTerritory = new NativeArray<int>(totalLength, Allocator.TempJob);
        NativeArray<int> nativeBorder = new NativeArray<int>(totalLength, Allocator.TempJob);
        JobHandle handle = default;

        try
        {
            int idx = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    nativeTerritory[idx++] = _worldLogicData.TerritoryWorld[x, y];
                }
            }

            var job = new MarkBorderTilesJob
            {
                Width = width,
                Height = height,
                TerritoryMap = nativeTerritory,
                BorderMap = nativeBorder
            };

            handle = job.Schedule(totalLength, 64);

            while (!handle.IsCompleted)
            {
                await UniTask.Yield(_ct);
            }
            handle.Complete();

            idx = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    _worldLogicData.BorderWorld[x, y] = nativeBorder[idx++];
                }
            }
        }
        finally
        {
            handle.Complete();
            if (nativeTerritory.IsCreated) nativeTerritory.Dispose();
            if (nativeBorder.IsCreated) nativeBorder.Dispose();
        }
    }
    #endregion

    #region Phase 6: Assign Tiles to Regions
    /// <summary>
    /// 타일 데이터를 각 노드의 RegionData에 할당하여 소유 타일 목록 구축  
    /// </summary>
    /// <returns></returns>
    private async UniTask AssignOwnedTilesToRegionsAsync()
    {
        var nodes = _graphResult.Nodes;
        if (nodes == null) return;

        int nodeCount = nodes.Count;
        foreach (var node in nodes)
        {
            node.OwnedTiles.Clear();
        }

        int width = _worldLogicData.TerrainSize.x;
        int height = _worldLogicData.TerrainSize.y;

        for (int x = 0; x < width; x++)
        {
            _ct.ThrowIfCancellationRequested();
            for (int y = 0; y < height; y++)
            {
                int nodeIndex = _worldLogicData.TerritoryWorld[x, y];       // 타일에 할당된 노드 인덱스
                if (nodeIndex >= 0 && nodeIndex < nodeCount)
                {
                    nodes[nodeIndex].OwnedTiles.Add(new Vector2Int(x, y));
                }
            }
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }
    #endregion

    #region Phase 7: Influence Map Generation
    private async UniTask GenerateInfluenceMapAsync()
    {
        int width = _worldLogicData.TerrainSize.x;
        int height = _worldLogicData.TerrainSize.y;
        int totalLength = width * height;
        var nodes = _graphResult.Nodes;
        int nodeCount = nodes != null ? nodes.Count : 0;
        int safeNodeCount = Mathf.Max(1, nodeCount);

        // RegionData 참조를 정수 ID로 매핑하여 Burst Job에서 비교 가능하도록 변환
        var regionIdMap = new Dictionary<RegionData, int>( safeNodeCount );
        int nextRegionId = 1;

        NativeArray<int> nativeTerritory = new NativeArray<int>(totalLength, Allocator.TempJob);
        NativeArray<int> nativeNodeRegionIds = new NativeArray<int>(safeNodeCount, Allocator.TempJob);
        NativeArray<int> queueOcean = new NativeArray<int>(totalLength, Allocator.TempJob);
        NativeArray<int> queueEdge = new NativeArray<int>(totalLength, Allocator.TempJob);
        NativeArray<byte> visitedOcean = new NativeArray<byte>(totalLength, Allocator.TempJob);
        NativeArray<byte> visitedEdge = new NativeArray<byte>(totalLength, Allocator.TempJob);
        NativeArray<int> distanceToOcean = new NativeArray<int>(totalLength, Allocator.TempJob);
        NativeArray<float> distanceToEdge = new NativeArray<float>(totalLength, Allocator.TempJob);
        NativeArray<int> nodeMaxOceanDepth = new NativeArray<int>(safeNodeCount, Allocator.TempJob);
        NativeArray<float> nodeMaxEdgeDepth = new NativeArray<float>(safeNodeCount, Allocator.TempJob);
        NativeArray<InfluenceData> coastlineData = new NativeArray<InfluenceData>(totalLength, Allocator.TempJob);
        NativeArray<InfluenceData> regionEdgeData = new NativeArray<InfluenceData>(totalLength, Allocator.TempJob);

        JobHandle bfsHandle = default;
        JobHandle weightsHandle = default;

        try
        {
            for (int i = 0; i < nodeCount; i++)
            {
                RegionData region = nodes[i].RegionData;
                if (region == null)
                {
                    // RegionData가 null인 노드는 고유 음수 ID 부여 (자기 자신과만 동일 Region 취급)
                    nativeNodeRegionIds[i] = -(i + 1);
                }
                else
                {
                    if (!regionIdMap.TryGetValue(region, out int rId))
                    {
                        rId = nextRegionId++;
                        regionIdMap[region] = rId;
                    }
                    nativeNodeRegionIds[i] = rId;
                }
            }

            int idx = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    nativeTerritory[idx++] = _worldLogicData.TerritoryWorld[x, y];
                }
            }

            var bfsJob = new InfluenceBfsJob
            {
                Width = width,
                Height = height,
                TerritoryMap = nativeTerritory,
                NodeRegionIds = nativeNodeRegionIds,
                QueueOcean = queueOcean,
                QueueEdge = queueEdge,
                VisitedOcean = visitedOcean,
                VisitedEdge = visitedEdge,
                DistanceToOcean = distanceToOcean,
                DistanceToEdge = distanceToEdge,
                NodeMaxOceanDepth = nodeMaxOceanDepth,
                NodeMaxEdgeDepth = nodeMaxEdgeDepth
            };

            var weightsJob = new ComputeInfluenceWeightsJob
            {
                TerritoryMap = nativeTerritory,
                DistanceToOcean = distanceToOcean,
                DistanceToEdge = distanceToEdge,
                NodeMaxOceanDepth = nodeMaxOceanDepth,
                NodeMaxEdgeDepth = nodeMaxEdgeDepth,
                CoastlineData = coastlineData,
                RegionEdgeData = regionEdgeData
            };

            bfsHandle = bfsJob.Schedule();
            weightsHandle = weightsJob.Schedule(totalLength, 64, bfsHandle);

            while (!weightsHandle.IsCompleted)
            {
                await UniTask.Yield(_ct);
            }
            weightsHandle.Complete();

            idx = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    _worldLogicData.DistanceToOceanWorld[x, y] = distanceToOcean[idx];
                    _worldLogicData.CoastlineDataWorld[x, y] = coastlineData[idx];
                    _worldLogicData.RegionEdgeDataWorld[x, y] = regionEdgeData[idx];
                    idx++;
                }
            }
        }
        finally
        {
            weightsHandle.Complete();
            bfsHandle.Complete();
            if (nativeTerritory.IsCreated) nativeTerritory.Dispose();
            if (nativeNodeRegionIds.IsCreated) nativeNodeRegionIds.Dispose();
            if (queueOcean.IsCreated) queueOcean.Dispose();
            if (queueEdge.IsCreated) queueEdge.Dispose();
            if (visitedOcean.IsCreated) visitedOcean.Dispose();
            if (visitedEdge.IsCreated) visitedEdge.Dispose();
            if (distanceToOcean.IsCreated) distanceToOcean.Dispose();
            if (distanceToEdge.IsCreated) distanceToEdge.Dispose();
            if (nodeMaxOceanDepth.IsCreated) nodeMaxOceanDepth.Dispose();
            if (nodeMaxEdgeDepth.IsCreated) nodeMaxEdgeDepth.Dispose();
            if (coastlineData.IsCreated) coastlineData.Dispose();
            if (regionEdgeData.IsCreated) regionEdgeData.Dispose();
        }

        Debug.Log("[TerritoryBuilder] 영토 맞춤형 영향력 맵 생성 완료");
        if (_worldSettings.EnableStepByStep) await UniTask.Yield(_ct);
    }

    #endregion

    private void TryAssignStartRegionSpawnTile()
    {
        if (_worldSettings?.CurrentStory?.StartRegion == null) return;

        RegionData startRegion = _worldSettings.CurrentStory.StartRegion;
        Node startNode = _graphResult.Nodes.FirstOrDefault(n => n.RegionData == startRegion);
        if (startNode == null || startNode.OwnedTiles == null || startNode.OwnedTiles.Count == 0) return;

        long sumX = 0;
        long sumY = 0;
        foreach (Vector2Int tile in startNode.OwnedTiles)
        {
            sumX += tile.x;
            sumY += tile.y;
        }

        Vector2 center = new Vector2(
            (float)sumX / startNode.OwnedTiles.Count,
            (float)sumY / startNode.OwnedTiles.Count
        );

        Vector2Int closest = startNode.OwnedTiles[0];
        float bestDist = float.MaxValue;

        foreach (Vector2Int tile in startNode.OwnedTiles)
        {
            float dx = tile.x - center.x;
            float dy = tile.y - center.y;
            float dist = (dx * dx) + (dy * dy);
            if (dist < bestDist)
            {
                bestDist = dist;
                closest = tile;
            }
        }

        _worldLogicData.SpawnTile = closest;
        Debug.Log($"[TerritoryBuilder] StartRegion 스폰 타일 결정: ({closest.x}, {closest.y})");
    }
}
