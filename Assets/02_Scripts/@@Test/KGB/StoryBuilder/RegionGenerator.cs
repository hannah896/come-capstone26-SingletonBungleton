using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

/// <summary>
/// 단일 Task의 Room 그래프를 생성하는 클래스
/// </summary>
public class RegionGenerator
{
    

    [Header("Settings")]
    
    private WorldSettings _worldSettings;

    // 2. 내부 변수
    private ForceSimSettings _macroSettings;
    private ForceSimSettings _microSettings;
    private ForceSimSettings _fastSettings;

    private GraphResult _regionResult;
    private CancellationToken _ct;

    //// Region Node에 생성된 Cluster 대한 매핑
    private Dictionary<Node, List<Node>> _regionToCluster = new();

    /// <summary>
    /// 메인 진입점 : RegionData 기반으로 Room 그래프 생성
    /// </summary>
    public async UniTask<GraphResult> ConvertRegionsToRoomsAsync(
        GraphResult result, 
        WorldSettings settings,
        CancellationToken ct)
    {
        try
        {
            _regionResult = result;
            _worldSettings = settings;

            _macroSettings = settings.MacroSettings;
            _microSettings = settings.MicroSettings;
            _fastSettings = settings.FastSettings;

            _ct = ct;
            // 1: Task Node 배치 (Macro Layout)
            // 논리적 그래프만 있는 상태이므로, Region Node들을 물리적으로 펼쳐줍니다.
            await ArrangeRegionNodes();

            // 2 : 각 Region Node를 Room Cluster로 변환
            await GenerateInternalTopology();

            // 3 : Force Simulation으로 Node들 배치 (Fast Layout)
            _regionResult.Nodes = await RunForceSimulationAsync(_regionResult.Nodes, _fastSettings, _ct);

            // 4 : Loop 생성
            await CreateLoopsAsync();

            // Phase 5 : 
            _regionResult.Nodes = await RunForceSimulationAsync(_regionResult.Nodes, _microSettings, _ct);

            // 오프셋 적용 및 결과 생성
            return _regionResult;
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log($"Region 생성이 취소되었습니다.");
            return _regionResult;
        }
    }
    #region Phase 0: Arrange Region Nodes

