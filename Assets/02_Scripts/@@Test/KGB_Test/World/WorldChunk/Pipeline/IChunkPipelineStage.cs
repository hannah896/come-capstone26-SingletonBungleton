using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

// 공간 데이터 생성 담당 (ChunkDirector 전용, 반복 실행)
public interface ILogicPipelineStage
{
    void Initialize(int worldSeed);  // 클래스별 채널 관리는 각자 책임
    UniTask ExecuteAsync(
        ChunkData chunkData,        // 청크 단위 입출력
        WorldGraphData graphData,   // 그래프는 읽기 전용 참조
        WorldSettings settings,
        CancellationToken ct);
}