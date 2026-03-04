using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

public class ForceSimulator
{
    private WorldSettings _worldSettings;
    private WorldGraphData _simulationResult;
    private CancellationToken _ct;


    private ForceSimSettings _macroSettings;
    private ForceSimSettings _microSettings;
    private ForceSimSettings _fastSettings;

    private Dictionary<RegionData, float> _regionMultipliers;

    public async UniTask<WorldGraphData> ForceSimulateAsync(
        WorldGraphData result,
        WorldSettings settings,
        CancellationToken ct)
    {
        _worldSettings = settings;
        _simulationResult = result;
        _ct = ct;

        _macroSettings = settings.MacroSettings;
        _microSettings = settings.MicroSettings;
        _fastSettings = settings.FastSettings;

        // ★ 1단계: 뼈대 세우기 (Region 단위로 노드 배치)
        List<Node> startRoomNodes = await ArrangeRegionNodes();

        //region 별 방 개수 계산 (Force 시뮬레이션에서 힘 조절할 때 활용)
        Dictionary<RegionData, int> clusterSizes = _simulationResult.Nodes
            .Where(n => n.RegionData != null)
            .GroupBy(n => n.RegionData)
            .ToDictionary(g => g.Key, g => g.Count());

        // 가장 방이 적은 구역의 개수 (기준점)
        int minRoomCount = Mathf.Max(1, clusterSizes.Values.Min());

        // 구역별 팽창 배수 미리 계산
        _regionMultipliers = new();

        foreach (var kvp in clusterSizes)
        {
            float ratio = (float)kvp.Value / minRoomCount;
            _regionMultipliers[kvp.Key] = ratio;
        }

        // ★ 2단계: 살 붙이기 (배치된 시작 방들 주변에 나머지 방들을 흩뿌림)
        await ScatterRoomsByRegionAsync(startRoomNodes);

        //_simulationResult.Nodes = await RunForceSimulationAsync(_simulationResult.Nodes, _macroSettings, _ct);

        // ★ 3단계: 팝콘 튀기기
        _simulationResult.Nodes = await RunForceSimulationAsync(_simulationResult.Nodes, _fastSettings, _ct);

        // ★ 4단계: 최종 안정화 
        _simulationResult.Nodes = await RunForceSimulationAsync(_simulationResult.Nodes, _microSettings, _ct);

        // ★ 5단계: 루프 절단 (마지막으로 부모-자식 관계를 Depth 기준으로 정리하여 루프 제거)
        await CutLoopConnections();

        return _simulationResult;
    }


    /// <summary>
    /// [Arrange Region Nodes 단계 ]
    /// Region Node들을 물리적으로 펼쳐서 배치 (Macro Layout)
    /// </summary>
    /// <returns></returns>
    #region Phase 1: Arrange Region Nodes

