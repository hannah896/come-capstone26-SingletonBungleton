using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace World.WorldGraph.Helpers.LandformJobs
{
    #region Job Structures (Burst Compatible)
    // 1. 노드의 지형 생성 데이터를 담는 가벼운 구조체
    public struct RegionGenData
    {
        public float TargetBaseHeight;
        public float Amplitude;
        public float Frequency;
        public int Octaves;
        public float Persistence;
        public float Lacunarity;
        public LandformType LandformType;
    }

    #region Height Generation Job
    // 2. 높이맵 생성 Job (기존 ModifyHeight 로직 대체)
    // FloatMode.Strict / FloatPrecision.High:
    // 기본 설정(FloatMode.Default)에서는 Burst가 곱셈+덧셈을 FMA로 합치거나 연산 순서를 재배열하고,
    // 에디터(JIT, 로컬 CPU 기준)와 빌드(AOT, 타깃 CPU 기준)의 결과가 미세하게 달라진다.
    // 그 미세한 차이가 높이/바이옴 임계값 비교를 뒤집어 같은 시드에서도 피어마다 다른 맵이 나온다.
    // 멀티에서 모든 피어가 같은 지형을 만들어야 하므로 재현 가능한 엄격 모드로 고정한다.
    [BurstCompile(FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.High)]
    public struct HeightGenerationJob : IJobParallelFor
    {
        public int MapWidth;
        public int MapHeight;
        public float OffsetX;
        public float OffsetY;
        public float MinHeight;
        public float UpliftEdgeFadeDistance;
        public int MaxOctaves;

        [ReadOnly] public NativeArray<int> TerritoryMap;
        [ReadOnly] public NativeArray<RegionGenData> NodeRegionData;
        [ReadOnly] public NativeArray<float2> NodeOctaveOffsets;
        [ReadOnly] public NativeArray<float2> EdgeDataMap;

        [WriteOnly] public NativeArray<float> HeightMap;

        private struct HeightKernelContext
        {
            public float BaseSampleX;
            public float BaseSampleY;
            public float MinHeight;
            public float EdgeFade;
            public float EdgeWeight;
            public RegionGenData RegionData;
            public int OwnerNode;
            public int MaxOctaves;
        }

        public void Execute(int index)
        {
            int ownerNode = TerritoryMap[index];
            if (ownerNode < 0)
            {
                return;
            }

            int x = index % MapWidth;
            int y = index / MapWidth;

            RegionGenData regionData = NodeRegionData[ownerNode];
            float2 edgeData = EdgeDataMap[index];
            float distToEdge = edgeData.x;
            float edgeWeight = edgeData.y;

            float edgeFade = math.smoothstep(0f, 1f, math.clamp(distToEdge / UpliftEdgeFadeDistance, 0f, 1f));
            float baseSampleX = (x + OffsetX) * regionData.Frequency;
            float baseSampleY = (y + OffsetY) * regionData.Frequency;

            HeightKernelContext ctx = new HeightKernelContext
            {
                BaseSampleX = baseSampleX,
                BaseSampleY = baseSampleY,
                MinHeight = MinHeight,
                EdgeFade = edgeFade,
                EdgeWeight = edgeWeight,
                RegionData = regionData,
                OwnerNode = ownerNode,
                MaxOctaves = MaxOctaves
            };

            float finalHeight = ApplyHeightKernel(ctx);
            HeightMap[index] = math.max(MinHeight, finalHeight);
        }

        private float ApplyHeightKernel(HeightKernelContext ctx)
        {
            switch (ctx.RegionData.LandformType)
            {
                case LandformType.Mountain:
                case LandformType.MountainRange:
                    return ComputeMountainHeight(ctx);

                case LandformType.Highlands:
                    return ComputeHighlandsHeight(ctx);

                default:
                    return ComputeDefaultHeight(ctx);
            }
        }

        private float ComputeDefaultHeight(HeightKernelContext ctx)
        {
            float noiseSum = 0f;
            float maxWeight = 0f;
            float octaveWeight = 1f;
            float octaveFreq = 1f;

            for (int i = 0; i < ctx.RegionData.Octaves; i++)
            {
                float2 offset = NodeOctaveOffsets[ctx.OwnerNode * ctx.MaxOctaves + i];
                float sampleX = ctx.BaseSampleX * octaveFreq + offset.x;
                float sampleY = ctx.BaseSampleY * octaveFreq + offset.y;

                float n = noise.cnoise(new float2(sampleX, sampleY));
                n = (n + 1f) * 0.5f;

                if (ctx.RegionData.Amplitude >= 30f)
                {
                    n = 1f - math.abs(n * 2f - 1f);
                    n = n * n;
                }

                noiseSum += n * octaveWeight;
                maxWeight += octaveWeight;

                octaveWeight *= ctx.RegionData.Persistence;
                octaveFreq *= ctx.RegionData.Lacunarity;
            }

            float normalizedNoise = noiseSum / maxWeight;
            float heightNoise = normalizedNoise * ctx.RegionData.Amplitude * ctx.EdgeFade;
            float rawFinalHeight = ctx.RegionData.TargetBaseHeight + heightNoise;

            return math.lerp(ctx.MinHeight, rawFinalHeight, ctx.EdgeWeight);
        }

        private float ComputeMountainHeight(HeightKernelContext ctx)
        {
            float finalHeight = ComputeDefaultHeight(ctx);

            float bumpFreq = 0.03f;
            float bumpNoise = noise.cnoise(new float2(ctx.BaseSampleX * bumpFreq + 12.3f, ctx.BaseSampleY * bumpFreq + 45.6f));
            finalHeight += bumpNoise * 8.0f;

            float stepSize = 14.0f;
            float widthFreq = 0.025f;
            float widthNoise = noise.cnoise(new float2(ctx.BaseSampleX * widthFreq + 78.9f, ctx.BaseSampleY * widthFreq + 12.3f));
            widthNoise = (widthNoise + 1f) * 0.5f;

            float flatArea = math.lerp(0.0f, 0.85f, widthNoise);
            float cliffArea = math.max(0.05f, 1.0f - flatArea);

            float hClamp = finalHeight / stepSize;
            float stepIndex = math.floor(hClamp);
            float stepFraction = hClamp - stepIndex;

            float sCurve = 0f;
            if (stepFraction <= flatArea)
            {
                if (flatArea > 0f)
                {
                    sCurve = (stepFraction / flatArea) * 0.03f;
                }
            }
            else
            {
                float t = (stepFraction - flatArea) / cliffArea;
                sCurve = 0.03f + 0.97f * math.smoothstep(0f, 1f, t);
            }

            float terracedHeight = (stepIndex + sCurve) * stepSize;
            float maskFreq = 0.015f;
            float maskNoise = noise.cnoise(new float2(ctx.BaseSampleX * maskFreq + 99.9f, ctx.BaseSampleY * maskFreq + 88.8f));
            maskNoise = (maskNoise + 1f) * 0.5f;
            float terraceMask = math.smoothstep(0.2f, 0.7f, maskNoise);

            return math.lerp(finalHeight, terracedHeight, terraceMask);
        }

        private float ComputeHighlandsHeight(HeightKernelContext ctx)
        {
            // 현재는 기본 로직 재사용. 필요 시 고원 전용 커널 확장.
            return ComputeDefaultHeight(ctx);
        }
    }
    #endregion

    #region Smoothing Job

    // 시드 동일 = 결과 동일을 보장하기 위해 엄격 모드 고정 (HeightGenerationJob 주석 참고)
    [BurstCompile(FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.High)]
    public struct HeightSmoothingJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> ReadMap;
        [ReadOnly] public NativeArray<int> TerritoryMap;
        [ReadOnly] public NativeArray<LandformType> NodeLandformTypes;
        [WriteOnly] public NativeArray<float> WriteMap;

        public int MapWidth;
        public int MapHeight;

        private struct SmoothingKernelContext
        {
            public int X;
            public int Y;
            public float CurrentHeight;
            public LandformType LandformType;
        }

        public void Execute(int index)
        {
            int ownerNode = TerritoryMap[index];
            float currentHeight = ReadMap[index];

            if (ownerNode < 0)
            {
                WriteMap[index] = currentHeight;
                return;
            }

            int x = index % MapWidth;
            int y = index / MapWidth;

            SmoothingKernelContext ctx = new SmoothingKernelContext
            {
                X = x,
                Y = y,
                CurrentHeight = currentHeight,
                LandformType = NodeLandformTypes[ownerNode]
            };

            WriteMap[index] = ApplySmoothingKernel(ctx);
        }

        private float ApplySmoothingKernel(SmoothingKernelContext ctx)
        {
            switch (ctx.LandformType)
            {
                case LandformType.Mountain:
                case LandformType.MountainRange:
                    return SmoothMountain(ctx);

                case LandformType.Highlands:
                    return SmoothHighlands(ctx);

                default:
                    return SmoothDefault(ctx);
            }
        }

        private float SmoothDefault(SmoothingKernelContext ctx)
        {
            float sum = ctx.CurrentHeight;
            float weightSum = 1.0f;

            AddNeighborDefault(ctx.X - 1, ctx.Y, ref sum, ref weightSum);
            AddNeighborDefault(ctx.X + 1, ctx.Y, ref sum, ref weightSum);
            AddNeighborDefault(ctx.X, ctx.Y - 1, ref sum, ref weightSum);
            AddNeighborDefault(ctx.X, ctx.Y + 1, ref sum, ref weightSum);

            AddNeighborDefault(ctx.X - 1, ctx.Y - 1, ref sum, ref weightSum);
            AddNeighborDefault(ctx.X - 1, ctx.Y + 1, ref sum, ref weightSum);
            AddNeighborDefault(ctx.X + 1, ctx.Y - 1, ref sum, ref weightSum);
            AddNeighborDefault(ctx.X + 1, ctx.Y + 1, ref sum, ref weightSum);

            return sum / weightSum;
        }

        private float SmoothMountain(SmoothingKernelContext ctx)
        {
            float sum = ctx.CurrentHeight;
            float weightSum = 1.0f;

            AddNeighborMountain(ctx.X - 1, ctx.Y, ctx.CurrentHeight, ref sum, ref weightSum);
            AddNeighborMountain(ctx.X + 1, ctx.Y, ctx.CurrentHeight, ref sum, ref weightSum);
            AddNeighborMountain(ctx.X, ctx.Y - 1, ctx.CurrentHeight, ref sum, ref weightSum);
            AddNeighborMountain(ctx.X, ctx.Y + 1, ctx.CurrentHeight, ref sum, ref weightSum);

            AddNeighborMountain(ctx.X - 1, ctx.Y - 1, ctx.CurrentHeight, ref sum, ref weightSum);
            AddNeighborMountain(ctx.X - 1, ctx.Y + 1, ctx.CurrentHeight, ref sum, ref weightSum);
            AddNeighborMountain(ctx.X + 1, ctx.Y - 1, ctx.CurrentHeight, ref sum, ref weightSum);
            AddNeighborMountain(ctx.X + 1, ctx.Y + 1, ctx.CurrentHeight, ref sum, ref weightSum);

            return sum / weightSum;
        }

        private float SmoothHighlands(SmoothingKernelContext ctx)
        {
            // 현재는 기본 스무딩 재사용
            return SmoothDefault(ctx);
        }

      

        private void AddNeighborDefault(int nx, int ny, ref float sum, ref float weightSum)
        {
            if (nx < 0 || nx >= MapWidth || ny < 0 || ny >= MapHeight) return;
            float neighborHeight = ReadMap[ny * MapWidth + nx];
            if (neighborHeight < 0f) return;

            sum += neighborHeight; // weight 곱셈조차 생략 가능
            weightSum += 1.0f;
        }

        // 🔴 산맥용: 산맥 침식 공식만 계산함 (if isMountain 검사 생략)
        private void AddNeighborMountain(int nx, int ny, float currentHeight, ref float sum, ref float weightSum)
        {
            if (nx < 0 || nx >= MapWidth || ny < 0 || ny >= MapHeight) return;
            float neighborHeight = ReadMap[ny * MapWidth + nx];
            if (neighborHeight < 0f) return;

            // isMountain 검사 없이 바로 산맥용 가중치 계산
            float weight = (neighborHeight < currentHeight) ? 3.0f : 0.2f;
            sum += neighborHeight * weight;
            weightSum += weight;
        }
        #endregion
    }
    #endregion

}
