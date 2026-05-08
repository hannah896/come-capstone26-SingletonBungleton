using System.Collections.Generic;

/// <summary>
/// 월드 생성 파이프라인에서 각 단계가 공유하는 컨텍스트 객체
/// </summary>
public class WorldGenContext
{
    public WorldSettings Settings;
    public WorldGraphData GraphData;        // WorldGraphDirector가 생성한 그래프 데이터
    public WorldLogicData LogicData;        //
    public WorldDisposeData DisposeData;

    public void Clear()
    {
        Settings = null;
        GraphData = null;
        LogicData = null;
        DisposeData = null;
    }
}