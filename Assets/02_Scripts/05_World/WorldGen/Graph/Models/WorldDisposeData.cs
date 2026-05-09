using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 월드에 실제로 배치될 오브젝트/아이템 들의 정보를 담는 클래스
/// </summary>
public class WorldDisposeData
{
    // 총 배치된 오브젝트/아이템 에 대한 리스트
    public List<PlacementData> PlacementDatas = new();
    // 배치된 오브젝트에 대한 리스트
    public List<PlacementData> ObjectPlacements = new();
    // 배치된 아이템에 대한 리스트
    public List<PlacementData> ItemPlacements = new();

    public void Clear()
    {
        PlacementDatas.Clear();
        ObjectPlacements.Clear();
        ItemPlacements.Clear();
    }

}
