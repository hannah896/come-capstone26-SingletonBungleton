using UnityEngine;

namespace World.WorldGraph.Helpers.LandformStrategies
{
    public enum LandformType
    {
        Default,
        Mountain,
        MountainRange,
        Highlands
    }

    public interface ILandformStrategy
    {
        float ModifyHeight(HeightContext ctx);
        
        // ★ 스무딩 로직 전체를 위임
        float SmoothHeight(SmoothingContext ctx); 
    }

    public struct HeightContext
    {
        public float BaseSampleX;
        public float BaseSampleY;
        public Vector2[] OctaveOffsets;
        public float TargetBaseHeight;
        public float MinHeight;
        public float DistToEdge;
        public float EdgeFade;
        public InfluenceData CoastlineData;
        public InfluenceData EdgeData;
        public NoiseParameters NoiseParams;
    }

    // ★ 스무딩에 필요한 데이터를 담은 Context
    public struct SmoothingContext
    {
        public int X;
        public int Y;
        public float[,] ReadMap;
        public int MapWidth;
        public int MapHeight;
    }
    #region DefaultStrategy
    public class DefaultStrategy : ILandformStrategy
    {
        // 자식 클래스에서 쓸 수 있도록 protected 권한 부여
        protected static readonly int[] _dx = { -1, 1, 0, 0, -1, -1, 1, 1 };
        protected static readonly int[] _dy = { 0, 0, -1, 1, -1, 1, -1, 1 };

        public virtual float ModifyHeight(HeightContext ctx)
        {
            float noiseSum = 0f;
            float maxWeight = 0f;
            float octaveWeight = 1f;
            float octaveFreq = 1f;
            float amplitude = ctx.NoiseParams.HeightVarianceBlocks;

            // HeightBuilder에 있던 옥타브 노이즈 루프를 여기서 실행
            for (int i = 0; i < ctx.NoiseParams.Octaves; i++)
            {
                float sampleX = ctx.BaseSampleX * octaveFreq + ctx.OctaveOffsets[i].x;
                float sampleY = ctx.BaseSampleY * octaveFreq + ctx.OctaveOffsets[i].y;
                float n = Mathf.PerlinNoise(sampleX, sampleY);

                if (amplitude >= 30f)
                {
                    n = 1f - Mathf.Abs(n * 2f - 1f);
                    n = n * n;      // n 이 가능한 값의 범위를 0~1로 압축하면서, 낮은 값은 더 낮게, 높은 값은 더 높게 만들어서 산맥의 극적인 굴곡을 강조
                }
                noiseSum += n * octaveWeight;
                maxWeight += octaveWeight;

                octaveWeight *= ctx.NoiseParams.Persistence;
                octaveFreq *= ctx.NoiseParams.Lacunarity;
            }

            float normalizedNoise = noiseSum / maxWeight;

            // 1. 노이즈(굴곡)가 경계선 근처에서 0으로 부드럽게 가라앉음
            float heightNoise = normalizedNoise * amplitude * ctx.EdgeFade;

            float rawFinalHeight = ctx.TargetBaseHeight + heightNoise;

            float finalHeight = Mathf.LerpUnclamped(ctx.MinHeight, rawFinalHeight, ctx.EdgeData.Weight);

            return finalHeight;
        }

        // ★ Default: 평원, 사막, 부드러운 언덕용 (균등한 블러 스무딩)
        public virtual float SmoothHeight(SmoothingContext ctx)
        {
            float currentHeight = ctx.ReadMap[ctx.X, ctx.Y];
            float sum = currentHeight;
            float weightSum = 1.0f; 

            for (int d = 0; d < 8; d++)
            {
                int nx = ctx.X + _dx[d];
                int ny = ctx.Y + _dy[d];

                if (nx >= 0 && nx < ctx.MapWidth && ny >= 0 && ny < ctx.MapHeight)
                {
                    float neighborHeight = ctx.ReadMap[nx, ny];
                    if (neighborHeight >= 0)
                    {
                        float weight = 1.0f; // 모두 균등하게 섞어 둥글게 깎음
                        sum += neighborHeight * weight;
                        weightSum += weight;
                    }
                }
            }
            return sum / weightSum;
        }
    }

    #endregion

    #region MountainStrategy

