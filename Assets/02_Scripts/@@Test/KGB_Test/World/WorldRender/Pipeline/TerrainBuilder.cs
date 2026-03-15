using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;

public class TerrainBuilder
{
    WorldLogicData _logicData;
    WorldGraphData _graphData;
    WorldSettings _settings;

    public async UniTask<(TerrainData, GameObject)> BuildTerrainAsync(
        WorldLogicData logicData,
        WorldGraphData graphData,
        WorldSettings settings,
        CancellationToken ct)
    {
        Debug.Log("⛰️ [TerrainBuilder] 지형 융기 연산 시작...");

        _logicData = logicData;
        _graphData = graphData;
        _settings = settings;

        int gridX = logicData.TerrainSize.x;
        int gridY = logicData.TerrainSize.y;
        float maxHeight = _settings.GetHeight(HeightLevel.Max);

        // 1. TerrainData 생성 및 해상도 설정
        TerrainData terrainData = new TerrainData();

        // 유니티 지형 해상도는 배열의 크기와 동일해야 함.
        int resolution = Mathf.Max(gridX, gridY);
        terrainData.heightmapResolution = resolution;

        // 2. 실제 월드 사이즈(Transform 크기) 설정
        // 반드시 heightmapResolution을 먼저 설정한 뒤에 size를 줘야 버그가 안 남.
        float worldSizeX = gridX;
        float worldSizeZ = gridY;
        terrainData.size = new Vector3(worldSizeX, maxHeight, worldSizeZ);

        // 3. 높이 데이터 변환 
        // ⚠️ 유니티 SetHeights는 [y, x] 순서의 2차원 배열을 받음
        float[,] unityHeights = new float[resolution, resolution];

        int processedCount = 0;
        int batchSize = 100; // 프레임 방어용

        for (int x = 0; x < gridX; x++)
        {
            for (int y = 0; y < gridY; y++)
            {
                float logicHeight = logicData.HeightWorld[x, y];

                // 0.0f ~ 1.0f 사이의 비율로 정규화 (Normalize)
                float normalizedHeight = logicHeight / maxHeight;

                // 인덱스를 [y, x]로 뒤집어서 대입
                unityHeights[y, x] = normalizedHeight;
            }

            processedCount++;
            if (processedCount % batchSize == 0)
            {
                ct.ThrowIfCancellationRequested();
                await UniTask.Yield(ct);
            }
        }

        // 4. 지형을 한 번에 융기! (가장 무거운 연산)
        terrainData.SetHeights(0, 0, unityHeights);

        // 5. 씬에 Terrain 게임 오브젝트 소환
        GameObject terrainGO = Terrain.CreateTerrainGameObject(terrainData);
        terrainGO.name = "World_Terrain";

        // 맵이 (0,0)에서 시작하도록 설정 (필요 시 중앙 정렬로 변경 가능)
        terrainGO.transform.position = Vector3.zero;

        Debug.Log("⛰️ [TerrainBuilder] 지형 융기 완료!");

        // 칠하기 단계(TerrainPainter)에서 재사용할 수 있도록 반환
        return (terrainData, terrainGO);
    }
}
