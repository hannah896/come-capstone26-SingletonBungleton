#if PHOTON_FUSION
using Fusion;
using UnityEngine;

/// <summary>
/// 몬스터 1마리의 복제 단위. NetworkMonsterDirector의 고정 슬롯 배열에 담긴다.
///
/// 몬스터마다 NetworkObject를 붙이지 않는 이유는 개수(수십~백 단위)와 풀링 충돌 때문이다.
/// 대신 디렉터 하나가 이 구조체 배열을 복제하고, 각 피어가 로컬 몬스터를 만들어 재현한다.
/// 상세는 docs/WorkSummary.md 참고.
///
/// Id == 0 이면 빈 슬롯이다. (슬롯을 앞으로 당겨 채우지 않는다 — 델타 압축을 최대한 살리기 위해
/// 한 마리가 죽어도 그 칸만 비우고 나머지 슬롯은 건드리지 않는다)
/// </summary>
public struct MonsterNetState : INetworkStruct
{
    /// <summary>호스트가 발급한 고유 ID. 0이면 빈 슬롯.</summary>
    public ushort Id;

    /// <summary>MonsterCatalog의 CatalogId(1-based). 클라가 어떤 프리팹을 만들지 결정한다.</summary>
    public byte CatalogId;

    /// <summary>현재 재생 중인 애니메이션(MonsterAnimId). 클라는 이걸로 애니를 재현한다.</summary>
    public byte AnimId;

    public Vector3 Position;
    public float Yaw;
}
#endif
