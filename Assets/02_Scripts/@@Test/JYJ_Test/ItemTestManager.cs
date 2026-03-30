using UnityEngine;

public class ItemTestManager : MonoBehaviour
{
    [Header("테스트할 아이템 SO 연결")]
    public ItemDataSO testResource;
    public ItemDataSO testFood;
    public ItemDataSO testTool;

    [Header("테스트할 채집 오브젝트")]
    public GatherableObject testTree;

    [Header("크래프팅 테스트")]
    public RecipeDataSO testRecipe;


    private void Update()
    {
        // 숫자키로 테스트
        if (Input.GetKeyDown(KeyCode.Alpha1)) TestAddResource();
        if (Input.GetKeyDown(KeyCode.Alpha2)) TestAddFood();
        if (Input.GetKeyDown(KeyCode.Alpha3)) TestAddTool();
        if (Input.GetKeyDown(KeyCode.Alpha4)) TestGather();
        if (Input.GetKeyDown(KeyCode.Alpha5)) TestRemove();
        if (Input.GetKeyDown(KeyCode.Alpha6)) TestDurability();
        if (Input.GetKeyDown(KeyCode.Alpha7)) TestCraft();
    }

    // 1키 — 재료 아이템 추가 (스택 테스트)
    void TestAddResource()
    {
        InventoryManager.Instance.AddItem(testResource, 5);
        Debug.Log($"[테스트] {testResource.itemName} 5개 추가 → 현재 {InventoryManager.Instance.GetItemCount(testResource)}개");
    }

    // 2키 — 음식 추가
    void TestAddFood()
    {
        InventoryManager.Instance.AddItem(testFood, 1);
        Debug.Log($"[테스트] {testFood.itemName} 추가");
    }

    // 3키 — 도구 추가
    void TestAddTool()
    {
        InventoryManager.Instance.AddItem(testTool, 1);
        Debug.Log($"[테스트] {testTool.itemName} 추가");
    }

    // 4키 — 채집 오브젝트 타격 (GatherableObject 테스트)
    void TestGather()
    {
        if (testTree == null) { Debug.Log("[테스트] Tree_Object 연결 안 됨!"); return; }
        testTree.OnHit(SurvivalToolType.Axe);
        Debug.Log("[테스트] Tree_Object 타격!");
    }

    // 5키 — 아이템 제거 테스트
    void TestRemove()
    {
        bool result = InventoryManager.Instance.RemoveItem(testResource, 1);
        Debug.Log($"[테스트] {testResource.itemName} 제거 {(result ? "성공" : "실패")}");
    }

    // 6키 — 내구도 테스트
    void TestDurability()
    {
        // 인벤토리에 도끼가 있는지 확인
        bool has = InventoryManager.Instance.HasItem(testTool);
        Debug.Log($"[테스트] 도끼 보유: {has}, 수량: {InventoryManager.Instance.GetItemCount(testTool)}");
    }

    // 7키 — 크래프팅 테스트
    void TestCraft()
    {
        if (testRecipe == null) { Debug.Log("[테스트] 레시피 연결 안 됨!"); return; }
        bool can = CraftingManager.Instance.CanCraft(testRecipe);
        Debug.Log($"[테스트] 제작 가능: {can}");
        if (can) CraftingManager.Instance.Craft(testRecipe);
    }
}