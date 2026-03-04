using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Triggers;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using UnityEngine;
using static UnityEditor.Experimental.AssetDatabaseExperimental.AssetDatabaseCounters;

/// <summary>
/// 단일 Task의 Room 그래프를 생성하는 클래스
/// </summary>
public class RegionGenerator
{
    private WorldSettings _worldSettings;
    private WorldGraphData _regionResult;
    private CancellationToken _ct;




    //// Region Node에 생성된 Cluster 대한 매핑
    private Dictionary<Node, List<Node>> _regionToCluster = new();

    /// <summary>
    /// 메인 진입점 : RegionData 기반으로 Room 그래프 생성
    /// </summary>
    public async UniTask<WorldGraphData> ConvertRegionsToRoomsAsync(
        WorldGraphData result, 
        WorldSettings settings,
        CancellationToken ct)
    {
        try
        {
            _regionResult = result;
            _worldSettings = settings;

            _ct = ct;

            // 1 : 각 Region Node를 Room Cluster로 변환
            await GenerateInternalTopology();

            // 2 : Region 내부에 루프 연결 생성 
            await CreateLoopsAsync();

            // 오프셋 적용 및 결과 생성
            return _regionResult;
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log($"Region 생성이 취소되었습니다.");
            return _regionResult;
        }
    }




    /// <summary>
    ///  [Room Cluster 생성 단계]
    ///  각 Region Node를 중심으로 Room Cluster를 생성하고, Region 간 연결을 Cluster 간 연결로 변환
    /// </summary>
    /// <returns></returns>
    #region Phase 1: Room Cluster Generation
    private async UniTask GenerateInternalTopology()
    {
        // 1. [Snapshot] 원본 Region 데이터 복사 (루프용 & 참조용)
        List<Node> regionNodesSnapshot = _regionResult.Nodes.OrderBy(n => n.Depth).ToList();
        List<NodeConnection> regionConnectionsSnapshot = _regionResult.NodeConnections.ToList();

        // 2. [Clear] 결과 컨테이너 초기화
        _regionResult.Clear();
        _regionToCluster.Clear();

        foreach (Node regionNode in regionNodesSnapshot)
        {
            // 2.1 [Generate] Region Node를 Room Cluster로 변환
            var (cluster, clusterConnections) = GenerateRoomsInRegion(regionNode);

            _regionToCluster[regionNode] = cluster;
            
            if (cluster.Count > 0)
            {
                _regionResult.Nodes.AddRange(cluster);
                _regionResult.NodeConnections.AddRange(clusterConnections);
            }

            if (_worldSettings.EnableStepByStep)
                await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
        }// foreach RegionNodes 끝

        // 3. [Bridge] Region 간 연결 생성 (Region Node 간 연결을 Cluster 간 연결로 변환)
        foreach (var conn in regionConnectionsSnapshot)
        {
            Node parentRegion = conn.ParentNode;
            Node childRegion = conn.ChildNode;

            if (_regionToCluster.TryGetValue(parentRegion, out var parentCluster) && parentCluster.Count > 0 &&
                _regionToCluster.TryGetValue(childRegion, out var childCluster) && childCluster.Count > 0)
            {
                // 부모의 출구(Exit) <-> 자식의 입구(Entrance)
                Node bridgeStart = parentCluster.OrderByDescending(n => n.RoomDepth).First();
                Node bridgeEnd = childCluster.OrderBy(n => n.RoomDepth).First();

                // 다리 연결 추가
                _regionResult.NodeConnections.Add(new NodeConnection(bridgeStart, bridgeEnd));
            }
            else
            {
                Debug.LogError($"[RegionGen] 치명적 오류: Region 클러스터를 찾을 수 없음 ({parentRegion.RegionData.RegionName} -> {childRegion.RegionData.RegionName})");
            }

            if (_worldSettings.EnableStepByStep)
                await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
        }
    }


