using UnityEngine;

/// <summary>
/// 음식 아이템
/// - 구운고기
/// </summary>


public class Item_Food : Item
{
    [Header("=== 음식 전용 ===")]
    public FoodType foodType = FoodType.None;

    [Tooltip("음식 신선도 (0~1)")]
    [Range(0f, 1f)]
    public float freshness = 1f;

    [Tooltip("초당 신선도 감소량")]
    public float freshnessDecayRate = 0.001f;

    private bool _isExpired = false;

    protected override void Init()
    {
        base.Init();
        if (itemData != null)
            foodType = itemData.foodType;
    }

    public override void Init(ItemDataSO data)
    {
        base.Init(data);
        foodType = itemData.foodType;
    }

    private void Update()
    {
        // 신선도 감소
        if (!_isExpired)
        {
            freshness = Mathf.Max(0f, freshness - freshnessDecayRate * Time.deltaTime);
            if (freshness <= 0f)
            {
                _isExpired = true;
                Debug.Log($"[음식] {itemData.itemName}이(가) 상했습니다!");
                // TODO: 아이콘 변경, 먹으면 페널티 적용
            }
        }
    }

    /// 음식 먹기 (플레이어가 인벤토리에서 사용 시 호출)
    public void Eat()
    {
        if (_isExpired)
        {
            Debug.Log($"[음식] {itemData.itemName}이(가) 상해서 먹을 수 없습니다!");
            // TODO: 상한 음식 먹으면 디버프 적용
            return;
        }

        Debug.Log($"[음식] {itemData.itemName} 섭취!");
        ApplySurvivalEffects();  // 부모 클래스의 배고픔/체력/정신력 회복

        int removed = RemoveStack(1);
        if (stackCount <= 0)
            Destroy(gameObject);
    }

    public bool IsExpired() => _isExpired;

    public override string ToString()
    {
        string info = base.ToString();
        info += $" [신선도: {freshness * 100f:F0}%]";
        if (_isExpired) info += "상함";
        return info;
    }
}