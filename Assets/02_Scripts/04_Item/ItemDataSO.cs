using UnityEngine;

[CreateAssetMenu(fileName = "New_Item", menuName = "Game/Item Data")]
public class ItemDataSO : ScriptableObject
{
    [Header("=== 기본 정보 ===")]
    [Tooltip("아이템 고유 ID")]
    public string itemID;

    [Tooltip("아이템 이름")]
    public string itemName;

    [Tooltip("아이템 설명")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("아이템 아이콘 (UI용)")]  //나중에 추가
    public Sprite icon;

    [Tooltip("아이템 프리팹")]  //나중에 추가
    public GameObject prefab;




    [Header("=== 분류 ===")]
    [Tooltip("아이템 대분류 타입")]
    public ItemType itemType;

    [Header("=== 스택 관리 ===")]
    [Tooltip("겹치기 가능 여부 (자원은 true, 장비는 false)")]
    public bool isStackable = true;

    [Tooltip("최대 겹치기 개수")]
    [Range(1, 999)]
    public int maxStack = 64;


    [Header("=== 장착 정보 (장비 전용) ===")]
    [Tooltip("장착 슬롯 (머리/가슴/손)")]
    public EquipSlot equipSlot = EquipSlot.None;




    [Header("=== 생존 스탯 효과 (음식/일부 도구) ===")]
    [Tooltip("배고픔 회복량")]
    public float hungerRestore = 0f;

    [Tooltip("체력 회복량")]
    public float healthRestore = 0f;

    [Tooltip("정신력 회복량")]
    public float sanityRestore = 0f;




    [Header("=== 내구도 (도구/무기/방어구) ===")]
    [Tooltip("내구도가 있는 아이템인지")]
    public bool hasDurability = false;

    [Tooltip("최대 내구도")]
    public float maxDurability = 100f;




    [Header("=== 전투 속성 (무기 전용) ===")]
    [Tooltip("공격력")]
    public float attackDamage = 0f;

    [Tooltip("공격 속도 (초당 공격 횟수)")]
    public float attackSpeed = 1f;

    [Tooltip("공격 범위")]
    public float attackRange = 1f;




    [Header("=== 방어 속성 (방어구 전용) ===")]
    [Tooltip("방어력")]
    public float defense = 0f;

    [Tooltip("이동 속도 배율 (1 = 기본, 0.8 = 20% 느림)")]
    public float moveSpeedMultiplier = 1f;



    [Header("=== 채집 효율 (도구 전용) ===")]
    [Tooltip("채집 속도 배율 (1 = 기본, 2 = 2배 빠름)")]
    public float harvestSpeedMultiplier = 1f;



    [Header("=== 인벤토리 확장 (가방 전용) ===")]
    [Tooltip("추가 슬롯 개수")]
    public int additionalSlots = 0;


    private void OnValidate()
    {
        // ID가 없으면 자동 생성 (파일명 기반)
        if (string.IsNullOrEmpty(itemID))
        {
            itemID = name; // ScriptableObject 파일명 사용
        }
    }
}