/// <summary>
/// 로비에서 결정한 월드 생성 옵션을 게임씬으로 전달하기 위한 정적 홀더.
///
/// Main.Clear()는 리플렉션으로 Managers 타입 필드만 비우므로(Main.cs 참고),
/// Manager가 아닌 이 static 데이터는 씬 전환(Cleanup) 중에도 살아남습니다.
/// 시드를 로비에서 미리 확정해두면 재현/공유가 가능합니다.
/// </summary>
public static class WorldGenRequest
{
    public struct Data
    {
        public WorldBranchSetting Branch;
        public WorldLoopSetting Loop;
        public int Seed;
        public WorldSize Size;
    }

    /// <summary>소비되지 않은 생성 요청이 남아 있는지 여부.</summary>
    public static bool HasRequest { get; private set; }

    /// <summary>이번 실행에서 한 번이라도 옵션이 확정된 적이 있는지 여부.
    /// 씬을 다시 로드해도(요청은 이미 소비됨) 같은 시드로 재생성하기 위해 사용한다.</summary>
    public static bool HasData { get; private set; }

    /// <summary>
    /// 마지막으로 확정된 옵션이 "이 세션의 호스트가 정한 값"인지 여부.
    ///
    /// 멀티 세션에서는 이 값이 true인 옵션만 써야 한다. 싱글 플레이(월드 생성 팝업)에서 남은
    /// 이전 요청을 그대로 쓰면 플레이어마다 다른 시드로 월드가 만들어진다.
    /// </summary>
    public static bool IsFromHost { get; private set; }

    /// <summary>호스트가 정한, 아직 소비되지 않은 생성 요청이 있는지 여부.</summary>
    public static bool HasHostRequest => HasRequest && IsFromHost;

    private static Data _data;

    /// <summary>로비에서 선택한 옵션으로 생성 요청을 등록합니다.</summary>
    /// <param name="fromHost">이 세션의 호스트가 확정한 옵션이면 true (멀티 시드 공유 경로).</param>
    public static void Set(WorldBranchSetting branch, WorldLoopSetting loop, int seed,
        WorldSize size = WorldSize.Large, bool fromHost = false)
    {
        _data = new Data
        {
            Branch = branch,
            Loop = loop,
            Seed = seed,
            Size = size
        };
        HasRequest = true;
        HasData = true;
        IsFromHost = fromHost;
    }

    /// <summary>요청 데이터를 읽고 소비합니다(1회성).</summary>
    public static Data Consume()
    {
        HasRequest = false;
        return _data;
    }

    /// <summary>마지막으로 확정된 옵션을 소비하지 않고 읽습니다. (씬 리로드 시 같은 시드 재사용)</summary>
    public static Data Peek() => _data;

    /// <summary>요청을 폐기합니다.</summary>
    public static void Clear() => HasRequest = false;
}
