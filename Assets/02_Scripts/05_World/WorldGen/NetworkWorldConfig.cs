using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 월드 생성 옵션을 호스트 → 클라이언트로 전달하기 위한 헬퍼.
///
/// 같은 시드+옵션이면 각 클라가 로컬에서 동일 월드를 생성한다(시드 공유 방식).
/// 시드가 어긋나면 서로 다른 월드에서 플레이하게 되므로, 전달 경로를 2개 둔다.
///
/// 1) 세션 속성 (<see cref="ToProperties"/> → <see cref="TryApplyFromSession"/>)
///    - 호스트가 방 생성 시 StartGameArgs.SessionProperties로 실어 보낸다.
///    - 방 참가 직후에는 SessionInfo.Properties가 아직 채워지지 않은 순간이 있어
///      한 번만 읽으면 실패할 수 있다(= 클라가 랜덤 시드로 떨어지던 원인).
/// 2) NetworkPlayerData 복제 상태 (<see cref="WriteToPlayerData"/> → <see cref="TryApplyFromPlayerData"/>)
///    - 호스트가 각 플레이어의 데이터 오브젝트를 스폰할 때 옵션을 함께 기록한다.
///    - Fusion 복제로 확실히 도착하므로 1)이 비어 있어도 시드를 받을 수 있다.
///
/// 실제 사용은 <see cref="WaitAndApplyAsync"/> — 두 경로를 제한 시간까지 번갈아 확인한다.
/// </summary>
public static class NetworkWorldConfig
{
    public const string KeySeed = "wgSeed";
    public const string KeyBranch = "wgBranch";
    public const string KeyLoop = "wgLoop";
    public const string KeySize = "wgSize";

    /// <summary>월드 옵션 → 세션 int 속성 딕셔너리 (호스트가 방 생성 시 사용)</summary>
    public static Dictionary<string, int> ToProperties(
        WorldBranchSetting branch, WorldLoopSetting loop, int seed, WorldSize size)
    {
        return new Dictionary<string, int>
        {
            { KeySeed, seed },
            { KeyBranch, (int)branch },
            { KeyLoop, (int)loop },
            { KeySize, (int)size },
        };
    }

    /// <summary>
    /// 세션 속성에서 월드 옵션을 읽어 WorldGenRequest에 반영한다.
    /// 시드 속성이 아직 안 왔으면(호스트가 안 실었거나 복제 전이면) false.
    /// </summary>
    public static bool TryApplyFromSession()
    {
        if (Main.Network == null) return false;
        if (!Main.Network.TryGetSessionInt(KeySeed, out int seed)) return false;

        Main.Network.TryGetSessionInt(KeyBranch, out int branch);
        Main.Network.TryGetSessionInt(KeyLoop, out int loop);
        Main.Network.TryGetSessionInt(KeySize, out int size);

        Apply(seed, branch, loop, size, "세션 속성");
        return true;
    }

    /// <summary>기존 호출부 호환용 (내부적으로 <see cref="TryApplyFromSession"/>).</summary>
    public static bool ApplyFromSession() => TryApplyFromSession();

#if PHOTON_FUSION
    /// <summary>
    /// 호스트가 플레이어 데이터 오브젝트에 월드 옵션을 기록한다. (onBeforeSpawned에서 호출)
    /// </summary>
    public static void WriteToPlayerData(NetworkPlayerData data, IReadOnlyDictionary<string, int> properties)
    {
        if (data == null || properties == null) return;
        if (!properties.TryGetValue(KeySeed, out int seed)) return;

        properties.TryGetValue(KeyBranch, out int branch);
        properties.TryGetValue(KeyLoop, out int loop);
        properties.TryGetValue(KeySize, out int size);

        data.WorldSeed = seed;
        data.WorldBranch = branch;
        data.WorldLoop = loop;
        data.WorldSizeIndex = size;
        data.HasWorldConfig = true;
    }

    /// <summary>
    /// 로컬 플레이어 데이터(호스트가 기록해 복제해준 값)에서 월드 옵션을 읽어 반영한다.
    /// </summary>
    public static bool TryApplyFromPlayerData()
    {
        NetworkPlayerData data = Main.Network != null ? Main.Network.LocalPlayerData : null;
        if (data == null || !data.HasWorldConfig) return false;

        Apply(data.WorldSeed, data.WorldBranch, data.WorldLoop, data.WorldSizeIndex, "플레이어 데이터");
        return true;
    }
#else
    public static bool TryApplyFromPlayerData() => false;
#endif

    /// <summary>
    /// 호스트가 정한 월드 옵션이 도착할 때까지 기다렸다가 WorldGenRequest에 반영한다.
    /// 세션 속성과 플레이어 데이터 두 경로를 번갈아 확인한다.
    /// </summary>
    /// <returns>제한 시간 내에 옵션을 받았으면 true</returns>
    public static async UniTask<bool> WaitAndApplyAsync(
        float timeoutSeconds = 10f, CancellationToken token = default)
    {
        if (Main.Network == null || !Main.Network.IsInRoom) return false;

        float elapsed = 0f;
        const float interval = 0.1f;

        while (true)
        {
            if (TryApplyFromSession()) return true;
            if (TryApplyFromPlayerData()) return true;

            if (elapsed >= timeoutSeconds) break;

            await UniTask.Delay(
                System.TimeSpan.FromSeconds(interval), DelayType.UnscaledDeltaTime, cancellationToken: token);
            elapsed += interval;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogError(
            $"[NetworkWorldConfig] 호스트의 월드 시드를 {timeoutSeconds}초 안에 받지 못했습니다. " +
            "이대로 진행하면 호스트와 다른 월드가 생성됩니다.");
        LogSessionProperties();
#endif
        return false;
    }

    // 실제 반영 + 로그 (양쪽 피어의 시드를 로그로 비교할 수 있게 항상 남긴다)
    private static void Apply(int seed, int branch, int loop, int size, string source)
    {
        WorldGenRequest.Set((WorldBranchSetting)branch, (WorldLoopSetting)loop, seed, (WorldSize)size);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NetworkWorldConfig] 월드 옵션 수신({source}) — seed {seed}, " +
                  $"branch {(WorldBranchSetting)branch}, loop {(WorldLoopSetting)loop}, size {(WorldSize)size}");
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>현재 세션 속성 상태를 로그로 남긴다 (시드 공유 실패 원인 추적용).</summary>
    public static void LogSessionProperties()
    {
        if (Main.Network == null)
        {
            Debug.LogWarning("[NetworkWorldConfig] Main.Network 가 없습니다.");
            return;
        }

        bool hasSeed = Main.Network.TryGetSessionInt(KeySeed, out int seed);
        Debug.LogWarning($"[NetworkWorldConfig] 세션 속성 상태 — InRoom {Main.Network.IsInRoom}, " +
                         $"IsHost {Main.Network.IsHost}, {KeySeed} {(hasSeed ? seed.ToString() : "없음")}");
    }
#endif
}
