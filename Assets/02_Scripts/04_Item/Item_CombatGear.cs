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

public class Item_CombatGear : Item, IEquipable
{
    [Header("=== 전투도구 전용 속성 ===")]
    [Tooltip("전투도구 세부 타입")]
    public CombatGearType combatGearType = CombatGearType.None;

    // IEquipable 인터페이스 구현
    private int _currentDurability;
    public int CurrentDurability
    {
        get => _currentDurability;
        set => _currentDurability = value;
    }

    [Header("=== 활 전용 ===")]
    [Tooltip("화살 프리팹")] //나중에 추가
    public GameObject arrowPrefab;

    [Tooltip("발사 위치")]
    public Transform firePoint;

    protected override void Init()
    {
        base.Init();

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

//장비 장착
    public void Equip()
    {
        Debug.Log($"{itemData.itemName} 장착!");

        // TODO: 플레이어 스탯에 방어력/공격력 추가
        if (IsArmor())
        {
            Debug.Log($"방어력 +{itemData.defense}");
        }

        if (IsWeapon())
        {
            Debug.Log($"공격력 +{itemData.attackDamage}");
        }
    }

//장비 해제
    public void Unequip()
    {
        Debug.Log($"{itemData.itemName} 해제!");

        // TODO: 플레이어 스탯에서 방어력/공격력 제거
    }


//무기로 공격
    public void Attack()
    {
        if (!IsWeapon())
        {
            Debug.LogWarning($"{itemData.itemName}은(는) 무기가 아닙니다!");
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

//근접공격
    private void MeleeAttack()
    {
        Debug.Log($"{itemData.itemName} 근접 공격! (공격력: {itemData.attackDamage}, 범위: {itemData.attackRange})");

        // TODO: Physics.OverlapSphere로 범위 내 적 탐지 후 데미지 적용

        UseDurability(1);
    }

//원거리공격
    private void RangedAttack()
    {
        Debug.Log($"{itemData.itemName} 원거리 공격! (공격력: {itemData.attackDamage})");

        // TODO: 화살 발사
        // if (arrowPrefab != null)
        // {
        //     Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        //     Instantiate(arrowPrefab, spawnPos, transform.rotation);
        // }

        UseDurability(1);
    }

//방패
    private void Block()
    {
        Debug.Log($"{itemData.itemName}으로 방어!");

        // TODO: 방어 모션, 데미지 감소 처리

        UseDurability(1); // 내구도 소모 
    }

//내구도 사용
    public void UseDurability(int amount = 1)
    {
        if (!itemData.hasDurability) return;

        _currentDurability = Mathf.Max(0, _currentDurability - amount);

        if (_currentDurability <= 0)
        {
            OnGearBroken();
        }
    }


 //장비 부서졌을 때
    private void OnGearBroken()
    {
        Debug.Log($"{itemData.itemName}이(가) 부서졌습니다!");

        // 장착 중이었다면 장착 해제
        Unequip();

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

//방어구인지 확인
    public bool IsArmor()
    {
        return combatGearType == CombatGearType.Helmet ||
               combatGearType == CombatGearType.Armor;
    }

//무기인지 확인
    public bool IsWeapon()
    {
        return combatGearType == CombatGearType.Sword ||
               combatGearType == CombatGearType.Bow ||
               combatGearType == CombatGearType.Spear ||
               combatGearType == CombatGearType.Shield;
    }

//공격 가능여부 확인
    public bool CanAttack()
    {
        // 내구도가 없으면 공격 불가
        if (itemData.hasDurability && _currentDurability <= 0)
            return false;

        return true;
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

        // 방어력 표시
        if (IsArmor())
        {
            baseInfo += $" [방어: {itemData.defense}]";
        }

        // 공격력 표시
        if (IsWeapon())
        {
            baseInfo += $" [공격: {itemData.attackDamage}]";
        }

        // 내구도 표시
        if (itemData.hasDurability)
        {
            baseInfo += $" [내구도: {_currentDurability}/{itemData.maxDurability}]";
        }

        return baseInfo;
    }
}