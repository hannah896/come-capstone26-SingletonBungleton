using UnityEngine;

/// <summary>
/// 아이템 데이터 테스트용 매니저
/// - U 키: 전체 아이템 목록 출력
/// - T 키: 도구 사용 테스트
/// - W 키: 무기 공격 테스트
/// </summary>
public class ItemDataTestManager : MonoBehaviour
{
    [Header("=== 생존도구 데이터 ===")]
    public ItemDataSO[] survivalToolData;

    [Header("=== 전투도구 데이터 ===")]
    public ItemDataSO[] combatGearData;

    [Header("=== 자원 데이터 ===")]
    public ItemDataSO[] resourceData;

    [Header("=== 전리품 데이터 ===")]
    public ItemDataSO[] lootData;

    // 테스트용 아이템 인스턴스
    private Item_SurvivalTool testTool;
    private Item_CombatGear testWeapon;

    void Start()
    {
        Debug.Log("=== 아이템 시스템 테스트 시작 ===");
        Debug.Log("U 키: 전체 아이템 목록");
        Debug.Log("T 키: 도구 테스트");
        Debug.Log("W 키: 무기 테스트");

        CreateTestItems();
    }

    void Update()
    {
        // U 키: 아이템 목록 출력
        if (Input.GetKeyDown(KeyCode.U))
        {
            PrintAllItems();
        }

        // T 키: 도구 사용 테스트
        if (Input.GetKeyDown(KeyCode.T))
        {
            TestTool();
        }

        // W 키: 무기 공격 테스트
        if (Input.GetKeyDown(KeyCode.W))
        {
            TestWeapon();
        }
    }

    /// <summary>
    /// 테스트용 아이템 인스턴스 생성
    /// </summary>
    void CreateTestItems()
    {
        // 도끼 생성 (첫 번째 생존도구)
        if (survivalToolData.Length > 0 && survivalToolData[0] != null)
        {
            GameObject toolObj = new GameObject("TestTool");
            testTool = toolObj.AddComponent<Item_SurvivalTool>();
            testTool.itemData = survivalToolData[0];
            testTool.survivalToolType = SurvivalToolType.Axe;

            Debug.Log($"테스트 도구 생성: {testTool.ToString()}");
        }

        // 칼 생성 (첫 번째 전투도구)
        if (combatGearData.Length > 0 && combatGearData[0] != null)
        {
            GameObject weaponObj = new GameObject("TestWeapon");
            testWeapon = weaponObj.AddComponent<Item_CombatGear>();
            testWeapon.itemData = combatGearData[0];
            testWeapon.combatGearType = CombatGearType.Sword;

            Debug.Log($"테스트 무기 생성: {testWeapon.ToString()}");
        }
    }

    /// <summary>
    /// 전체 아이템 데이터 출력
    /// </summary>
    void PrintAllItems()
    {
        Debug.Log("\n=== 📦 생존도구 ===");
        PrintItemArray(survivalToolData);

        Debug.Log("\n=== ⚔️ 전투도구 ===");
        PrintItemArray(combatGearData);

        Debug.Log("\n=== 🪨 자원 ===");
        PrintItemArray(resourceData);

        Debug.Log("\n=== 🥩 전리품 ===");
        PrintItemArray(lootData);
    }

    /// <summary>
    /// 아이템 배열 출력
    /// </summary>
    void PrintItemArray(ItemDataSO[] items)
    {
        if (items == null || items.Length == 0)
        {
            Debug.Log("  (아이템 없음)");
            return;
        }

        foreach (var data in items)
        {
            if (data == null) continue;

            string info = $"  [{data.itemID}] {data.itemName}";

            if (data.hasDurability)
                info += $" (내구도: {data.maxDurability})";

            if (data.attackDamage > 0)
                info += $" (공격력: {data.attackDamage})";

            if (data.defense > 0)
                info += $" (방어력: {data.defense})";

            Debug.Log(info);
        }
    }

    /// <summary>
    /// 도구 사용 테스트
    /// </summary>
    void TestTool()
    {
        if (testTool == null)
        {
            Debug.LogWarning("테스트 도구가 없습니다!");
            return;
        }

        Debug.Log($"\n=== 도구 사용 테스트 ===");
        Debug.Log($"사용 전: {testTool.ToString()}");

        testTool.Use();

        Debug.Log($"사용 후: {testTool.ToString()}");
    }

    /// <summary>
    /// 무기 공격 테스트
    /// </summary>
    void TestWeapon()
    {
        if (testWeapon == null)
        {
            Debug.LogWarning("테스트 무기가 없습니다!");
            return;
        }

        Debug.Log($"\n=== 무기 공격 테스트 ===");
        Debug.Log($"공격 전: {testWeapon.ToString()}");

        testWeapon.Attack();

        Debug.Log($"공격 후: {testWeapon.ToString()}");
    }
}