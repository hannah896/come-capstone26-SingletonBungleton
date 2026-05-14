using Cysharp.Threading.Tasks;
using UnityEngine;
/// <summary>
/// 나무, 돌 등의 자원 노드로, 플레이어가 도구로 공격하여 부술 수 있는 오브젝트
/// </summary>
public class BreakableResourceNode : ResourceNode
{
    [Header("Drop")]
    [SerializeField] private string _dropPrefabKey;
    [SerializeField] private float _dropRadius = 0.5f;

    [Header("FX")]
    [SerializeField] private string _destroyFxKey;

    protected override void OnDamaged(DamageContext context)
    {
        Debug.Log($"{ResourceNodeData.Name} 데미지 입음! 사용 도구: {context.ToolId}, 피해량: {context.Amount}, 남은 체력: {CurrentHealth}");
    }

    protected override void OnDestroyed()
    {
        base.OnDestroyed();
        SpawnDestroyFxAsync().Forget();
        SpawnDropsAsync().Forget();
    }

    private async UniTask SpawnDestroyFxAsync()
    {
        if (string.IsNullOrEmpty(_destroyFxKey)) return;

        GameObject fx = await Extensions.SpawnAsync(_destroyFxKey, null);
        if (fx == null) return;

        fx.transform.position = DeathPosition;
    }

    private async UniTask SpawnDropsAsync()
    {
        // 배열이 비어있으면 종료
        if (ResourceNodeData.Drops == null || ResourceNodeData.Drops.Length == 0) return;

        // 배열에 등록된 모든 드롭 아이템(통나무, 나뭇가지, 사과 등)을 순회
        foreach (DropItemData dropData in ResourceNodeData.Drops)
        {
            if (string.IsNullOrEmpty(dropData.DropPrefabKey)) continue;
            // 1. 드롭 확률 체크 (예: 사과가 0.1(10%) 확률이라면)
            if (Random.value > dropData.DropChance)
                continue; 

            // 2. 수량 결정
            int minCount = Mathf.Max(0, dropData.MinDropCount);
            int maxCount = Mathf.Max(minCount, dropData.MaxDropCount);
            int count = Random.Range(minCount, maxCount + 1);

            // 3. 결정된 수량만큼 스폰
            for (int i = 0; i < count; i++)
            {
                GameObject dropObj = await Extensions.SpawnAsync(dropData.DropPrefabKey, null);
                if (dropObj == null) continue;

                // 바닥에 흩뿌리기
                Vector2 offset2D = Random.insideUnitCircle * _dropRadius;
                Vector3 offset = new Vector3(offset2D.x, 0f, offset2D.y);
                dropObj.transform.position = DeathPosition + offset;
            }
        }
    }
}