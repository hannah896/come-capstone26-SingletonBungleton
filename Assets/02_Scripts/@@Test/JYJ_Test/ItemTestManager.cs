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

    private PlayerInventory inventory;

    private void Update()
    {
        if (inventory == null)
            inventory = FindFirstObjectByType<PlayerInventory>();

        if (Input.GetKeyDown(KeyCode.Alpha1)) TestAddResource();
        if (Input.GetKeyDown(KeyCode.Alpha2)) TestAddFood();
        if (Input.GetKeyDown(KeyCode.Alpha3)) TestAddTool();
        if (Input.GetKeyDown(KeyCode.Alpha4)) TestGather();
        if (Input.GetKeyDown(KeyCode.Alpha5)) TestRemove();
        if (Input.GetKeyDown(KeyCode.Alpha6)) TestDurability();
        if (Input.GetKeyDown(KeyCode.Alpha7)) TestCraft();
        if (Input.GetKeyDown(KeyCode.Alpha8)) TestCategoryFilter();
        if (Input.GetKeyDown(KeyCode.Alpha9)) TestCraftableFilter();
    }

    private void TestAddResource()
    {
        if (!TryResolveInventory()) return;

        inventory.AddItem(testResource, 5);
        Debug.Log($"[테스트] {testResource.itemName} 5개 추가, 현재 {inventory.GetItemCount(testResource)}개");
    }

    private void TestAddFood()
    {
        if (!TryResolveInventory()) return;

        inventory.AddItem(testFood, 1);
        Debug.Log($"[테스트] {testFood.itemName} 추가");
    }

    private void TestAddTool()
    {
        if (!TryResolveInventory()) return;

        inventory.AddItem(testTool, 1);
        Debug.Log($"[테스트] {testTool.itemName} 추가");
    }

    private void TestGather()
    {
        if (testTree == null)
        {
            Debug.Log("[테스트] Tree_Object 연결 없음");
            return;
        }

        testTree.OnHit(SurvivalToolType.Axe);
        Debug.Log("[테스트] Tree_Object 공격");
    }

    private void TestRemove()
    {
        if (!TryResolveInventory()) return;

        bool result = inventory.RemoveItem(testResource, 1);
        Debug.Log($"[테스트] {testResource.itemName} 제거 {(result ? "성공" : "실패")}");
    }

    private void TestDurability()
    {
        if (!TryResolveInventory()) return;

        bool has = inventory.HasItem(testTool);
        Debug.Log($"[테스트] 도구 보유: {has}, 수량: {inventory.GetItemCount(testTool)}");
    }

    private void TestCraft()
    {
        if (testRecipe == null)
        {
            Debug.Log("[테스트] 레시피 연결 없음");
            return;
        }

        bool canCraft = CraftingManager.Instance.CanCraft(testRecipe);
        Debug.Log($"[테스트] 제작 가능: {canCraft}");

        if (canCraft)
            CraftingManager.Instance.Craft(testRecipe);
    }

    private void TestCategoryFilter()
    {
        foreach (RecipeCategory category in System.Enum.GetValues(typeof(RecipeCategory)))
        {
            var recipes = CraftingManager.Instance.GetRecipesByCategory(category);
            if (recipes.Count == 0) continue;

            Debug.Log($"[카테고리: {category}]");
            foreach (var recipe in recipes)
                Debug.Log($"  - {recipe.recipeName}");
        }
    }

    private void TestCraftableFilter()
    {
        var craftable = CraftingManager.Instance.GetCraftableRecipes();
        Debug.Log($"[제작 가능 레시피] {craftable.Count}개");

        foreach (var recipe in craftable)
            Debug.Log($"  - {recipe.recipeName}");
    }

    private bool TryResolveInventory()
    {
        if (inventory != null) return true;

        inventory = FindFirstObjectByType<PlayerInventory>();
        if (inventory != null) return true;

        Debug.LogWarning("[테스트] PlayerInventory를 찾지 못했습니다.");
        return false;
    }
}
