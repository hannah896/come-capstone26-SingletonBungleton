using UnityEngine;

[CreateAssetMenu(fileName = "Item_NewItem", menuName = "Game/Item Data", order = 0)]
public class ItemDataSO : ScriptableObject
{
    // ===== 기본 정보 =====
    [Header("기본 정보")]
    [Tooltip("아이템 고유 ID")]
    public string itemID = "";

    [Tooltip("게임에 표시될 이름")]
    public string itemName = "";

    [Tooltip("아이템 대분류")]
    public ItemType itemType = ItemType.Gatherables;


    // ===== 세부 타입 (도구/무기일 때만 사용) =====
    [Header("세부 타입")]
    [Tooltip("도구 종류 (itemType이 Tool일 때만)")]
    public ToolType toolType = ToolType.None;

    [Tooltip("무기 종류 (itemType이 Weapon일 때만)")]
    public WeaponType weaponType = WeaponType.None;


    // ===== 비주얼 =====
    [Header("아이콘")]
    public Sprite icon = null;


    // ===== 스택 설정 =====
    [Header("스택 설정")]
    [Tooltip("중첩 가능 여부 (도구/무기는 false)")]
    public bool isStackable = true;

    [Tooltip("최대 중첩 개수")]
    public int maxStack = 999;


    // ===== 내구도 설정 =====
    [Header("내구도 설정")]
    [Tooltip("내구도 있는지 (도구/무기는 true)")]
    public bool hasDurability = false;

    [Tooltip("최대 내구도")]
    public int maxDurability = 100;


    // ===== 음식 전용 =====
    [Header("음식 전용 설정")]
    [Tooltip("소비기한이 있는지 (음식만 true)")]
    public bool hasFreshness = false;

    [Tooltip("최대 신선도")]
    public float maxFreshness = 0f;

    [Tooltip("배고픔 회복량")]
    public float hungerRestore = 0f;

    [Tooltip("체력 회복량")]
    public float healthRestore = 0f;

    [Tooltip("정신력 회복량")]
    public float sanityRestore = 0f;


    // ===== 장비 전용 =====
    [Header("장비 전용 설정")]
    [Tooltip("장착 슬롯")]
    public EquipSlot equipSlot = EquipSlot.None;

    [Tooltip("공격력 (무기)")]
    public float attackDamage = 0f;

    [Tooltip("방어력 (방어구)")]
    public float defense = 0f;


    // ===== 설명 =====
    [Header("설명")]
    [TextArea(2, 4)]
    public string description = "";
}
