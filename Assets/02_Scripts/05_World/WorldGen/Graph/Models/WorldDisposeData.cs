using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 월드에 실제로 배치될 오브젝트/아이템 들의 정보를 담는 클래스
/// </summary>
public class WorldDisposeData
{
    // 총 배치된 오브젝트/아이템 에 대한 리스트
    public List<DisposeData> DisposeDatas = new();
    // 배치된 오브젝트에 대한 리스트
    public List<DisposeData> ObjectDisposes = new();
    // 배치된 아이템에 대한 리스트
    public List<DisposeData> ItemDisposes = new();
    public List<WorldInitialMonsterPlacement> InitialMonsterPlacements = new();
    // 프리팹 메타데이터에서 읽은 스폰 소스입니다. 청크 인스턴스 생성 여부와 무관합니다.
    public List<WorldMonsterSpawnSourcePlacement> MonsterSpawnSources = new();

    public void Clear()
    {
        DisposeDatas.Clear();
        ObjectDisposes.Clear();
        ItemDisposes.Clear();
        InitialMonsterPlacements.Clear();
        MonsterSpawnSources.Clear();
    }

}
