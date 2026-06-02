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

    private static Data _data;

    /// <summary>로비에서 선택한 옵션으로 생성 요청을 등록합니다.</summary>
    public static void Set(WorldBranchSetting branch, WorldLoopSetting loop, int seed, WorldSize size = WorldSize.Large)
    {
        _data = new Data
        {
            Branch = branch,
            Loop = loop,
            Seed = seed,
            Size = size
        };
        HasRequest = true;
    }

    /// <summary>요청 데이터를 읽고 소비합니다(1회성).</summary>
    public static Data Consume()
    {
        HasRequest = false;
        return _data;
    }

    /// <summary>요청을 폐기합니다.</summary>
    public static void Clear() => HasRequest = false;
}
