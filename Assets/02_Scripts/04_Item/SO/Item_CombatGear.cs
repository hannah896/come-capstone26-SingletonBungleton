using UnityEngine;

/// <summary>
/// 전투도구 아이템 클래스
/// - 헬멧
/// - 갑옷
/// - 칼
/// - 활
/// - 창
/// - 방패
/// </summary>

public class Item_CombatGear : ItemData, IEquipable
{
    [Header("=== 전투장비 전용 ===")]
    [Tooltip("전투도구 세부 타입")]
    public CombatGearType combatGearType = CombatGearType.None;

    // 내구도
    private int _currentDurability;
    public int CurrentDurability
    {
        get => _currentDurability;
        set => _currentDurability = Mathf.Clamp(value, 0, (int)itemData.maxDurability);
    }

    [Header("=== 활 전용 ===")]
    [Tooltip("화살 프리팹")]
    public GameObject arrowPrefab;
    [Tooltip("발사 위치")]
    public Transform firePoint;


    #region 초기화
    protected override void Init()
    {
        base.Init();
        if (itemData == null) return;
        if (itemData.hasDurability)
            _currentDurability = (int)itemData.maxDurability;
        combatGearType = itemData.combatGearType;
    }

    public override void Init(ItemDataSO data)
    {
        base.Init(data);
        if (itemData.hasDurability)
            _currentDurability = (int)itemData.maxDurability;

        combatGearType = itemData.combatGearType;
    }
    #endregion


    #region IEquipable 구현
    public void Equip()
    {
        Debug.Log($"[장비] {itemData.itemName} 장착!");

        // TODO: PlayerStats.Instance.AddDefense/AddAttack 연동
        if (IsArmor() || IsShield()) Debug.Log($"방어력 +{itemData.defense}");
        if (IsWeapon()) Debug.Log($"공격력 +{itemData.attackDamage}");
    }

    public void Unequip()
    {
        Debug.Log($"[장비] {itemData.itemName} 해제!");
        // TODO: PlayerStats에서 스탯 제거
    }

    public void UseDurability(int amount = 1)
    {
        if (!itemData.hasDurability) return;

        _currentDurability = Mathf.Max(0, _currentDurability - amount);
        Debug.Log($"[장비] 내구도 {_currentDurability}/{itemData.maxDurability}");

        if (_currentDurability <= 0)
            OnGearBroken();
    }

    public void Repair(int amount)
    {
        if (!itemData.hasDurability) return;

        _currentDurability = Mathf.Min(_currentDurability + amount, (int)itemData.maxDurability);
        Debug.Log($"[장비] {itemData.itemName} 수리! ({_currentDurability}/{itemData.maxDurability})");
    }

    public float GetDurabilityPercent()
    {
        if (!itemData.hasDurability) return 1f;
        return (float)_currentDurability / itemData.maxDurability;
    }
    #endregion


    #region 전투 행동
    /// 공격 (플레이어 입력에서 호출)
    public void Attack()
    {
        if (!IsWeapon())
        {
            Debug.LogWarning($"[장비] {itemData.itemName}은 무기가 아닙니다!");
            return;
        }

        if (!CanAttack())
        {
            Debug.Log($"[장비] {itemData.itemName}이(가) 부서져 공격 불가!");
            return;
        }

        switch (combatGearType)
        {
            case CombatGearType.Sword:
            case CombatGearType.Spear:
                MeleeAttack();
                break;
            case CombatGearType.Bow:
                RangedAttack();
                break;
            case CombatGearType.Shield:
                Block();
                break;
        }
    }

    private void MeleeAttack()
    {
        Debug.Log($"[근접] {itemData.itemName} 공격! (데미지: {itemData.attackDamage}, 범위: {itemData.attackRange})");
        // TODO: Physics.OverlapSphere(transform.position, itemData.attackRange) → IDamageable.TakeDamage()
        UseDurability(1);
    }

    private void RangedAttack()
    {
        Debug.Log($"[원거리] {itemData.itemName} 발사! (데미지: {itemData.attackDamage})");
        // TODO: arrowPrefab 인스턴스화 → 방향으로 발사
        UseDurability(1);
    }

    private void Block()
    {
        Debug.Log($"[방패] {itemData.itemName} 방어!");
        // TODO: 방어 모션, 데미지 감소 처리
        UseDurability(1);
    }
    #endregion


    #region 헬퍼 메서드
    public bool IsArmor()
    {
        return combatGearType == CombatGearType.Helmet ||
               combatGearType == CombatGearType.Chestplate;
    }

    public bool IsShield() => combatGearType == CombatGearType.Shield;

    public bool IsWeapon()
    {
        return combatGearType == CombatGearType.Sword ||
               combatGearType == CombatGearType.Bow   ||
               combatGearType == CombatGearType.Spear;
    }

    public bool CanAttack()
    {
        if (itemData.hasDurability && _currentDurability <= 0) return false;
        return true;
    }
    #endregion



    #region 내부 이벤트
    private void OnGearBroken()
    {
        Debug.Log($"[장비] {itemData.itemName}이(가) 부서졌습니다!");
        Unequip();

        //var inv = InventoryManager.Instance;
        //if (inv != null)
        //{
        //    if (inv.equippedHand?.data == itemData)     inv.UnequipAndDiscard(EquipSlot.Hand);
        //    else if (inv.equippedHead?.data == itemData) inv.UnequipAndDiscard(EquipSlot.Head);
        //    else if (inv.equippedChest?.data == itemData) inv.UnequipAndDiscard(EquipSlot.Chest);
        //    else inv.RemoveItem(itemData, 1);
        //}

        Destroy(gameObject);
    }
    #endregion



    public override string ToString()
    {
        string info = base.ToString();
        if (IsArmor()) info += $" [방어: {itemData.defense}]";
        if (IsWeapon()) info += $" [공격: {itemData.attackDamage}]";
        if (itemData.hasDurability)
            info += $" [내구도: {_currentDurability}/{itemData.maxDurability}]";
        return info;
    }
}