    private async UniTask<List<Node>> ArrangeRegionNodes()
    {
        var seedChannel = (int)WorldSeedChannel.Force_ArrangeRegionNodes;
        var prng = new System.Random(_worldSettings.WorldSeed + seedChannel);

        // 1. 노드들을 RegionData 기준으로 그룹화
        var startRoomNodes = _simulationResult.Nodes.Where(n => n.RoomDepth == 1).ToList();
        int regionCount = startRoomNodes.Count;

        //시작 region과 마지막 region이 연결되어 있지 않다면 루프가 아니라고 판단
        if (!_simulationResult.IsLooped)
        {
            foreach (var node in _simulationResult.Nodes)
            {
                if (node.Depth == 1) // Start Node의 경우 작은 범위 내에 배치
                    node.Position = GetInsideUnitCircle(prng) * _microSettings.idealEdgeLength;
                else
                    node.Position = GetInsideUnitCircle(prng) * _macroSettings.idealEdgeLength;
            }
        }
        else
        {
            // [원형 배치] 도넛 모양 만들기
            // Depth 순서대로 정렬 (시작 -> 중간 -> 끝)
            var sortedNodes = startRoomNodes.OrderBy(n => n.Depth).ToList();

            // 적절한 반지름 계산 (노드 사이 간격을 유지하며 원을 만들 크기)
            float circumference = regionCount * _macroSettings.idealEdgeLength;
            float radius = circumference / (2 * Mathf.PI);

            // 반지름이 너무 작으면 뭉치므로 최소값 보장
            radius = Mathf.Max(radius, _macroSettings.idealEdgeLength * 2);

            for (int i = 0; i < regionCount; i++)
            {
                // 각도 계산 (0도 ~ 360도)
                float angle = i * (360f / regionCount) * Mathf.Deg2Rad;
                // 원형 좌표 할당
                sortedNodes[i].Position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(0.1f), cancellationToken: _ct);

        return startRoomNodes;
    }

    #endregion

    #region Phase 2 : Region 내 Room Node 배치 (Micro Layout)
    private async UniTask ScatterRoomsByRegionAsync(List<Node> startNodes)
    {
        var seedChannel = (int)WorldSeedChannel.Force_ArrangeRegionNodes + 1;
        var prng = new System.Random(_worldSettings.WorldSeed + seedChannel);

        foreach (var node in _simulationResult.Nodes)
        {
            // RoomDepth 1인 방은 1단계에서 이미 자리를 잡았으므로 건너뜀
            if (node.RoomDepth == 1) continue;

            // 내가 속한 구역의 시작 방을 찾음
            var myStartNode = startNodes.FirstOrDefault(s => s.RegionData == node.RegionData);

            if (myStartNode != null)
            {
                // 중심점 주변으로 오밀조밀하게 흩뿌림
                float scatterRadius = _microSettings.idealEdgeLength * _regionMultipliers[node.RegionData];

                Vector2 randomOffset = GetInsideUnitCircle(prng) * scatterRadius;
                node.Position = myStartNode.Position + randomOffset;
            }
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(0.1f), cancellationToken: _ct);
    }
    #endregion

    /// <summary>
    /// 들어온 노드들에대해 Force Simulation을 실행합니다.
    /// </summary>
    /// <param name="graph"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    #region Force Simulation
    private async UniTask<List<Node>> RunForceSimulationAsync(
        List<Node> nodes,
        ForceSimSettings forceSettings,
        CancellationToken ct)
    {
        for (int iteration = 0; iteration < forceSettings.simulationIterations; iteration++)
        {
            ct.ThrowIfCancellationRequested();

            foreach (var node in nodes)
                node.Force = Vector2.zero;

            nodes = CalculateRepulsionForces(nodes, forceSettings);
            CalculateAttractionForces(_simulationResult.NodeConnections, forceSettings);
            nodes = UpdateNodePositions(nodes, forceSettings);

            if (_worldSettings.EnableStepByStep && iteration % 10 == 0)
                await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: ct);
        }

        return nodes;
    }


    // 노드간 척력 계산
    private List<Node> CalculateRepulsionForces(List<Node> nodes, ForceSimSettings forceSettings)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                Vector2 direction = nodes[i].Position - nodes[j].Position;
                float distSqr = direction.sqrMagnitude;
                // 0 나누기 방지
                if (distSqr < 0.01f) distSqr = 0.01f;

                float currentRepulsion = forceSettings.repulsionStrength;

                // ★ 두 방이 같은 구역 소속이라면, 미리 계산해둔 배수를 곱함
                if (nodes[i].RegionData != null && nodes[i].RegionData == nodes[j].RegionData)
                {
                    currentRepulsion *= _regionMultipliers[nodes[i].RegionData];
                }

                // 공식: Force = Strength / distance^2
                float forceMagnitude = currentRepulsion / distSqr;
                Vector2 force = direction.normalized * forceMagnitude;

