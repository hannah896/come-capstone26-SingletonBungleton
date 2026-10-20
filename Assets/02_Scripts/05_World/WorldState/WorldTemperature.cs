using UnityEngine;

/// <summary>월드 위치가 속한 바이옴의 기본 기온을 섭씨로 조회합니다.</summary>
public static class WorldTemperature
{
    public const float DefaultTemperature = 20f;

    /// <summary>현재 월드의 기온을 반환하며, 조회할 수 없으면 기본 기온을 사용합니다.</summary>
    public static float GetTemperature(Vector3 worldPosition)
    {
        TryGetTemperature(worldPosition, out float temperature);
        return temperature;
    }

    /// <summary>현재 월드에서 조회합니다. 월드 미준비·바다·맵 밖이면 false와 기본 기온을 반환합니다.</summary>
    public static bool TryGetTemperature(Vector3 worldPosition, out float temperature)
    {
        temperature = DefaultTemperature;
        WorldGenManager world = WorldGenManager.Instance;
        if (world == null || world.CurrentLogicData == null) return false;

        WorldGraphDirector graph = world.GraphDirector;
        if (graph == null || !graph.HasWorldData) return false;

        return TryGetTemperature(world.CurrentLogicData, graph.GetWorldGraphData(), worldPosition, out temperature);
    }

    /// <summary>지정한 월드 데이터에서 조회합니다. 높이와 청크 로드 여부는 기온에 영향을 주지 않습니다.</summary>
    public static bool TryGetTemperature(
        WorldLogicData logicData,
        WorldGraphData graphData,
        Vector3 worldPosition,
        out float temperature)
    {
        temperature = DefaultTemperature;
        if (logicData?.TerritoryWorld == null || graphData?.Nodes == null) return false;

        // 정수 변환 전에 범위를 검사해 음수 소수 좌표와 NaN·무한대도 제외합니다.
        if (!(worldPosition.x >= 0f && worldPosition.x < logicData.TerrainSize.x &&
              worldPosition.z >= 0f && worldPosition.z < logicData.TerrainSize.y)) return false;

        int x = Mathf.FloorToInt(worldPosition.x);
        int z = Mathf.FloorToInt(worldPosition.z);
        int regionIndex = logicData.GetRegionAt(x, z);
        if (regionIndex < 0 || regionIndex >= graphData.Nodes.Count) return false;

        BiomeData biome = graphData.Nodes[regionIndex]?.BiomeData;
        if (biome == null) return false;

        temperature = biome.BaseTemperature;
        return true;
    }
}
