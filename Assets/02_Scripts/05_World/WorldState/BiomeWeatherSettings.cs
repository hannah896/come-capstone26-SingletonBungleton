using System;
using UnityEngine;

/// <summary>맑음/강수 상태에서 사용할 강수의 형태입니다.</summary>
public enum PrecipitationKind
{
    None = 0,
    Rain = 1,
    Snow = 2,
}

/// <summary>바이옴이 공유하는 날씨 설정입니다. 실제 수분량은 각 지역이 별도로 소유합니다.</summary>
[Serializable]
public sealed class BiomeWeatherSettings
{
    [Tooltip("None이면 이 바이옴에서는 강수가 발생하지 않습니다.")]
    public PrecipitationKind Precipitation;
    [Tooltip("강수가 그친 뒤 다음 강수까지의 최소 맑은 기간(게임 일).")]
    [Min(0f)] public float MinDryDays;
    [Min(0f)] public float MaxDryDays;
    [Tooltip("한 번의 비/눈이 지속되는 최소 기간(게임 일).")]
    [Min(0f)] public float MinWetDays;
    [Min(0f)] public float MaxWetDays;

    public bool IsEnabled => Precipitation == PrecipitationKind.Rain || Precipitation == PrecipitationKind.Snow;

    public void Validate()
    {
        if (!Enum.IsDefined(typeof(PrecipitationKind), Precipitation))
            throw new ArgumentException("정의되지 않은 강수 종류입니다.", nameof(Precipitation));
        if (!IsEnabled) return;

        if (!PositiveFinite(MinDryDays) || !PositiveFinite(MaxDryDays) || MinDryDays > MaxDryDays ||
            !PositiveFinite(MinWetDays) || !PositiveFinite(MaxWetDays) || MinWetDays > MaxWetDays)
            throw new ArgumentException("활성화한 날씨의 기간은 유한한 양수이며 최소값이 최대값 이하여야 합니다.");
    }

    /// <summary>런타임 계산이 공유된 바이옴 에셋을 수정하지 않도록 사본을 만듭니다.</summary>
    public BiomeWeatherSettings CloneValidated()
    {
        Validate();
        return new BiomeWeatherSettings
        {
            Precipitation = Precipitation,
            MinDryDays = MinDryDays,
            MaxDryDays = MaxDryDays,
            MinWetDays = MinWetDays,
            MaxWetDays = MaxWetDays,
        };
    }

    private static bool PositiveFinite(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
}
