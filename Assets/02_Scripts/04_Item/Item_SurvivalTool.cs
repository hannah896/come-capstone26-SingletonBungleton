using UnityEngine;

/// <summary>
/// 생존도구 아이템 클래스
/// - 조끼 (방어)
/// - 도끼 (벌목)
/// - 곡괭이 (채굴)
/// - 횃불 (조명)
/// </summary>


public class Item_SurvivalTool : Item, IEquipable
{
    [Header("=== 생존도구 전용 속성 ===")]
    [Tooltip("생존도구 세부 타입")]
    public SurvivalToolType survivalToolType = SurvivalToolType.None;

    // IEquipable 인터페이스 구현
    private int _currentDurability;
    public int CurrentDurability
    {
        get => _currentDurability;
        set => _currentDurability = value;
    }

    [Header("=== 횃불 전용 ===")]
    [Tooltip("남은 연료 (횃불 전용)")]
    public float remainingFuel = 100f;

    [Tooltip("최대 연료")]
    public float maxFuel = 100f;

    [Tooltip("초당 연료 소모량")]
    public float fuelConsumptionRate = 1f;

    private bool _isLit = false; // 횃불이 켜져있는지

    protected override void Init()
    {
        base.Init();

        // 내구도 초기화
        if (itemData.hasDurability)
        {
            _currentDurability = (int)itemData.maxDurability;
        }
    }

    protected override void Init(ItemDataSO data)
    {
        base.Init(data);

        if (itemData.hasDurability)
        {
            _currentDurability = (int)itemData.maxDurability;
        }
    }

    private void Update()
    {
        // 횃불이 켜져있으면 연료 소모
        if (survivalToolType == SurvivalToolType.Torch && _isLit)
        {
            ConsumeFuel(fuelConsumptionRate * Time.deltaTime);
        }
    }


//도구 사용
    public void Use()
    {
        switch (survivalToolType)
        {
            case SurvivalToolType.Axe:
                ChopTree();
                break;

            case SurvivalToolType.Pickaxe:
                MineOre();
                break;

            case SurvivalToolType.Torch:
                ToggleTorch();
                break;

            default:
                Debug.Log($"{itemData.itemName} 사용!");
                break;
        }
    }

//도끼
    private void ChopTree()
    {
        Debug.Log($"{itemData.itemName}으로 나무 벌목!");

        // TODO: 실제 나무 오브젝트와 상호작용
        // 채집 속도: itemData.harvestSpeedMultiplier

        UseDurability(1); // 내구도 1 감소
    }

//곡괭이
    private void MineOre()
    {
        Debug.Log($"{itemData.itemName}으로 광물 채굴!");

        // TODO: 실제 광물 오브젝트와 상호작용

        UseDurability(1);
    }

//횃불 연료 소모
    private void ToggleTorch()
    {
        if (remainingFuel <= 0)
        {
            Debug.Log("연료가 부족합니다!");
            _isLit = false;
            return;
        }

        _isLit = !_isLit;
        Debug.Log($"횃불 {(_isLit ? "켜짐" : "꺼짐")}!");

        // TODO: 라이트 컴포넌트 On/Off
    }

    private void ConsumeFuel(float amount)
    {
        remainingFuel -= amount;

        if (remainingFuel <= 0)
        {
            remainingFuel = 0;
            _isLit = false;
            Debug.Log("횃불의 연료가 다 떨어졌습니다!");
        }
    }

//내구도 사용
    public void UseDurability(int amount = 1)
    {
        if (!itemData.hasDurability) return;

        _currentDurability = Mathf.Max(0, _currentDurability - amount);

        if (_currentDurability <= 0)
        {
            OnToolBroken();
        }
    }


//도구 부서졌을 때
    private void OnToolBroken()
    {
        Debug.Log($"{itemData.itemName}이(가) 부서졌습니다!");
        Destroy(gameObject);
    }

//내구도 회복
    public void Repair(int amount)
    {
        if (!itemData.hasDurability) return;

        _currentDurability += amount;
        _currentDurability = Mathf.Min(_currentDurability, (int)itemData.maxDurability);

        Debug.Log($"{itemData.itemName} 수리! (현재: {_currentDurability}/{itemData.maxDurability})");
    }

//내구도 퍼센트
    public float GetDurabilityPercent()
    {
        if (!itemData.hasDurability) return 1f;
        return _currentDurability / itemData.maxDurability;
    }

//UI
    public override string ToString()
    {
        string baseInfo = base.ToString();

        // 내구도 표시
        if (itemData.hasDurability)
        {
            baseInfo += $" [내구도: {_currentDurability}/{itemData.maxDurability}]";
        }

        // 횃불 연료 표시
        if (survivalToolType == SurvivalToolType.Torch)
        {
            baseInfo += $" [연료: {remainingFuel:F0}%]";
        }

        return baseInfo;
    }
}