    private async UniTask ArrangeRegionNodes()
    {
        // 1. 초기화: 0,0에 뭉쳐있지 않게 랜덤하게 살짝 흩뿌림 (Start는 0,0 고정)
        foreach (var node in _regionResult.Nodes)
        {
            if (node.Depth == 1) // Start Node
                node.Position = Random.insideUnitCircle * _microSettings.idealEdgeLength;
            else
                node.Position = Random.insideUnitCircle * _macroSettings.idealEdgeLength;
        }
        //2. Force Simulation 실행
        _regionResult.Nodes = await RunForceSimulationAsync(_regionResult.Nodes,_macroSettings, _ct);


        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(0.1f), cancellationToken: _ct);
    }

    #endregion

    #region 1. Room Cluster Generation
    private async UniTask GenerateInternalTopology()
    {
        // 1. [Snapshot] 원본 Region 데이터 복사 (루프용 & 참조용)
        List<Node> regionNodesSnapshot = _regionResult.Nodes.OrderBy(n => n.Depth).ToList();
        List<NodeConnection> regionConnectionsSnapshot = _regionResult.NodeConnections.ToList();

        // 2. [Clear] 결과 컨테이너 초기화
        _regionResult.Clear();
        _regionToCluster.Clear();

        List<Node> allRoomNodes = new List<Node>();
        List<NodeConnection> allRoomConnections = new List<NodeConnection>();
        foreach (Node regionNode in regionNodesSnapshot)
        {
            //A. 해당 Region에 대한 Room Cluster 및 연결 생성,  Data 참조
            List<Node> cluster = new();
            List<NodeConnection> clusterConnections = new();
            RegionData regionData = regionNode.RegionData;

            _regionToCluster[regionNode] = cluster;


            //B. 기본 방 개수 결정
            int defaultRoomCount = Random.Range(regionData.DefaultMinCount, regionData.DefaultMaxCount + 1);

            //C. 시작 방 선정(입구 방 우선, 없는 경우 DefaultRooms 중 랜덤)
            RoomData startRoomData = regionData.EntranceRoom;
            if (startRoomData == null)
            {
                if (regionData.DefaultRooms != null && regionData.DefaultRooms.Count > 0)
                    startRoomData = regionData.DefaultRooms[Random.Range(0, regionData.DefaultRooms.Count)];
                else
                {
                    // DefaultRoom도 없으면 에러 혹은 기본값 처리
                    Debug.LogError($"Region {regionData.RegionName} has no rooms!");
                    return;
                }
            }
            //D. 입구 방 없는 경우 default 방 중 랜덤하게 선택하여 시작 방으로 설정
            Node startRoom = new()
            {
                Position = regionNode.Position,
                RegionData = regionData,
                RoomData = startRoomData,
                Depth = regionNode.Depth,
                RoomDepth = 1
            };
            cluster.Add(startRoom);
            // E. 기본 방 배치 루프(Essential Room 개수 만큼 루프 늘린 후 Essential Room 배치 시 기존 방과 스왑하는 형식)
            while (cluster.Count < defaultRoomCount + regionData.EssentialRooms.Count)
            {
                // E1. 부모 노드 선정
                Node parentNode = PickParentRoom(cluster, regionData.RoomBranch);

                // 방어 코드: 더 이상 붙일 곳이 없으면 중단 
                if (parentNode == null) break;

                // E2. 새 방 생성
                // 위치: 부모 위치 기준 + 랜덤 방향 (parentNode의 parent 방향은 피해서 ,Micro Setting 거리)
                Vector2 safeDirection = GetDirectionAwayFromGrandparent(
                    parentNode,
                    clusterConnections
                    );

                Vector2 newOffset = parentNode.Position + (safeDirection * _microSettings.idealEdgeLength);

                Node newRoom = new Node
                {
                    Position = newOffset,
                    RegionData = regionData,
                    RoomData = regionData.GetRandomDefaultRoom(),
                    Depth = parentNode.Depth,               // Region Depth
                    RoomDepth = parentNode.RoomDepth + 1    // Local Depth
                };

                // E3. 등록 및 연결
                cluster.Add(newRoom);
                clusterConnections.Add(new(parentNode, newRoom));
            }

            // F. 필수 방 배치(스왑)
            if (regionData.EssentialRooms != null)
            {
                int clusterMaxDepth = cluster.Max(r => r.RoomDepth);
                int minRange = 1;
                int maxRange = clusterMaxDepth;
                foreach (EssentialRoomEntry essential in regionData.EssentialRooms)
                {
                    RoomData essentialRoomData = essential.RoomData;
                    RoomDepth targetDepth = essential.Depth;

                    switch (targetDepth)
                    {
                        case RoomDepth.Early:
                            {
                                minRange = 1;
                                maxRange = Mathf.Max(1, Mathf.FloorToInt(clusterMaxDepth * 0.25f));
                                break;
                            }
                        case RoomDepth.Mid:
                            {
                                minRange = Mathf.Max(1, Mathf.FloorToInt(clusterMaxDepth * 0.45f));
                                maxRange = Mathf.Max(minRange, Mathf.FloorToInt(clusterMaxDepth * 0.55f));
                                break;
                            }
                        case RoomDepth.Late:
                            {
                                minRange = Mathf.Max(1, Mathf.FloorToInt(clusterMaxDepth * 0.8f));
                                maxRange = Mathf.Max(minRange, Mathf.FloorToInt(clusterMaxDepth * 0.9f));
                                break;
                            }
                    }

                    var candidates = cluster.Where(n =>
                        n.RoomDepth >= minRange &&
                        n.RoomDepth <= maxRange
                        ).ToList();

                    Node targetNode = null;

                    if (candidates.Count > 0)
                    {
                        // 후보가 있으면 그 중에서 랜덤 선택
                        targetNode = candidates[Random.Range(0, candidates.Count)];
                    }
                    else
                    {
                        // Fallback: 범위 내에 방이 없다면(맵이 작을 때), 
                        // 목표 깊이(이상적인 중간값)와 가장 가까운 '빈 방'을 찾음
                        int idealDepth = (minRange + maxRange) / 2;

                        targetNode = cluster
                            .OrderBy(n => Mathf.Abs(n.RoomDepth - idealDepth)) // 깊이 차이가 적은 순 정렬
                            .FirstOrDefault();
                    }

                    if (targetNode != null)
                    {
                        targetNode.RoomData = essentialRoomData;
                    }
                    else
                    {
                        Debug.LogWarning($"[{regionData.RegionName}] 필수 방({essentialRoomData.name})을 배치할 공간이 부족합니다.");
                    }
                }
            }

            await ConnectToParentRegion(regionNode, cluster, clusterConnections, regionConnectionsSnapshot);

            if (_worldSettings.EnableStepByStep)
                await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
        }// foreach RegionNodes 끝
    }

    
    #region Helper for Phase 1
    private Vector2 GetDirectionAwayFromGrandparent(
        Node parentNode,
        List<NodeConnection> existingConnections
        )
    {
        Vector2 baseDirection;

        // 1. 할아버지 노드 찾기 (부모가 Child로 되어 있는 연결 찾기)
        var parentConnection = existingConnections.FirstOrDefault(c => c.ChildNode == parentNode);

        if (parentConnection != null)
        {
            // 할아버지 -> 부모 방향 (이게 '전방' 기준)
            baseDirection = (parentNode.Position - parentConnection.ParentNode.Position).normalized;
        }
        else
        {
            // 할아버지가 없음 (부모가 시작 방임) -> 그냥 완전 랜덤 방향
            baseDirection = Random.insideUnitCircle.normalized;
            // 0 벡터 방지
            if (baseDirection == Vector2.zero) baseDirection = Vector2.up;
        }

        // 2. 랜덤 각도 생성 ( -120도 ~ +120도 )
        float randomAngle = Random.Range(-120f, 120f);

        // 3. 벡터 회전 
        // Z축을 기준으로 randomAngle만큼 회전시킴
        Quaternion rotation = Quaternion.Euler(0, 0, randomAngle);
        Vector2 finalDirection = rotation * baseDirection;

        return finalDirection.normalized;
    }

    // TODO: RoomBranch(Least/Most/Default)에 따라 부모를 고르는 전략(나중에 region 
    private Node PickParentRoom(List<Node> placedRooms, RegionBranch branch)
    {
        switch (branch)
        {
            case RegionBranch.Least: // 뱀 (Line)
                // 가장 최근에 만든 방(List의 마지막)에만 계속 붙임 -> 일직선 됨
                return placedRooms.Last();

            case RegionBranch.Most: // 성게 (Hub / Star)
                // Depth가 가장 낮은 방(입구 근처) 위주로 선택 -> 뭉침
                return placedRooms.OrderBy(n => n.RoomDepth).ThenBy(n => Random.value).First();

            case RegionBranch.Default: // 랜덤 (Tree)
            default:
                // 아무거나 랜덤 선택 (단, 너무 입구쪽만 걸리지 않게 약간의 가중치 조절 가능)
                return placedRooms[Random.Range(0, placedRooms.Count)];
        }
    }

    #endregion
    #endregion

    #region 2. Connect to Parent Region
    private async UniTask ConnectToParentRegion(
        Node currentRegionNode,
        List<Node> cluster, 
        List<NodeConnection> clusterConnections,
        List<NodeConnection> regionConnSnapshot) 
    {
        //  1. 클러스터 및 연결 등록
        _regionResult.Nodes.AddRange(cluster);
        _regionResult.NodeConnections.AddRange(clusterConnections);

        //  2. 부모 노드 찾기 (child가 currentRegionNode인 연결 찾기)
        var regionLink = regionConnSnapshot.FirstOrDefault(c => c.ChildNode == currentRegionNode);
        //  부모 노드가 없으면 (Start Region인 경우) 여기서 종료
        if (regionLink == null) 
            return;

        Node parentRegionNode = regionLink.ParentNode;

        
        //  3. 부모 Region의 방 목록 조회
        if (!_regionToCluster.TryGetValue(parentRegionNode, out var parentCluster))
        {
            Debug.LogError($"[순서 오류] Region '{currentRegionNode.RegionData.RegionName}'의 부모가 아직 생성되지 않았습니다.");
            return;
        }

        // 4. 부모 노드의 '출구' <-> 나의 '입구'
        Node myEntrance = cluster.OrderBy(n => n.RoomDepth).First();           // 나의 입구: 가장 깊이 낮은 방
        Node parentExit = parentCluster.OrderByDescending(n => n.RoomDepth).First();  // 부모 출구: 가장 깊이 높은 방

        //  5. [다리 놓기] Bridge Connection 생성 및 등록
        NodeConnection bridgeConnection = new NodeConnection(parentExit, myEntrance);
        _regionResult.NodeConnections.Add(bridgeConnection);



        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }
    #endregion

    #region 3. Create Loops
    private async UniTask CreateLoopsAsync()
    {
        // 1. 설정 확인 (Never면 패스)
        if (_worldSettings.WorldLoop == WorldLoopSetting.Never) return;

        _regionResult.RebuildAdjacency();

        // 2. 루프 확률 가져오기 (0.0 ~ 1.0)
        float loopChance = _worldSettings.GetLoopMultiplier();
        float connectRange = _microSettings.idealEdgeLength * 1.5f;
        float connectRangeSqr = connectRange * connectRange;

        
        List<Node> nodes = _regionResult.Nodes;
        List<NodeConnection> newConnections = new List<NodeConnection>();

        // 3. 모든 노드 쌍을 검사 
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                Node nodeA = nodes[i];
                Node nodeB = nodes[j];

                // 조건 A: 같은 Region끼리만 연결할 것인가? (보통 같은 Region 내에서 진행)
                if (nodeA.RegionData != nodeB.RegionData) continue;

                // 조건 B: 이미 연결되어 있는가?
                if (_regionResult.AreConnected(nodeA, nodeB)) continue;

                // 조건 C: 거리가 가까운가?
                float distSqr = (nodeA.Position - nodeB.Position).sqrMagnitude;
                if (distSqr > connectRangeSqr) continue;

                // 조건 D: 확률 체크
                if (Random.value < loopChance)
                {
                    // 연결 생성
                    newConnections.Add(new NodeConnection(nodeA, nodeB));

                    // 한 노드에서 너무 많은 루프가 생기는 걸 방지하려면 여기서 break 또는 확률 감소 로직 추가 가능
                }
            }
        }

        // 4. 생성된 루프 연결을 그래프에 반영
        if (newConnections.Count > 0)
        {
            // AdjacencyList 갱신 (AreConnected가 올바르게 작동하려면 필요)
            // _regionResult.CreateConnection 메서드를 쓰는 게 더 안전할 수 있음
            foreach (var conn in newConnections)
            {
                // CreateConnection 내부에서 AdjacencyList에 Add 함
                _regionResult.CreateConnection(conn.ParentNode, conn.ChildNode);
            }
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);

    }
    #endregion


    #region Force Simulation
        /// <summary>
        /// 들어온 노드들에대해 Force Simulation을 실행합니다.
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
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
            CalculateAttractionForces(_regionResult.NodeConnections, forceSettings);
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
                float distance = Mathf.Sqrt(distSqr);

                // 공식: Force = Strength / distance^2
                float forceMagnitude = forceSettings.repulsionStrength / distSqr;
                Vector2 force = direction.normalized * forceMagnitude;

                nodes[i].Force += force;
                nodes[j].Force -= force;
            }
        }

        return nodes;
    }

    private void CalculateAttractionForces(List<NodeConnection> connections, ForceSimSettings forceSettings)
    {
        foreach (var edge in connections)
        {
            Vector2 direction = edge.ChildNode.Position - edge.ParentNode.Position;
            float displacement = direction.magnitude - forceSettings.idealEdgeLength;
            Vector2 force = direction.normalized * (forceSettings.attractionStrength * displacement);
            
            edge.ParentNode.Force += force;
            edge.ChildNode.Force -= force;
        }
    }

    private List<Node> UpdateNodePositions(List<Node> nodes, ForceSimSettings forceSettings)
    {
        foreach (var node in nodes)
        {
            if (node.Depth == 1 && node.RoomDepth == 1)                 //시작 노드는 고정
                continue;
            node.Velocity = (node.Velocity + node.Force) * forceSettings.dampingFactor;
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

  


}

