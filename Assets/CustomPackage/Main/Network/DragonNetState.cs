#if PHOTON_FUSION
using Fusion;

/// <summary>
/// 최종 보스(월식룡) 보스전 공유 상태. NetworkMonsterDirector가 하나만 복제한다.
/// 보스 위치·애니·체력은 일반 몬스터 슬롯(MonsterNetState)으로 가고, 여기에는 호스트만 계산하는 보스전 상태를 싣는다.
/// RPC가 아니라 네트워크 상태라 늦게 들어온 피어도 현재 상태를 그대로 본다. (기획 8.3 ⑥)
/// </summary>
public struct DragonNetState : INetworkStruct
{
    public const byte FlagShootdown = 1 << 0; // 게이지가 격추 게이지
    public const byte FlagGroggy    = 1 << 1;
    public const byte FlagEclipse   = 1 << 2; // 월식(전환 ~ 2페이즈) — 조명
    public const byte FlagEnraged   = 1 << 3;
    public const byte FlagHasBoss   = 1 << 4; // 아래 보스 값이 유효한지

    public byte Flags;
    /// <summary>무력화(격추) 게이지 비율 0~255.</summary>
    public byte Poise;
    /// <summary>광폭화까지 남은 시간 (0.1초 단위). ushort.MaxValue면 광폭화 없음.</summary>
    public ushort EnrageLeft;
    /// <summary>기둥 부식 횟수. DragonCover.GetSorted 순번마다 4비트씩 (최대 8개).</summary>
    public uint PillarCorrosion;

    /// <summary>각인 대상 플레이어 오브젝트 (없으면 default).</summary>
    public NetworkId Mark0;
    public NetworkId Mark1;
    /// <summary>각인 폭발까지 남은 시간 (0.1초 단위).</summary>
    public ushort Mark0Left;
    public ushort Mark1Left;
}
#endif
