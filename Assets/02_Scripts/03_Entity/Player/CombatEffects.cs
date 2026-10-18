using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 무기의 공격 이펙트(파티클/VFX 프리팹)를 맞은 자리에 한 번 재생하고 일정 시간 뒤 풀로 돌려보낸다.
/// 이펙트 프리팹은 Addressables 키로 지정한다 (ItemDataSO.attackEffectKey).
/// 이 피어 화면에서만 재생된다 (멀티에서 다른 피어에게 이펙트를 보내지는 않는다).
/// </summary>
public static class CombatEffects
{
    public static void Play(ItemDataSO item, Vector3 position, Quaternion rotation)
    {
        if (item == null || string.IsNullOrEmpty(item.attackEffectKey)) return;
        PlayAsync(item.attackEffectKey, position, rotation, item.attackEffectScale, item.attackEffectLifetime).Forget();
    }

    public static void Play(ItemDataSO item, Vector3 position)
        => Play(item, position, Quaternion.identity);

    private static async UniTaskVoid PlayAsync(string key, Vector3 position, Quaternion rotation, float scale, float lifetime)
    {
        GameObject fx = await Extensions.SpawnAsync(key);
        if (fx == null) return;

        fx.transform.SetPositionAndRotation(position, rotation);
        fx.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);

        await UniTask.Delay(System.TimeSpan.FromSeconds(Mathf.Max(0.1f, lifetime)), cancellationToken: fx.GetCancellationTokenOnDestroy())
            .SuppressCancellationThrow();

        if (fx != null) Extensions.Despawn(fx);
    }
}
