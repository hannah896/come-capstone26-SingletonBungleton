using System.Collections.Generic;

public class WorldGenContext
{
    public WorldSettings Settings;
    public WorldGraphData GraphData;
    public WorldLogicData LogicData;
    public List<PlacementData> PlacementDatas = new();

    public void Clear()
    {
        Settings = null;
        GraphData = null;
        LogicData = null;
        PlacementDatas.Clear();
    }
}