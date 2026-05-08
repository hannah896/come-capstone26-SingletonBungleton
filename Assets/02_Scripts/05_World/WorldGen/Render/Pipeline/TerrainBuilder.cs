using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;

public class TerrainBuilder
{
    private const int DEFAULT_DETAIL_RESOLUTION = 1024;
    private const int DEFAULT_DETAIL_RESOLUTION_PER_PATCH = 16;
    private const float DEFAULT_DETAIL_OBJECT_DISTANCE = 200f;

    public async UniTask<(TerrainData, GameObject)> BuildChunkTerrainAsync(
        ChunkData chunk,
        WorldSettings settings,
        GameObject reuseTerrain = null,
        CancellationToken ct = default)
    {
        int chunkSize = settings.ChunkSize; // 64
        float maxHeight = settings.GetHeight(HeightLevel.Max);

        TerrainData terrainData = new TerrainData();
        terrainData.heightmapResolution = chunkSize + 1;
        terrainData.alphamapResolution = chunkSize * 2;
        terrainData.SetDetailResolution(DEFAULT_DETAIL_RESOLUTION, DEFAULT_DETAIL_RESOLUTION_PER_PATCH);   // (a,b) a : 1개의 터레인에 대한 그리드 수, b : 패치 크기 
        terrainData.size = new Vector3(chunkSize, maxHeight, chunkSize);

        float[,] unityHeights = new float[chunkSize + 1, chunkSize + 1];

        // 1. 청크 로컬 데이터만 순회
        for (int x = 0; x <= chunkSize; x++)
        {
            for (int y = 0; y <= chunkSize; y++)
            {
                unityHeights[y, x] = chunk.HeightMap[x, y] / maxHeight;
            }
        }

        terrainData.SetHeights(0, 0, unityHeights);

        // 2. Terrain 게임 오브젝트 생성/재사용
        GameObject terrainGO = reuseTerrain;
        Terrain terrain = null;

        if (terrainGO == null)
        {
            terrainGO = Terrain.CreateTerrainGameObject(terrainData);
            terrain = terrainGO.GetComponent<Terrain>();
        }
        else
        {
            terrainGO.SetActive(true);
            terrain = terrainGO.GetComponent<Terrain>();

            if (terrain == null)
            {
                terrainGO = Terrain.CreateTerrainGameObject(terrainData);
                terrain = terrainGO.GetComponent<Terrain>();
            }
            else
            {
                if (terrain.terrainData != null)
                    Object.Destroy(terrain.terrainData);

                terrain.terrainData = terrainData;

                TerrainCollider collider = terrainGO.GetComponent<TerrainCollider>();
                if (collider != null)
                    collider.terrainData = terrainData;
            }
        }

        terrainGO.name = $"Chunk_Terrain_{chunk.ChunkCoord.x}_{chunk.ChunkCoord.y}";

        // 추가 : 터레인 설정
        terrain.drawTreesAndFoliage = true;
        terrain.detailObjectDensity = 1.0f;     // 0~1
        terrain.detailObjectDistance = DEFAULT_DETAIL_OBJECT_DISTANCE;

        // 3. 핵심: 청크 좌표를 실제 월드 좌표로 변환하여 배치!
        float worldX = chunk.ChunkCoord.x * chunkSize;
        float worldZ = chunk.ChunkCoord.y * chunkSize;
        terrainGO.transform.position = new Vector3(worldX, 0, worldZ);

        return (terrainData, terrainGO);
    }
}
