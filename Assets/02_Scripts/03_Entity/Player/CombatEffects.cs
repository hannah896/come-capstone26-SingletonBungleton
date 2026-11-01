using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 무기의 공격 이펙트(파티클/VFX 프리팹)를 맞은 자리에 한 번 재생하고 일정 시간 뒤 풀로 돌려보낸다.
/// 이펙트 프리팹은 Addressables 키로 지정한다 (ItemDataSO.attackEffectKey).
/// 로컬 플레이어가 재생하면 <see cref="PlayerNetFx"/>를 거쳐 다른 피어 화면에도 같은 이펙트가 재생된다.
/// </summary>
public static class CombatEffects
{
    public static void Play(ItemDataSO item, Vector3 position, Quaternion rotation)
    {
        if (item == null || string.IsNullOrEmpty(item.attackEffectKey)) return;
        PlayAsync(item.attackEffectKey, position, rotation, item.attackEffectScale, item.attackEffectLifetime).Forget();
        PlayerNetFx.ShareEffect(item.attackEffectKey, position, rotation, item.attackEffectScale, item.attackEffectLifetime);
    }

    /// <summary>다른 피어가 재생한 이펙트를 이 화면에 재현한다 (NetworkPlayerSync 전용 — 다시 전파하지 않는다).</summary>
    public static void PlayRemote(string key, Vector3 position, Quaternion rotation, float scale, float lifetime)
    {
        if (string.IsNullOrEmpty(key)) return;
        PlayAsync(key, position, rotation, scale, lifetime).Forget();
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
