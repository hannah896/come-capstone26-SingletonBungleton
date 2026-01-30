using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections;

public class ItemTest : MonoBehaviour
{
    private Item test_Food;

    async void Start()
    {
        test_Food = await Extensions.LoadAssetAsync<Item_Food>("Test_Food");

        Debug.Log("   아이템 시스템 테스트 시작");

        TestItemStacking();
        TestItemSplit();
        TestToolDurability();
        TestFoodFreshness();
    }


    //void TestItemCreation()
    //{
    //    Debug.Log("━━━ [테스트 1] 아이템 생성 ━━━");

    //    Debug.Log($"재료: {test_Food.}");
    //}

    void TestItemStacking()
    {
        Debug.Log("━━━ [테스트 2] 중첩 ━━━");

        if (test_Food.CanStackWith(test_Food))
        {
            int remain = test_Food.AddStack(test_Food.stackCount);
            Debug.Log($" 중첩 후: {test_Food.stackCount}, 남은: {remain}\n");
        }
    }

    void TestItemSplit()
    {
        Debug.Log("━━━ [테스트 3] 분할 ━━━");
        int value = test_Food.stackCount;
        test_Food.RemoveStack(20);
        Debug.Log($" 원본: {value}, 분할: {test_Food.stackCount}\n");
    }

    private async void TestToolDurability()
    {
        Debug.Log("━━━ [테스트 4] 내구도 ━━━");
        var sword = await Extensions.LoadAssetAsync<Item_Tool>("Test_Tool");
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
            var f = test_Food as Item_Food;
            f?.UpdateFreshness();
            Debug.Log($"  {i * 5}초: {test_Food}");
        }
    }
}