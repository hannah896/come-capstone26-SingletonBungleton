#if PHOTON_FUSION
using Fusion;
using UnityEngine;

/// <summary>
/// Fusion의 오브젝트 생성/파괴를 처리하는 프로바이더.
/// NetworkRunner에 연결되어 네트워크 오브젝트의 생성과 파괴를 담당합니다.
/// PoolManager는 비동기 전용(SpawnAsync)이므로, 동기 콜백인 Fusion에서는 기본 Instantiate/Destroy를 사용합니다.
/// </summary>
public class FusionPoolProvider : NetworkObjectProviderDefault
{
    // 프리팹 인스턴스를 생성
    public override NetworkObjectAcquireResult AcquirePrefabInstance(NetworkRunner runner, in NetworkPrefabAcquireContext context, out NetworkObject result)
    {
        result = null;

        var prefab = runner.Config.PrefabTable.Load(context.PrefabId, true);
        if (prefab == null)
        {
            return NetworkObjectAcquireResult.Failed;
        }

        result = Object.Instantiate(prefab);
        return NetworkObjectAcquireResult.Success;
    }

    // 인스턴스를 파괴하거나 풀로 반환
    public override void ReleaseInstance(NetworkRunner runner, in NetworkObjectReleaseContext context)
    {
        if (context.Object == null) return;

        var go = context.Object.gameObject;

        // Main.Pool에 등록된 오브젝트라면 Despawn으로 반환
        var poolManager = Main.Pool;
        if (poolManager != null)
        {
            poolManager.Despawn(go);
            return;
        }

        Object.Destroy(go);
    }
}
#endif