using System;
using UnityEngine;

/// <summary>
/// 로컬 플레이어가 낸 소리·이펙트를 다른 피어에도 재현하기 위한 허브.
///
/// 원격 캐릭터는 상태 머신을 돌리지 않고 위치·애니메이션만 복제하므로, 휘두르는 소리나 타격 이펙트가 저절로 나지 않는다.
/// 게임 코드는 로컬에서 평소처럼 재생한 뒤 여기에 알리고, 로컬 캐릭터의 NetworkPlayerSync가 구독해 다른 피어에 전파한다.
/// 싱글에서는 구독자가 없어 아무 일도 하지 않는다.
///
/// 발소리·착지음은 복제된 애니메이터 Bool로 원격 쪽이 직접 판단하므로 여기를 거치지 않는다 (<see cref="RemotePlayerAudio"/>).
/// </summary>
public static class PlayerNetFx
{
    /// <summary>로컬 플레이어가 위치가 있는 소리를 냈다 (소리, 위치).</summary>
    public static event Action<AudioLibrarySounds, Vector3> OnLocalSound;

    /// <summary>로컬 플레이어가 이펙트 프리팹을 재생했다 (Addressable 키, 위치, 회전, 스케일, 유지 시간).</summary>
    public static event Action<string, Vector3, Quaternion, float, float> OnLocalEffect;

    public static void ShareSound(AudioLibrarySounds sound, Vector3 position)
        => OnLocalSound?.Invoke(sound, position);

    public static void ShareEffect(string key, Vector3 position, Quaternion rotation, float scale, float lifetime)
        => OnLocalEffect?.Invoke(key, position, rotation, scale, lifetime);
}
