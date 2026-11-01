using System;
using System.Collections.Generic;

/// <summary>지역별 수분, 선택한 기간, 난수 상태를 저장하고 호스트 상태를 전송합니다.</summary>
[Serializable]
public sealed class WorldWeatherSaveData
{
    public int version = 1;
    public int seed;
    public int regionCount;
    public string generationId;
    public double clockSeconds;
    public List<RegionWeatherSaveData> regions = new();

    public static void Validate(WorldWeatherSaveData data)
    {
        if (data == null) return; // 날씨 도입 전 저장 파일과 호환합니다.
        if (data.version != 1 || data.regionCount < 0 || data.regionCount > 4096 ||
            data.regions == null || data.regions.Count != data.regionCount || !Finite(data.clockSeconds) ||
            data.clockSeconds < 0d || !Guid.TryParse(data.generationId, out _))
            throw new ArgumentException("월드 날씨 저장 정보가 올바르지 않습니다.");

        var indices = new HashSet<int>();
        foreach (RegionWeatherSaveData region in data.regions)
        {
            if (region == null || region.regionIndex < 0 || region.regionIndex >= data.regionCount ||
                !indices.Add(region.regionIndex) || region.precipitationKind < 0 || region.precipitationKind > 2 ||
                region.randomState == 0 || !Finite(region.moisture) || region.moisture < 0d || region.moisture > 100d ||
                !Finite(region.phaseStartSeconds) || region.phaseStartSeconds < 0d ||
                !Finite(region.lastUpdateSeconds) || region.lastUpdateSeconds < region.phaseStartSeconds ||
                region.lastUpdateSeconds > data.clockSeconds || !Finite(region.phaseDurationSeconds))
                throw new ArgumentException("지역 날씨 저장 정보가 올바르지 않습니다.");

            if (region.precipitationKind == 0)
            {
                if (region.isPrecipitating || region.moisture != 0d || region.phaseDurationSeconds != 0d)
                    throw new ArgumentException("강수가 없는 지역의 날씨 저장 정보가 올바르지 않습니다.");
                continue;
            }

            double end = region.phaseStartSeconds + region.phaseDurationSeconds;
            if (region.phaseDurationSeconds <= 0d || !Finite(end) || end <= region.phaseStartSeconds ||
                region.lastUpdateSeconds >= end)
                throw new ArgumentException("지역 날씨의 지속시간이 올바르지 않습니다.");
            double progress = (region.lastUpdateSeconds - region.phaseStartSeconds) / region.phaseDurationSeconds;
            double expected = 100d * (region.isPrecipitating ? 1d - progress : progress);
            if (Math.Abs(region.moisture - expected) > 0.00001d)
                throw new ArgumentException("지역 날씨의 수분과 시각이 일치하지 않습니다.");
        }
    }

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