    public class MountainStrategy : DefaultStrategy
    {
        public override float ModifyHeight(HeightContext ctx)
        {
            // 1. 기본 산 높이
            float baseHeight = base.ModifyHeight(ctx);

            // =========================================================
            // ★ 중요: 지형 윤곽선(등고선) 찌그러뜨리기
            // =========================================================
            // 기본 산은 너무 매끈하기 때문에(직선 형태), 슬라이스하기 전에 일부러 울퉁불퉁한 덩어리들을 추가합니다.
            // 이렇게 하면 테라스의 외곽선이 직선이 아니라 반도처럼 툭 튀어나오거나 파이는 등 아주 자연스럽게 휘어집니다.
            float bumpFreq = 0.03f;
            float bumpNoise = (Mathf.PerlinNoise(ctx.BaseSampleX * bumpFreq + 12.3f, ctx.BaseSampleY * bumpFreq + 45.6f) * 2f - 1f);
            baseHeight += bumpNoise * 8.0f; 

            // 단차 높이는 고정 (지형이 찢어지지 않도록)
            float stepSize = 14.0f; 

            // =========================================================
            // ★ 중요: 평지 면적 비율을 '극단적'으로 조절
            // =========================================================
            // widthNoise에 따라 평지의 비율이 0% ~ 85%까지 널뛰게 합니다.
            // 평지 비율이 0%인 곳은 테라스가 아예 끊기고 그냥 절벽이 됩니다!
            float widthFreq = 0.025f;
            float widthNoise = Mathf.PerlinNoise(ctx.BaseSampleX * widthFreq + 78.9f, ctx.BaseSampleY * widthFreq + 12.3f);
            
            float flatArea = Mathf.Lerp(0.0f, 0.85f, widthNoise); 
            float cliffArea = Mathf.Max(0.05f, 1.0f - flatArea); // 0으로 나누어지는 것 방지

            // =========================================================
            // 계단화 연산
            // =========================================================
            float hClamp = baseHeight / stepSize;
            float stepIndex = Mathf.Floor(hClamp);
            float stepFraction = hClamp - stepIndex; // 0.0 ~ 1.0

            float sCurve = 0f;
            if (stepFraction <= flatArea)
            {
                // 평지(쉼터) 구간: 완전히 수평이면 그래픽이 어색하므로 3% 정도의 아주 미세한 오르막만 줍니다.
                if (flatArea > 0f)
                {
                    sCurve = (stepFraction / flatArea) * 0.03f;
                }
            }
            else
            {
                // 절벽 구간: 쉼터를 지나면 다음 층까지 급격하게 솟아오릅니다.
                float t = (stepFraction - flatArea) / cliffArea;
                sCurve = 0.03f + 0.97f * Mathf.SmoothStep(0f, 1f, t);
            }

            float terracedHeight = (stepIndex + sCurve) * stepSize;

            // =========================================================
            // 최종 블렌딩 (마스크)
            // =========================================================
            // 산 전체가 테라스가 되지 않도록, 테라스가 있는 구역과 그냥 뾰족한 암벽인 구역을 섞습니다.
            float maskFreq = 0.015f;
            float maskNoise = Mathf.PerlinNoise(ctx.BaseSampleX * maskFreq + 99.9f, ctx.BaseSampleY * maskFreq + 88.8f);
            float terraceMask = Mathf.SmoothStep(0.2f, 0.7f, maskNoise);

            float finalHeight = Mathf.Lerp(baseHeight, terracedHeight, terraceMask);

            return finalHeight;
        }

        public override float SmoothHeight(SmoothingContext ctx)
        {
            // 기존 침식 스무딩 코드는 유지
            float currentHeight = ctx.ReadMap[ctx.X, ctx.Y];
            float sum = currentHeight;
            float weightSum = 1.0f;

            for (int d = 0; d < 8; d++)
            {
                int nx = ctx.X + _dx[d];
                int ny = ctx.Y + _dy[d];

                if (nx >= 0 && nx < ctx.MapWidth && ny >= 0 && ny < ctx.MapHeight)
                {
                    float neighborHeight = ctx.ReadMap[nx, ny];
                    if (neighborHeight >= 0)
                    {
                        float weight = (neighborHeight < currentHeight) ? 3.0f : 0.2f;
                        sum += neighborHeight * weight;
                        weightSum += weight;
                    }
                }
            }
            return sum / weightSum;
        }
    }

    #endregion
    #region MountainRangeStrategy
    public class MountainRangeStrategy : MountainStrategy
    {
        // Mountain과 동일한 침식 스무딩 로직 사용
    }
    #endregion
    #region HighlandsStrategy
    public class HighlandsStrategy : DefaultStrategy
    {
        public override float ModifyHeight(HeightContext ctx)
        {
            float finalHeight = base.ModifyHeight(ctx);

            //TODO: 고원 추가 로직

            return finalHeight;
        }

        public override float SmoothHeight(SmoothingContext ctx)
        {
            return base.SmoothHeight(ctx);
        }
    }
    #endregion
}
