#if PHOTON_FUSION
using Fusion;
using UnityEngine;

/// <summary>
/// 동물 1마리의 복제 단위. NetworkAnimalDirector의 고정 슬롯 배열에 담긴다.
/// 구조와 이유는 MonsterNetState와 같다(동물마다 NetworkObject를 붙이지 않음).
///
/// Id == 0 이면 빈 슬롯이다.
/// </summary>
public struct AnimalNetState : INetworkStruct
{
    /// <summary>호스트가 발급한 고유 ID. 0이면 빈 슬롯.</summary>
    public ushort Id;

    /// <summary>AnimalCatalog의 CatalogId(1-based). 클라가 어떤 프리팹을 만들지 결정한다.</summary>
    public byte CatalogId;

    /// <summary>현재 재생 중인 애니메이션(AnimalAnimId).</summary>
    public byte AnimId;

    public Vector3 Position;
    public float Yaw;
}
#endif
