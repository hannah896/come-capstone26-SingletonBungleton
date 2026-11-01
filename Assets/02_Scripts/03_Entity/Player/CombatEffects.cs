using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 무기의 공격 이펙트(파티클/VFX 프리팹)를 맞은 자리에 한 번 재생하고 일정 시간 뒤 풀로 돌려보낸다.
/// 이펙트 프리팹은 Addressables 키로 지정한다 (ItemDataSO.attackEffectKey).
/// 이 피어 화면에서만 재생된다 (멀티에서 다른 피어에게 이펙트를 보내지는 않는다).
/// </summary>
public static class CombatEffects
{
    /// <summary>
    /// 이 피어의 플레이어가 공격 이펙트를 재생했을 때 발생한다 (키, 위치, 크기, 유지 시간).
    /// 멀티플레이 계층(NetworkPlayerSync)이 구독해 다른 피어에도 같은 이펙트를 보여준다.
    /// </summary>
    public static event System.Action<string, Vector3, float, float> LocalEffectPlayed;

    public static void Play(ItemDataSO item, Vector3 position, Quaternion rotation)
    {
        if (item == null || string.IsNullOrEmpty(item.attackEffectKey)) return;

        PlayAsync(item.attackEffectKey, position, rotation, item.attackEffectScale, item.attackEffectLifetime).Forget();
        LocalEffectPlayed?.Invoke(item.attackEffectKey, position, item.attackEffectScale, item.attackEffectLifetime);
    }

    /// <summary>다른 피어가 보낸 이펙트를 재생한다. (다시 네트워크로 내보내지 않는다)</summary>
    public static void PlayRemote(string key, Vector3 position, float scale, float lifetime)
    {
        if (string.IsNullOrEmpty(key)) return;
        PlayAsync(key, position, Quaternion.identity, scale, lifetime).Forget();
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
