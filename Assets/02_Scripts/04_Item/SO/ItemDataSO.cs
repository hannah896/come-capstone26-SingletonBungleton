using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "Item_", menuName = "Game/Item Data")]
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




    [Header("=== 분류 및 스택 ===")]
    [Tooltip("아이템 분류 타입")]
    public ItemType itemType;

    [Header("=== 세부 분류 (해당되는 것만 선택) ===")]
    [Tooltip("생존도구일 경우 선택")]
    public SurvivalToolType survivalToolType;

    [Tooltip("이 도구로 채집 가능한 노드 타입 목록 (생존도구용, None = 제한 없음)")]
    public List<ResourceNodeType> harvestableNodeTypes = new List<ResourceNodeType>();

    [Tooltip("전투장비일 경우 선택")]
    public CombatGearType combatGearType;

    [Tooltip("자원일 경우 선택")]
    public ResourceType resourceType;

    [Tooltip("전리품일 경우 선택")]
    public BootyType bootyType;

    [Tooltip("음식일 경우 선택")]
    public FoodType foodType;

    [Tooltip("요리일 경우 선택")]
    public DishType dishType;

    [Tooltip("특수 아이템일 경우 선택")]
    public SpecialType specialType;

    [Tooltip("겹치기 가능 여부, 최대 개수")]
    public bool isStackable = true;
    [Range(1, 999)]
    public int maxStack = 64;


    [Header("=== 장착 정보 (장비용) ===")]
    [Tooltip("장착 슬롯 (머리/가슴/손)")]
    public EquipSlot equipSlot = EquipSlot.None;

    [Tooltip("내구도 여부")]
    public bool hasDurability = false;

    [Tooltip("최대 내구도")]
    public float maxDurability = 100f;

    [Tooltip("한번 사용 시 감소하는 내구도")]
    public float costPerDurability = 1f;

    [Tooltip("공격력")]
    public float attackDamage = 0f;

    [Tooltip("공격 범위")]
    public float attackRange = 1.5f;

    [Tooltip("방어력")]
    public float defense = 0f;



    [Header("=== 생존 효과 (음식용) ===")]
    [Tooltip("배고픔 회복량")]
    public float hungerRestore = 0f;

    [Tooltip("체력 회복량")]
    public float healthRestore = 0f;

    [Tooltip("Ego 회복량")]
    [FormerlySerializedAs("sanityRestore")]
    public float egoRestore = 0f;

    [Tooltip("소비기한 (분 단위, 0 = 부패 없음)")]
    public float expirationTime = 0f;


    [Header("=== 아이템 태그 ===")]
    [Tooltip("이 아이템의 사용 용도 (복수 선택 가능)")]
    public ItemTag tags = ItemTag.None;

    /// <summary>특정 태그를 가지고 있는지 확인.</summary>
    public bool HasTag(ItemTag tag) => (tags & tag) != 0;

    [Header("=== 배치 정보 ===")]
    [Tooltip("배치 가능 아이템 여부")]
    public bool isPlaceable = false;

    [Tooltip("배치 시 사용할 프리팹")]
    public GameObject placementPrefab;

    [Tooltip("배치 크기 (그리드 기준)")]
    public Vector2Int placementFootprint = Vector2Int.one;

    [Tooltip("배치 시 피벗 오프셋")]
    public Vector3 placementPivotOffset;

    [Tooltip("그리드 스냅 여부")]      // -> 연속적 움직임 or 그리드 스냅 방식
    public bool placementSnapToGrid = true;

    [Tooltip("배치 가능 체크 높이")]
    public float placementCheckHeight = 2f;

    [Tooltip("체크 박스 중심 오프셋")]
    public Vector3 placementCheckCenterOffset;

    private void OnValidate()
    {
        // ID가 없으면 자동 생성 (파일명 기반)
        if (string.IsNullOrEmpty(itemID))
        {
            itemID = name;
        }
    }
}