                nodes[i].Force += force;
                nodes[j].Force -= force;
            }
        }

        return nodes;
    }

    // 노드 연결에 대한 인력 계산
    private void CalculateAttractionForces(List<NodeConnection> connections, ForceSimSettings forceSettings)
    {
        foreach (var edge in connections)
        {
            Vector2 direction = edge.ChildNode.Position - edge.ParentNode.Position;

            float currentIdealLength = forceSettings.idealEdgeLength;

            // ★ 다리(Region 간 연결)가 아닌, 구역 내부의 연결선인 경우 배수 적용
            if (edge.ParentNode.RegionData != null && edge.ParentNode.RegionData == edge.ChildNode.RegionData)
            {
                currentIdealLength *= _regionMultipliers[edge.ParentNode.RegionData];
            }

            float displacement = direction.magnitude - currentIdealLength;
            Vector2 force = direction.normalized * (forceSettings.attractionStrength * displacement);

            edge.ParentNode.Force += force;
            edge.ChildNode.Force -= force;
        }
    }


    // 노드 위치 업데이트 및 최소 거리 유지
    private List<Node> UpdateNodePositions(List<Node> nodes, ForceSimSettings forceSettings)
    {
        float maxVelocity = forceSettings.idealEdgeLength * 1.0f; // 한 턴에 목표 거리 이상 날아가지 못함
        float maxForce = forceSettings.idealEdgeLength * 3.0f;    // 억눌린 척력이 너무 쌓이지 않도록 제한

        foreach (var node in nodes)
        {
            // 1. 힘(Force) 폭발 방지
            if (node.Force.magnitude > maxForce)
                node.Force = node.Force.normalized * maxForce;

            // 2. 속도(Velocity) 계산 (마찰력 적용)
            node.Velocity = (node.Velocity + node.Force) * forceSettings.dampingFactor;

            // 3. 우주 미아 방지 (최대 속도 제한)
            if (node.Velocity.magnitude > maxVelocity)
                node.Velocity = node.Velocity.normalized * maxVelocity;
            node.Position += node.Velocity;
        }


        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                Vector2 direction = nodes[i].Position - nodes[j].Position;
                float distance = direction.magnitude;

                if (distance < forceSettings.minNodeDistance && distance > 0.01f)
                {
                    float overlap = forceSettings.minNodeDistance - distance;

                    Vector2 push = direction.normalized * (overlap / 2f);

                    nodes[i].Position += push;
                    nodes[j].Position -= push;
                }
            }
        }

        return nodes;
    }
    #endregion

    #region Helpers
    private Vector2 GetInsideUnitCircle(System.Random prng)
    {
        // 1. 0 ~ 360도(2π) 사이의 랜덤 각도
        float angle = (float)prng.NextDouble() * Mathf.PI * 2f;

        // 2. 중심에 몰리지 않도록 루트(Sqrt)를 씌워서 반지름 거리 계산
        float r = Mathf.Sqrt((float)prng.NextDouble());

        // 3. 삼각함수로 X, Y 좌표 변환
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
    }

    //private Vector2 GetDirectionAwayFromGrandparent(
    //    Node parentNode,
    //    List<NodeConnection> existingConnections
    //    )
    //{
    //    Vector2 baseDirection;

    //    // 1. 할아버지 노드 찾기 (부모가 Child로 되어 있는 연결 찾기)
    //    var parentConnection = existingConnections.FirstOrDefault(c => c.ChildNode == parentNode);

    //    // 랜덤 시드 설정 (부모 노드의 인덱스 + 메서드 채널 번호)
    //    var seedChannel = (int)WorldSeedChannel.Region_GetDirectionAwayFromGrandparent;
    //    var prng = new System.Random(_worldSettings.WorldSeed + seedChannel);

    //    if (parentConnection != null)
    //    {
    //        // 할아버지 -> 부모 방향 (이게 '전방' 기준)
    //        baseDirection = (parentNode.Position - parentConnection.ParentNode.Position).normalized;
    //    }
    //    else
    //    {
    //        // 할아버지가 없음 (부모가 시작 방임) -> 그냥 완전 랜덤 방향
    //        baseDirection = GetInsideUnitCircle(prng).normalized;
    //        // 0 벡터 방지
    //        if (baseDirection == Vector2.zero) baseDirection = Vector2.up;
    //    }

    //    // 2. 랜덤 각도 생성 ( -120도 ~ +120도 )
    //    float randomAngle = prng.Next(-120, 121);

    //    // 3. 벡터 회전 
    //    // Z축을 기준으로 randomAngle만큼 회전시킴
    //    Quaternion rotation = Quaternion.Euler(0, 0, randomAngle);
    //    Vector2 finalDirection = rotation * baseDirection;

    //    return finalDirection.normalized;
    //}
    #endregion

    #region Phase 3: Loop Cutting
    private async UniTask CutLoopConnections()
    {
        // 1. 연결 정보에서 Depth가 높은 쪽이 부모인 연결을 모두 제거 (루프 절단)
        int removedCount = _simulationResult.NodeConnections.RemoveAll(conn =>
            conn.ParentNode.Depth > conn.ChildNode.Depth
        );

        // 연결 정보 갱신 (필수)
        _simulationResult.RebuildAdjacency();

        if (removedCount > 0)
            Debug.Log($"[RegionGen] 루프 연결 {removedCount}개를 절단했습니다.");

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }
    #endregion

}
