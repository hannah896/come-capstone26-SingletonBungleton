using System;

/// <summary>지역 하나의 잠재 수분과 날씨를 월드 절대 시간으로 계산하는 모델입니다.</summary>
public sealed class RegionWeatherState
{
    public const double MoistureCapacity = 100d;
    private const int MaxTransitionsPerAdvance = 1000000;
    private readonly BiomeWeatherSettings _settings;
    private readonly double _secondsPerDay;
    private uint _randomState;

    public int RegionIndex { get; }
    public PrecipitationKind Kind => _settings.Precipitation;
    public bool IsPrecipitating { get; private set; }
    public double Moisture { get; private set; }
    public double PhaseStartSeconds { get; private set; }
    public double PhaseDurationSeconds { get; private set; }
    public double LastUpdateSeconds { get; private set; }
    public uint RandomState => _randomState;
    public double MoistureRatePerDay => _settings.IsEnabled
        ? (IsPrecipitating ? -1d : 1d) * MoistureCapacity * _secondsPerDay / PhaseDurationSeconds
        : 0d;

    public RegionWeatherState(int regionIndex, BiomeWeatherSettings settings, uint seed,
        double startSeconds, double secondsPerDay)
    {
        if (regionIndex < 0) throw new ArgumentOutOfRangeException(nameof(regionIndex));
        if (!NonNegativeFinite(startSeconds)) throw new ArgumentOutOfRangeException(nameof(startSeconds));
        if (!PositiveFinite(secondsPerDay)) throw new ArgumentOutOfRangeException(nameof(secondsPerDay));

        RegionIndex = regionIndex;
        _settings = (settings ?? new BiomeWeatherSettings()).CloneValidated();
        _secondsPerDay = secondsPerDay;
        _randomState = MixSeed(seed, regionIndex);
        PhaseStartSeconds = LastUpdateSeconds = startSeconds;
        PhaseDurationSeconds = _settings.IsEnabled ? NextDuration(false) : 0d;
        EnsurePhaseCanAdvance();
    }

    /// <summary>중간 상태를 모두 통과해 지정 시각까지 진행합니다. 하나라도 전환되면 true입니다.</summary>
    public bool AdvanceTo(double worldSeconds)
    {
        if (!NonNegativeFinite(worldSeconds) || worldSeconds < LastUpdateSeconds)
            throw new ArgumentOutOfRangeException(nameof(worldSeconds), "날씨는 현재 시각보다 과거로 진행할 수 없습니다.");
        if (worldSeconds == LastUpdateSeconds) return false;

        if (!_settings.IsEnabled)
        {
            LastUpdateSeconds = worldSeconds;
            return false;
        }

        bool changed = false;
        int transitions = 0;
        while (worldSeconds >= PhaseStartSeconds + PhaseDurationSeconds)
        {
            if (++transitions > MaxTransitionsPerAdvance)
                throw new InvalidOperationException("한 번에 처리할 수 있는 날씨 전환 횟수를 초과했습니다.");

            // 남은 프레임 시간이 아니라 종료 절대 시각에서 다음 단계를 시작합니다.
            PhaseStartSeconds += PhaseDurationSeconds;
            IsPrecipitating = !IsPrecipitating;
            PhaseDurationSeconds = NextDuration(IsPrecipitating);
            EnsurePhaseCanAdvance();
            changed = true;
        }

        LastUpdateSeconds = worldSeconds;
        Moisture = CalculateMoisture(IsPrecipitating, PhaseStartSeconds, PhaseDurationSeconds, worldSeconds);
        return changed;
    }

    public RegionWeatherSaveData Capture() => new RegionWeatherSaveData
    {
        regionIndex = RegionIndex,
        precipitationKind = (int)Kind,
        isPrecipitating = IsPrecipitating,
        moisture = Moisture,
        phaseStartSeconds = PhaseStartSeconds,
        phaseDurationSeconds = PhaseDurationSeconds,
        lastUpdateSeconds = LastUpdateSeconds,
        randomState = _randomState,
    };

