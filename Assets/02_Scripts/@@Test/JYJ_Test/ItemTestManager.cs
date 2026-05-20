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

    [Header("내구도 파괴 테스트")]
    public ItemDataSO testToolSO;

    [Header("음식 섭취 테스트")]
    public ItemDataSO testFoodSO;

    private void Start()
    {
        if (InventoryManager.Instance == null)
            Debug.LogError("[테스트] InventoryManager.Instance null");
        else
            Debug.Log("[테스트] InventoryManager 연결");
    }
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
        if (Input.GetKeyDown(KeyCode.Alpha8)) TestCategoryFilter();
        if (Input.GetKeyDown(KeyCode.Alpha9)) TestCraftableFilter();
        if (Input.GetKeyDown(KeyCode.Alpha0)) TestDurabilityBreak();
        if (Input.GetKeyDown(KeyCode.Q)) TestEatFood();
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
        testTree.OnHit(SurvivalToolType.Axe_Stone); // Axe → Axe_Stone으로 변경
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

    // 8키 — 카테고리별 레시피 출력
    void TestCategoryFilter()
    {
        foreach (RecipeCategory category in System.Enum.GetValues(typeof(RecipeCategory)))
        {
            var recipes = CraftingManager.Instance.GetRecipesByCategory(category);
            if (recipes.Count == 0) continue;
            Debug.Log($"[카테고리: {category}]");
            foreach (var r in recipes)
                Debug.Log($"  - {r.recipeName}");
        }
    }

    // 9키 — 현재 제작 가능한 레시피만 출력
    void TestCraftableFilter()
    {
        var craftable = CraftingManager.Instance.GetCraftableRecipes();
        Debug.Log($"[제작 가능 레시피: {craftable.Count}개]");
        foreach (var r in craftable)
            Debug.Log($"  - {r.recipeName}");
    }

    // 0키 — 내구도 반복 소모 → 파괴 확인
    void TestDurabilityBreak()
    {
        if (testToolSO == null) { Debug.Log("[테스트] 도구 SO 연결 안 됨!"); return; }
        if (!InventoryManager.Instance.HasItem(testToolSO))
        {
            InventoryManager.Instance.AddItem(testToolSO, 1);
            Debug.Log($"[테스트] {testToolSO.itemName} 인벤토리 추가");
            return;
        }
        GameObject tempObj = new GameObject("TempTool");
        var tool = tempObj.AddComponent<Item_SurvivalTool>();
        tool.Init(testToolSO);

        Debug.Log($"[테스트] 내구도 파괴 테스트 시작 ({tool.CurrentDurability}/{testToolSO.maxDurability})");

        int maxTries = 200;
        while (tool != null && maxTries-- > 0)
            tool.UseDurability(10);

        Debug.Log("[테스트] 내구도 파괴 완료");
    }


    // Q키 — 음식 먹기 테스트
    void TestEatFood()
    {
        if (testFoodSO == null) { Debug.Log("[테스트] 음식 SO 연결 안 됨!"); return; }

        if (!InventoryManager.Instance.HasItem(testFoodSO))
        {
            InventoryManager.Instance.AddItem(testFoodSO, 1);
            Debug.Log($"[테스트] {testFoodSO.itemName} 인벤토리 추가");
            return;
        }

        GameObject tempObj = new GameObject("TempFood"); //임시 음식 생성
        var food = tempObj.AddComponent<Item_Food>();
        food.Init(testFoodSO);
        food.Eat();

        Debug.Log($"[테스트] {testFoodSO.itemName} 섭취 완료!");
        Debug.Log($"[테스트] 배고픔 +{testFoodSO.hungerRestore} / 체력 +{testFoodSO.healthRestore} / 정신력 +{testFoodSO.sanityRestore}");
    }
}