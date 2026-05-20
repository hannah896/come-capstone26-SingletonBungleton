using UnityEngine;

public interface IEatable
{
    // 음식 데이터 id
    public FoodType FoodType { get; set; }

    // 신선도 (0.0 ~ 1.0, 1.0이 가장 신선한 상태)
    public float Freshness { get; set; }

    // 신선도 감소 속도 (초당 감소량)
    public float FreshnessDecayRate { get; set; }  

    // 적용될 버프 효과 및 스텟 변화
    public ItemDataSO Status { get; set; }
}

/// <summary>
/// 요리 전용 아이템 클래스
/// </summary>
public class ItemData_Food : ItemData, IEatable
{
    public FoodType id;
    public float freshness;
    public float freshnessDecayRate;
    public ItemDataSO status;

    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="data"></param>
    /// <param name="count"></param>
    public ItemData_Food(ItemDataSO data, int count = 1) : base(data, count)
    {
    }

    FoodType IEatable.FoodType { get => id; set => id = value; }
    float IEatable.Freshness { get => freshness; set => freshness = value; }
    float IEatable.FreshnessDecayRate { get => freshnessDecayRate; set => freshnessDecayRate = value; }
    ItemDataSO IEatable.Status { get => status; set => status = value; }
}