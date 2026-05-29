using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// DropData 배열을 확률·수량에 따라 월드에 스폰하는 공용 헬퍼.
/// </summary>
public static class DropSpawner
{
    public static async UniTask SpawnAsync(DropData[] drops, Vector3 origin, float radius)
    {
        if (drops == null || drops.Length == 0) return;

        foreach (DropData dropData in drops)
        {
            if (string.IsNullOrEmpty(dropData.DropPrefabKey)) continue;

            // 드랍 확률 체크
            if (Random.value > dropData.DropChance) continue;

            // 수량 결정
            int minCount = Mathf.Max(0, dropData.MinDropCount);
            int maxCount = Mathf.Max(minCount, dropData.MaxDropCount);
            int count = Random.Range(minCount, maxCount + 1);

            for (int i = 0; i < count; i++)
            {
                GameObject dropObj = await Extensions.SpawnAsync(dropData.DropPrefabKey, null);
                if (dropObj == null) continue;

                // 바닥에 흩뿌리기
                Vector2 offset2D = Random.insideUnitCircle * radius;
                dropObj.transform.position = origin + new Vector3(offset2D.x, 0f, offset2D.y);
            }
        }
    }
}