    /// <summary>검증을 마친 저장 상태와 난수 상태를 한 번에 복원합니다.</summary>
    public void Restore(RegionWeatherSaveData data)
    {
        ValidateSnapshot(data);
        IsPrecipitating = data.isPrecipitating;
        Moisture = data.moisture;
        PhaseStartSeconds = data.phaseStartSeconds;
        PhaseDurationSeconds = data.phaseDurationSeconds;
        LastUpdateSeconds = data.lastUpdateSeconds;
        _randomState = data.randomState;
    }

    public void ValidateSnapshot(RegionWeatherSaveData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.regionIndex != RegionIndex || data.precipitationKind != (int)Kind || data.randomState == 0 ||
            !NonNegativeFinite(data.phaseStartSeconds) || !NonNegativeFinite(data.lastUpdateSeconds) ||
            data.lastUpdateSeconds < data.phaseStartSeconds || !NonNegativeFinite(data.moisture) ||
            data.moisture > MoistureCapacity)
            throw new ArgumentException("지역 날씨 저장 정보가 올바르지 않습니다.", nameof(data));

        if (!_settings.IsEnabled)
        {
            if (data.isPrecipitating || data.moisture != 0d || data.phaseDurationSeconds != 0d)
                throw new ArgumentException("강수가 비활성화된 지역에 강수 상태를 복원할 수 없습니다.", nameof(data));
            return;
        }

        double end = data.phaseStartSeconds + data.phaseDurationSeconds;
        if (!PositiveFinite(data.phaseDurationSeconds) || !PositiveFinite(end) || end <= data.phaseStartSeconds ||
            data.lastUpdateSeconds >= end)
            throw new ArgumentException("지역 날씨의 단계 시각이 올바르지 않습니다.", nameof(data));

        double expected = CalculateMoisture(data.isPrecipitating, data.phaseStartSeconds,
            data.phaseDurationSeconds, data.lastUpdateSeconds);
        if (Math.Abs(expected - data.moisture) > 0.00001d)
            throw new ArgumentException("지역 날씨의 수분량과 진행 시각이 일치하지 않습니다.", nameof(data));
    }

    private double NextDuration(bool isPrecipitating)
    {
        float min = isPrecipitating ? _settings.MinWetDays : _settings.MinDryDays;
        float max = isPrecipitating ? _settings.MaxWetDays : _settings.MaxDryDays;
        uint value = _randomState;
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        _randomState = value;
        // 매 단계의 기간을 균등 추첨합니다. 수분 속도를 균등 추첨하지 않습니다.
        return (min + (max - (double)min) * (value / (double)uint.MaxValue)) * _secondsPerDay;
    }

    private void EnsurePhaseCanAdvance()
    {
        if (_settings.IsEnabled && (!PositiveFinite(PhaseDurationSeconds) ||
            !PositiveFinite(PhaseStartSeconds + PhaseDurationSeconds) ||
            PhaseStartSeconds + PhaseDurationSeconds <= PhaseStartSeconds))
            throw new ArgumentException("날씨 기간을 현재 월드 시각에서 표현할 수 없습니다.");
    }

    private static double CalculateMoisture(bool raining, double start, double duration, double now)
    {
        double progress = Math.Max(0d, Math.Min(1d, (now - start) / duration));
        return MoistureCapacity * (raining ? 1d - progress : progress);
    }

    private static uint MixSeed(uint seed, int regionIndex)
    {
        unchecked
        {
            uint value = seed ^ ((uint)regionIndex + 1u) * 0x9e3779b9u;
            value = (value ^ (value >> 16)) * 0x85ebca6bu;
            value = (value ^ (value >> 13)) * 0xc2b2ae35u;
            value ^= value >> 16;
            return value == 0 ? 0x6d2b79f5u : value;
        }
    }

    private static bool PositiveFinite(double value) => value > 0d && !double.IsNaN(value) && !double.IsInfinity(value);
    private static bool NonNegativeFinite(double value) => value >= 0d && !double.IsNaN(value) && !double.IsInfinity(value);
}

/// <summary>세션의 Fusion 객체를 포함하지 않는 지역 날씨 저장 데이터입니다.</summary>
[Serializable]
public sealed class RegionWeatherSaveData
{
    public int regionIndex;
    public int precipitationKind;
    public bool isPrecipitating;
    public double moisture;
    public double phaseStartSeconds;
    public double phaseDurationSeconds;
    public double lastUpdateSeconds;
    public uint randomState;
}
