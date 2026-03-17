using UnityEngine;

namespace LandformStrategys
{
    public class DefaultStrategy : ILandformStrategy
    {
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
                    n = n * n;
                }

                noiseSum += n * octaveWeight;
                maxWeight += octaveWeight;

                octaveWeight *= ctx.NoiseParams.Persistence;
                octaveFreq *= ctx.NoiseParams.Lacunarity;
            }

            float normalizedNoise = noiseSum / maxWeight;

            float noiseValue = (normalizedNoise * 2f) - 1f; // -1에서 1 사이로 변환

            // 기본 높이 적용 및 가장자리 감쇠, 전역 영향력 적용
            float heightNoise = noiseValue * amplitude * ctx.EdgeFade;
            float rawFinalHeight = ctx.TargetBaseHeight + heightNoise;

            float finalHeight = Mathf.Lerp(ctx.MinHeight, rawFinalHeight, ctx.CoastlineInfluence);

            return finalHeight;
        }
    }

    public class MountainStrategy : DefaultStrategy
    {
        public override float ModifyHeight(HeightContext ctx)
        {
            float currentHeight = base.ModifyHeight(ctx);

            float terraceStep = 8f;         // 계단 한 칸의 높이 (값이 클수록 거대한 층이 생김)
            float terraceSharpness = 4.0f;  // 계단 모서리의 날카로움 (값이 클수록 직각 절벽에 가까워짐)

            float h = currentHeight / terraceStep;     // 높이를 계단 간격으로 나눠서 몇 층인지 계산
            float currentStep = Mathf.Floor(h);      // 정수부 (현재 층수)
            float fractionalPart = h - currentStep;  // 소수부 (층과 층 사이의 위치 0.0 ~ 1.0)

            // 소수부에 곡선(Smooth)을 주어 모서리가 살짝 둥근 계단을 만듭니다.
            float smoothFraction = Mathf.Clamp01((fractionalPart - 0.5f) * terraceSharpness + 0.5f);

            // 최종 높이를 다시 계단식으로 조립
            float finalHeight = (currentStep + smoothFraction) * terraceStep;

            return finalHeight;
        }
    }

    public class HighlandsStrategy : DefaultStrategy
    {
        public override float ModifyHeight(HeightContext ctx)
        {
            float currentHeight = base.ModifyHeight(ctx);

            //TODO: RegionEdgeInfluenceWorld 활용하여 가장자리에서 산맥 지형이 만들어지도록 추가

            return currentHeight;
        }
    }


}
