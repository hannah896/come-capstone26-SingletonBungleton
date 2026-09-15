using Cysharp.Threading.Tasks;
using UnityEngine;
/// <summary>
/// 나무, 돌 등의 자원 노드로, 플레이어가 도구로 공격하여 부술 수 있는 오브젝트
/// </summary>
public class BreakableResourceNode : ResourceNode
{
    protected override void OnDamaged(DamageContext context)
    {
        Debug.Log($"{ResourceNodeData.Name} 데미지 입음! 사용 도구: {context.ToolId}, 피해량: {context.Amount}, 남은 체력: {CurrentHealth}");
    }

    protected override void OnDestroyed(DamageContext context)
    {
        base.OnDestroyed(context);
        SpawnDestroyFxAsync().Forget();
        SpawnDropsAsync(context).Forget();
    }

    // 다른 플레이어가 부순 경우: 파괴 이펙트만 보여주고 드롭은 만들지 않는다 (드롭 중복 방지)
    protected override void OnRemoteDestroyed()
    {
        SpawnDestroyFxAsync().Forget();
    }

    private async UniTask SpawnDestroyFxAsync()
    {
        if (string.IsNullOrEmpty(ResourceNodeData.DropFxPrefabKey)) return;

        GameObject fx = await Extensions.SpawnAsync(ResourceNodeData.DropFxPrefabKey, null);
        if (fx == null) return;

        fx.transform.position = DeathPosition;
    }

    private async UniTask SpawnDropsAsync(DamageContext context)
    {
        // 배열이 비어있으면 종료
        if (ResourceNodeData.Drops == null || ResourceNodeData.Drops.Length == 0) return;

        bool directToInventory = ResourceNodeData.GatherDirectlyToInventory;
        PlayerInventory inventory = directToInventory && context.Instigator != null
            ? context.Instigator.GetComponent<PlayerInventory>()
            : null;

        // 노드는 곧 풀로 반납돼 재사용될 수 있으므로 필요한 값은 await 전에 잡아둔다
        Vector3 deathPosition = DeathPosition;
        float dropRadius = ResourceNodeData.DropRadius;

        // 배열에 등록된 모든 드롭 아이템(통나무, 나뭇가지, 사과 등)을 순회
        foreach (DropData dropData in ResourceNodeData.Drops)
        {
            if (string.IsNullOrEmpty(dropData.DropPrefabKey)) continue;
            // 1. 드롭 확률 체크 (예: 사과가 0.1(10%) 확률이라면)
            if (Random.value > dropData.DropChance)
                continue;

            // 2. 수량 결정
            int minCount = Mathf.Max(0, dropData.MinDropCount);
            int maxCount = Mathf.Max(minCount, dropData.MaxDropCount);
            int count = Random.Range(minCount, maxCount + 1);
            if (count <= 0) continue;

            // 3. 인벤토리 직행 모드면 먼저 인벤토리에 넣는다
            ItemDataSO itemSO = inventory != null
                ? await WorldItemSync.LoadItemDataAsync(dropData.DropPrefabKey)
                : null;

            for (int i = 0; i < count; i++)
            {
                if (inventory != null && itemSO != null)
                {
                    inventory.AddItem(itemSO, 1, out int remaining);
                    if (remaining <= 0) continue;
                }

                // 인벤토리에 못 넣었거나(가득 참) direct 모드가 아니면 바닥에 흩뿌리기
                // (멀티에서는 호스트를 거쳐 모든 피어에 같은 바닥 아이템이 생긴다)
                Vector2 offset2D = Random.insideUnitCircle * dropRadius;
                Vector3 offset = new Vector3(offset2D.x, 0f, offset2D.y);
                WorldItemSync.SpawnDroppedItem(dropData.DropPrefabKey, 1, deathPosition + offset);
            }
        }
    }
}