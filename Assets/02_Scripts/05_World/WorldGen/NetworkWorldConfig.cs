using System.Collections.Generic;

/// <summary>
/// 월드 생성 옵션을 네트워크 세션 int 속성으로 주고받기 위한 헬퍼.
///
/// - 호스트: 방 생성 시 <see cref="ToProperties"/>로 옵션을 세션에 실어 공유.
/// - 클라이언트: 방 참가 후 <see cref="ApplyFromSession"/>로 읽어 WorldGenRequest에 반영.
///
/// 같은 시드+옵션이면 각 클라가 로컬에서 동일 월드를 생성한다(시드 공유 방식).
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
    /// 세션 속성에서 월드 옵션을 읽어 WorldGenRequest에 반영한다(클라이언트가 참가 후 호출).
    /// 시드 속성이 없으면(호스트가 안 실었으면) false.
    /// </summary>
    public static bool ApplyFromSession()
    {
        if (Main.Network == null) return false;
        if (!Main.Network.TryGetSessionInt(KeySeed, out int seed)) return false;

        Main.Network.TryGetSessionInt(KeyBranch, out int branch);
        Main.Network.TryGetSessionInt(KeyLoop, out int loop);
        Main.Network.TryGetSessionInt(KeySize, out int size);

        WorldGenRequest.Set((WorldBranchSetting)branch, (WorldLoopSetting)loop, seed, (WorldSize)size);
        return true;
    }
}
