using UnityEngine;

public interface IEatable
{
    FoodType FoodType { get; set; }

    /// <summary>신선도 (0~1, 1이 최신선)</summary>
    float Freshness { get; set; }

    /// <summary>초당 신선도 감소량</summary>
    float FreshnessDecayRate { get; set; }

    /// <summary>음식 섭취 시 버프/스탯 효과 (추후 버프 SO로 교체 예정)</summary>
    ItemDataSO Status { get; set; }
}

/// <summary>
/// 음식 런타임 데이터 클래스.
/// hungerRestore / healthRestore / egoRestore 는 SO에서 읽고,
/// 신선도는 런타임에서 감소 처리한다.
/// </summary>
[System.Serializable]
public class ItemData_Food : ItemData, IEatable
{
    public FoodType id;
    public float freshness = 1f;
    public float freshnessDecayRate = 0.005f;  // 기본값: 초당 0.5% 감소
    public ItemDataSO status;

    public ItemData_Food(ItemDataSO data) : base(data)
    {
        id = data.foodType;
        freshness = 1f;
    }

    FoodType IEatable.FoodType         { get => id;                 set => id = value; }
    float    IEatable.Freshness        { get => freshness;          set => freshness = Mathf.Clamp01(value); }
    float    IEatable.FreshnessDecayRate { get => freshnessDecayRate; set => freshnessDecayRate = value; }
    ItemDataSO IEatable.Status         { get => status;             set => status = value; }

    /// <summary>
    /// 신선도를 deltaTime만큼 감소시킨다.
    /// Player 업데이트 루프 또는 상태 전환 시 호출.
    /// </summary>
    public void UpdateFreshness(float deltaTime)
    {
        freshness = Mathf.Max(0f, freshness - freshnessDecayRate * deltaTime);
    }

    /// <summary>신선도 보정된 허기 회복량</summary>
    public float GetEffectiveHungerRestore() => data.hungerRestore * freshness;

    /// <summary>신선도 보정된 체력 회복량</summary>
    public float GetEffectiveHealthRestore() => data.healthRestore * freshness;

    /// <summary>신선도 보정된 Ego 회복량</summary>
    public float GetEffectiveEgoRestore()    => data.egoRestore * freshness;
}
