using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections;

public class ItemTest : MonoBehaviour
{
    private Item testFood;

    async void Start()
    {
        testFood = await Extensions.LoadAssetAsync<Item_Food>("TestFood");

        Debug.Log("   아이템 시스템 테스트 시작");

        TestItemStacking();
        TestItemSplit();
        TestToolDurability();
        TestFoodFreshness();
    }


    //void TestItemCreation()
    //{
    //    Debug.Log("━━━ [테스트 1] 아이템 생성 ━━━");

    //    Debug.Log($"재료: {testFood.}");
    //}

    void TestItemStacking()
    {
        Debug.Log("━━━ [테스트 2] 중첩 ━━━");

        if (testFood.CanStackWith(testFood))
        {
            int remain = testFood.AddStack(testFood.stackCount);
            Debug.Log($" 중첩 후: {testFood.stackCount}, 남은: {remain}\n");
        }
    }

    void TestItemSplit()
    {
        Debug.Log("━━━ [테스트 3] 분할 ━━━");
        int value = testFood.stackCount;
        testFood.RemoveStack(20);
        Debug.Log($" 원본: {value}, 분할: {testFood.stackCount}\n");
    }

    private async void TestToolDurability()
    {
        Debug.Log("━━━ [테스트 4] 내구도 ━━━");
        var sword = await Extensions.LoadAssetAsync<Item_Tool>("TestTool");
        sword.UseDurability(30);
        Debug.Log($" 사용 후: {sword.CurrentDurability}\n");
    }

    void TestFoodFreshness()
    {
        Debug.Log("━━━ [테스트 5] 신선도 ━━━");
        StartCoroutine(CheckFreshnessRoutine());
    }

    IEnumerator CheckFreshnessRoutine()
    {
        for (int i = 1; i <= 3; i++)
        {
            yield return new WaitForSeconds(5f);
            var f = testFood as Item_Food;
            f?.UpdateFreshness();
            Debug.Log($"  {i * 5}초: {testFood}");
        }
    }
}