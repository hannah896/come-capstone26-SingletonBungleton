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

    public void Clear()
    {
        DisposeDatas.Clear();
        ObjectDisposes.Clear();
        ItemDisposes.Clear();
    }

}