    private (List<Node> cluster, List<NodeConnection> clusterConnections) GenerateRoomsInRegion(Node regionNode)
    {
        List<Node> cluster = new();
        List<NodeConnection> clusterConnections = new();
        RegionData regionData = regionNode.RegionData;

        // 1. 시작 방 생성
        RoomData startRoomData = regionData.EntranceRoom ?? regionData.GetRandomDefaultRoom();
        if (startRoomData == null) return (cluster, clusterConnections);

        Node startRoom = new()
        {
            RegionData = regionData,
            RoomData = startRoomData,
            Depth = regionNode.Depth,
            RoomDepth = 1,
        };
        cluster.Add(startRoom);

        // 2. 방 확장 루프
        var seedChannel = (int)WorldSeedChannel.Region_GenerateRoomsForRegion;
        var prng = new System.Random(_worldSettings.WorldSeed + seedChannel);

        int defaultRoomCount = prng.Next(regionData.DefaultMinCount, regionData.DefaultMaxCount + 1) + regionData.EssentialRooms.Count;


        while (cluster.Count < defaultRoomCount)
        {
            Node parentNode = PickParentRoom(cluster, regionData.RoomBranch);
            if (parentNode == null) break;


            Node newRoom = new Node
            {
                RegionData = regionData,
                RoomData = regionData.GetRandomDefaultRoom() ?? startRoomData,
                Depth = parentNode.Depth,
                RoomDepth = parentNode.RoomDepth + 1
            };

            cluster.Add(newRoom);
            clusterConnections.Add(new NodeConnection(parentNode, newRoom));
        }

        // 3. 필수 방 배치 (Swap)
        if (regionData.EssentialRooms != null && regionData.EssentialRooms.Count > 0)
        {
            AssignEssentialRooms(cluster, regionData);
        }

        return (cluster, clusterConnections);
    }
    private Node PickParentRoom(List<Node> placedRooms, RegionBranch branch)
    {
        var seedChannel = (int)WorldSeedChannel.Region_PickParentRoom;
        var prng = new System.Random(_worldSettings.WorldSeed + seedChannel);
        switch (branch)
        {
            case RegionBranch.Least: // 뱀 (Line)
                // 가장 최근에 만든 방(List의 마지막)에만 계속 붙임 -> 일직선 됨
                return placedRooms.Last();

            case RegionBranch.Most: // 성게 (Hub / Star)
                // Depth가 가장 낮은 방(입구 근처) 위주로 선택 -> 뭉침
                return placedRooms.OrderBy(n => n.RoomDepth).ThenBy(n => prng.NextDouble()).First();

            case RegionBranch.Default: // 랜덤 (Tree)
            default:
                // 아무거나 랜덤 선택 (단, 너무 입구쪽만 걸리지 않게 약간의 가중치 조절 가능)
                return placedRooms[prng.Next(0, placedRooms.Count)];
        }
    }

    private void AssignEssentialRooms(List<Node> clusters, RegionData regionData)
    {
        int clustersMaxDepth = clusters.Max(r => r.RoomDepth);
        int minRange = 1, maxRange = clustersMaxDepth;

        var seedChannel = (int)WorldSeedChannel.Region_AssignEssentialRooms;
        var prng = new System.Random(_worldSettings.WorldSeed + seedChannel);

        foreach (EssentialRoomEntry essential in regionData.EssentialRooms)
        {
            RoomData essentialRoomData = essential.RoomData;

            switch (essential.Depth)
            {
                case RoomDepth.Early:
                    maxRange = Mathf.Max(1, Mathf.FloorToInt(clustersMaxDepth * 0.25f)); break;
                case RoomDepth.Mid:
                    minRange = Mathf.Max(1, Mathf.FloorToInt(clustersMaxDepth * 0.45f));
                    maxRange = Mathf.Max(minRange, Mathf.FloorToInt(clustersMaxDepth * 0.55f)); break;
                case RoomDepth.Late:
                    minRange = Mathf.Max(1, Mathf.FloorToInt(clustersMaxDepth * 0.8f));
                    maxRange = Mathf.Max(minRange, Mathf.FloorToInt(clustersMaxDepth * 0.9f)); break;
            }

            var candidates = clusters.Where(n => n.RoomDepth >= minRange && n.RoomDepth <= maxRange).ToList();
            Node targetNode = (candidates.Count > 0) ? candidates[prng.Next(0, candidates.Count)] :
                clusters.OrderBy(n => Mathf.Abs(n.RoomDepth - (minRange + maxRange) / 2)).FirstOrDefault();

            if (targetNode != null) targetNode.RoomData = essentialRoomData;
        }

    }
    #endregion

