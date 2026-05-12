using UnityEngine;

/// <summary>
/// 생존도구 아이템 클래스
/// - 도끼
/// - 곡괭이
/// - 횃불
/// </summary>


public class Item_SurvivalTool : Item, IEquipable
{
    [Header("=== 생존도구 전용 ===")]
    public SurvivalToolType survivalToolType = SurvivalToolType.None;

    // ─── 내구도 ───
    private int _currentDurability;
    public int CurrentDurability
    {
        get => _currentDurability;
        set => _currentDurability = Mathf.Clamp(value, 0, (int)itemData.maxDurability);
    }

    [Header("=== 횃불 전용 ===")]
    [Tooltip("횃불 Light 컴포넌트 연결")]
    public Light torchLight;

    private bool _isLit = false;


    #region 초기화

    protected override void Init()
    {
        base.Init();
        if (itemData != null && itemData.hasDurability)
            _currentDurability = (int)itemData.maxDurability;

        if (itemData != null)
            survivalToolType = itemData.survivalToolType;

        // 시작 시 횃불 꺼두기
        if (survivalToolType == SurvivalToolType.Torch && torchLight != null)
            torchLight.enabled = false;
    }

    public override void Init(ItemDataSO data)
    {
        base.Init(data);
        if (itemData.hasDurability)
            _currentDurability = (int)itemData.maxDurability;

        survivalToolType = itemData.survivalToolType;
    }
    #endregion



    #region IEquipable 구현
    public void Equip()
    {
        Debug.Log($"[도구] {itemData.itemName} 장착!");

        if (survivalToolType == SurvivalToolType.Torch)
            SetTorchLight(true);
    }

    public void Unequip()
    {
        Debug.Log($"[도구] {itemData.itemName} 해제!");

        if (survivalToolType == SurvivalToolType.Torch)
            SetTorchLight(false);
    }

    public void UseDurability(int amount = 1)
    {
        if (!itemData.hasDurability) return;

        _currentDurability = Mathf.Max(0, _currentDurability - amount);
        Debug.Log($"[도구] 내구도 {_currentDurability}/{itemData.maxDurability}");

        if (_currentDurability <= 0)
            OnToolBroken();
    }

    public void Repair(int amount)
    {
        if (!itemData.hasDurability) return;

        _currentDurability = Mathf.Min(_currentDurability + amount, (int)itemData.maxDurability);
        Debug.Log($"[도구] {itemData.itemName} 수리! ({_currentDurability}/{itemData.maxDurability})");
    }

    public float GetDurabilityPercent()
    {
        if (!itemData.hasDurability) return 1f;
        return (float)_currentDurability / itemData.maxDurability;
    }
    #endregion



    #region 도구 사용
    public void Use()
    {
        if (itemData.hasDurability && _currentDurability <= 0)
        {
            Debug.Log($"[도구] {itemData.itemName}이(가) 부서져서 사용할 수 없습니다!");
            return;
        }

        switch (survivalToolType)
        {
            case SurvivalToolType.Axe: ChopTree(); break;
            case SurvivalToolType.Pickaxe: MineOre(); break;
            default: Debug.Log($"[도구] {itemData.itemName} 사용!"); break;
        }
    }

    private void ChopTree()
    {
        Debug.Log($"[도끼] 나무 벌목!");
        // TODO: 나무 오브젝트에 Raycast → IHarvestable.Harvest(this) 호출
        UseDurability(1);
    }

    private void MineOre()
    {
        Debug.Log($"[곡괭이] 광물 채굴!");
        // TODO: 광물 오브젝트에 Raycast → IHarvestable.Harvest(this) 호출
        UseDurability(1);
    }
    #endregion



    #region 횃불
    private void SetTorchLight(bool on)
    {
        _isLit = on;

        if (torchLight != null)
            torchLight.enabled = on;
        else
            Debug.LogWarning("[횃불] torchLight가 연결되지 않았습니다");

        Debug.Log($"[횃불] {(on ? "켜짐" : "꺼짐")}");
    }
    #endregion



    #region 내부 이벤트
    private void OnToolBroken()
    {
        Debug.Log($"[도구] {itemData.itemName}이(가) 부서졌습니다!");
        Unequip();

        PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
        if (inventory != null)
            inventory.ClearEquippedItem(EquipSlot.Hand, itemData);

        Destroy(gameObject);
    }
    #endregion


    public override string ToString()
    {
        string info = base.ToString();
        if (itemData.hasDurability)
            info += $" [내구도: {_currentDurability}/{itemData.maxDurability}]";
        if (survivalToolType == SurvivalToolType.Torch)
            info += $" [{(_isLit ? "켜짐" : "꺼짐")}]";
        return info;
    }
}
