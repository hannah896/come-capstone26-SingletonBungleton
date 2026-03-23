using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

// 그래프 구조 생성 담당 (GlobalDirector 전용, 1회 실행)
public interface IGraphPipelineStage
{
    
    void Initialize(WorldSettings settings);  // 클래스별 채널 관리는 각자 책임
    UniTask<WorldGraphData> ExecuteAsync(
        WorldGraphData graphData,
        CancellationToken ct);
}