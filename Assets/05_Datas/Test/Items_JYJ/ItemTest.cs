using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections;

public class ItemTest : MonoBehaviour
{
    private Item testFood;

    async void Start()
    {
        await UniTask.WaitUntil(() => Managers.Item != null);

        Debug.Log("   아이템 시스템 테스트 시작");

        TestItemCreation();
        TestItemStacking();
        TestItemSplit();
        TestToolDurability();
        TestFoodFreshness();
    }


    void TestItemCreation()
    {
        Debug.Log("━━━ [테스트 1] 아이템 생성 ━━━");
        var wood = Managers.Item.CreateItem("Item_Wood", 50);
        Debug.Log($"재료: {wood}");
    }

    void TestItemStacking()
    {
        Debug.Log("━━━ [테스트 2] 중첩 ━━━");
        var wood1 = Managers.Item.CreateItem("Item_Wood", 80);
        var wood2 = Managers.Item.CreateItem("Item_Wood", 30);
        if (wood1.CanStackWith(wood2))
        {
            int remain = wood1.AddStack(wood2.stackCount);
            Debug.Log($" 중첩 후: {wood1}, 남은: {remain}\n");
        }
    }

    void TestItemSplit()
    {
        Debug.Log("━━━ [테스트 3] 분할 ━━━");
        var wood = Managers.Item.CreateItem("Item_Wood", 50);
        Item splitted = wood.Clone(20);
        wood.RemoveStack(20);
        Debug.Log($" 원본: {wood}, 분할: {splitted}\n");
    }

    void TestToolDurability()
    {
        Debug.Log("━━━ [테스트 4] 내구도 ━━━");
        var sword = Managers.Item.CreateItem("Item_Sword");
        sword.UseDurability(30);
        Debug.Log($" 사용 후: {sword}\n");
    }

    void TestFoodFreshness()
    {
        Debug.Log("━━━ [테스트 5] 신선도 ━━━");
        testFood = Managers.Item.CreateItem("Item_CookedMeat", 3);
        StartCoroutine(CheckFreshnessRoutine());
    }

    IEnumerator CheckFreshnessRoutine()
    {
        for (int i = 1; i <= 3; i++)
        {
            yield return new WaitForSeconds(5f);
            testFood.UpdateFreshness();
            Debug.Log($"  {i * 5}초: {testFood}");
        }
    }
}