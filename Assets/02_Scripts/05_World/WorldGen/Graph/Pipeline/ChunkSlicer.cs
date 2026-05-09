using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

/// <summary>
/// 6단계: 완성된 전역 맵 데이터와 오브젝트 배치를 청크 단위로 분할하는 작업자
/// </summary>
public class ChunkSlicer : IGraphPipelineStage
{
    public void Initialize(WorldSettings settings)
    {
        // 설정 불필요
    }

    public async UniTask ExecuteAsync(WorldGenContext ctx, CancellationToken ct)
    {
        Debug.Log("📦 [ChunkSlicerStage] 글로벌 맵 데이터 청크 슬라이싱 시작...");

        WorldLogicData logicData = ctx.LogicData;
        int width = logicData.TerrainSize.x;
        int height = logicData.TerrainSize.y;
        int chunkSize = logicData.ChunkSize;

        // =========================================================
        // 1. 지형 데이터 썰기
        // =========================================================

        // 맵 전체를 덮기 위해 가로/세로 몇 개의 청크가 필요한지 계산 (예: 512 / 64 = 8개)
        int chunksX = Mathf.CeilToInt((float)width / chunkSize);
        int chunksY = Mathf.CeilToInt((float)height / chunkSize);

        for (int cx = 0; cx < chunksX; cx++)
        {
            for (int cy = 0; cy < chunksY; cy++)
            {
                Vector2Int chunkCoord = new Vector2Int(cx, cy);
                ChunkData chunk = logicData.GetOrCreateChunk(chunkCoord);
                for (int lx = 0; lx <= chunkSize; lx++)
                {
                    for (int ly = 0; ly <= chunkSize; ly++)
                    {
                        // 로컬 좌표(lx)를 글로벌 좌표(globalX)로 변환
                        int globalX = (cx * chunkSize) + lx;
                        int globalY = (cy * chunkSize) + ly;

                        // 글로벌 맵(예: 513x513)의 범위를 벗어나지 않도록 안전장치 (마지막 청크용)
                        int safeX = Mathf.Min(globalX, width - 1);
                        int safeY = Mathf.Min(globalY, height - 1);

                        // 전역 배열 -> 청크 로컬 배열로 복사
                        // 이때, 청크 A의 lx=64와 청크 B의 lx=0은 똑같은 globalX 데이터를 가져가게 됩니다!
                        chunk.HeightMap[lx, ly] = logicData.HeightWorld[safeX, safeY];
                        chunk.TerritoryMap[lx, ly] = logicData.TerritoryWorld[safeX, safeY];

                        if (logicData.NoiseWorld != null)
                            chunk.NoiseMap[lx, ly] = logicData.NoiseWorld[safeX, safeY];
                    }
                }
            }
        }

        // =========================================================
        // 2. 오브젝트 데이터 청크 분배
        // =========================================================
        int objectCount = 0;
        if (ctx.DisposeData != null)
        {
            foreach (var disposeData in ctx.DisposeData.PlacementDatas)
            {
                Vector2Int chunkCoord = logicData.GetChunkCoord(disposeData.tilePosition.x, disposeData.tilePosition.y);
                ChunkData chunk = logicData.GetOrCreateChunk(chunkCoord);

                chunk.PlacementDatas.Add(disposeData);
                objectCount++;
            }
        }

        Debug.Log($"📦 [ChunkSlicerStage] 청크 슬라이싱 완료! 총 {objectCount}개의 오브젝트 할당됨.");

        await UniTask.Yield(ct);
    }
}