    #region Phase 2: Region Loop Creation
    private async UniTask CreateLoopsAsync()
    {
        // 1. 설정 확인
        if (_worldSettings.WorldLoop == WorldLoopSetting.Never) return;

        // 그래프 갱신
        _regionResult.RebuildAdjacency();

        float loopChance = _worldSettings.GetLoopMultiplier();
        int maxDepthDifference = 1;

        int maxConnectionsPerNode = 3; // 한 방이 가질 수 있는 최대 연결(문) 개수

        var seedChannel = (int)WorldSeedChannel.Region_CreateLoopsAsync;
        var prng = new System.Random(_worldSettings.WorldSeed + seedChannel);

        // _regionToCluster.Values는 각 지역에 속한 노드들의 리스트(Cluster)입니다.
        foreach (List<Node> cluster in _regionToCluster.Values)
        {
            // 빈 클러스터 스킵
            if (cluster == null || cluster.Count < 2) continue;

            int maxLoopsForThisCluster = Mathf.Max(1, Mathf.RoundToInt(cluster.Count * loopChance)); //* 0.4f));
            int currentLoops = 0;

            // 해당 지역(Cluster) 내부에서만 쌍을 검사
            for (int i = 0; i < cluster.Count; i++)
            {
                for (int j = i + 1; j < cluster.Count; j++)
                {
                    if (currentLoops >= maxLoopsForThisCluster) break; 

                    Node nodeA = cluster[i];
                    Node nodeB = cluster[j];

                    // 조건 A: 이미 연결되어 있는가?
                    if (_regionResult.AreConnected(nodeA, nodeB)) continue;

                    // 조건 B: RoomDepth 차이 검사
                    int depthDiff = Mathf.Abs(nodeA.RoomDepth - nodeB.RoomDepth);
                    if (depthDiff > maxDepthDifference) continue;

                    // 조건 C: 각 노드의 연결 수 검사 (너무 많은 연결 방지)
                    int connA = _regionResult.NodeConnections.Count(c => c.ParentNode == nodeA || c.ChildNode == nodeA);
                    int connB = _regionResult.NodeConnections.Count(c => c.ParentNode == nodeB || c.ChildNode == nodeB);
                    if (connA >= maxConnectionsPerNode || connB >= maxConnectionsPerNode) continue;

                    // 조건 D: 같은 Region 내에서만 연결 (부모 노드가 같은지 검사)
                    var parentA = _regionResult.NodeConnections.FirstOrDefault(c => c.ChildNode == nodeA)?.ParentNode;
                    var parentB = _regionResult.NodeConnections.FirstOrDefault(c => c.ChildNode == nodeB)?.ParentNode;
                    if (parentA != parentB) continue;


                    // 최종 : 확률 체크
                    if (prng.NextDouble() < loopChance)
                    {
                        Node parent = (nodeA.RoomDepth <= nodeB.RoomDepth) ? nodeA : nodeB;
                        Node child = (parent == nodeA) ? nodeB : nodeA;

                        _regionResult.CreateConnection(parent, child);
                        currentLoops++;
                    }
                }
            }
        }
        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }
    #endregion

    

}

