using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using UnityEngine;

public class ForceSimulator : IGraphPipelineStage
{
    private const float DEFAULT_PADDING = 0.1f;
    private const float MIN_DIMENSION_SIZE = 0.1f;

    private WorldSettings _worldSettings;
    private System.Random _prng;
    private WorldGraphData _simulationResult;
    private CancellationToken _ct;
    
    private ForceSimParameters _macroParameter;
    private ForceSimParameters _microParameter;

    private Dictionary<RegionData, float> _regionMultipliers;


    #region ForceSim Parameters
    [System.Serializable]
    public class ForceSimParameters
    {
        [Header("Force Simulation")]
        public int simulationIterations;
        public float repulsionStrength;
        public float attractionStrength;
        public float idealEdgeLength;
        public float dampingFactor;
        public float minNodeDistance;
    }
    #endregion

    public void Initialize(WorldSettings settings)
    {
        _worldSettings = settings;
        _prng = new System.Random(settings.WorldSeed + (int)WorldSeedChannel.ForceSimulator);
    }


    public async UniTask<WorldGraphData> ExecuteAsync(
        WorldGraphData graphData, 
        CancellationToken ct)
    { 
        _simulationResult = graphData;
        _ct = ct;

        // 0. 초기 세팅
        Vector2Int mapSize = _worldSettings.GetWorldSize();
        int nodeCount = graphData.Nodes.Count;
        
        // 시작(Region) 방들만 따로 추출 (초기 배치 및 Region 카운트 용도)
        List<Node> startRoomNodes = _simulationResult.Nodes.Where(n => n.RoomDepth == 1).ToList();
        int regionCount = Mathf.Max(1, startRoomNodes.Count);

        float mapArea = mapSize.x * mapSize.y;

        // Macro 기준: Region 1개당 평균 면적의 제곱근 (큼직한 덩어리 배치용)
        float regionUnit = Mathf.Sqrt(mapArea / regionCount);
        // Micro 기준: 방 1개당 평균 면적의 제곱근 (오밀조밀한 방배치용)
        float roomUnit = Mathf.Sqrt(mapArea / Mathf.Max(1, nodeCount));
        
        ForceSimulatorSettings simSettings = _worldSettings.ForceSimulatorSettings;
        int calculatedIterations = Mathf.Clamp(
            (int)(nodeCount * simSettings.iterationMultiplier),
            simSettings.minIterations,
            simSettings.maxIterations
        );
        // 동적으로 세팅값 생성
        _macroParameter = CreateDynamicSettings(
            unit: regionUnit,      // Region 단위 길이를 사용
            lengthRatio: simSettings.macroLengthRatio,         // Region 공간의 60%를 이상적 거리로
            repulsionFactor: simSettings.macroRepulsionFactor,      // 척력 기본값 유지 (구역 간 간격은 너무 멀어지지 않도록)
            iterations: calculatedIterations // 통합 시뮬은 한 번만 돌리므로 반복수 넉넉히
        );

        _microParameter = CreateDynamicSettings(
            unit: roomUnit,         // Node 단위 길이를 사용
            lengthRatio: simSettings.microLengthRatio,        // Node 공간의 80%를 이상적 거리로
            repulsionFactor: simSettings.microRepulsionFactor,    // 뭉치지 않게 방어 척력 증가
            iterations: calculatedIterations
        );

        // region 별 방 개수 계산 (Force 시뮬레이션에서 팽창 배수로 활용)
        Dictionary<RegionData, int> clusterSizes = _simulationResult.Nodes
            .Where(n => n.RegionData != null)
            .GroupBy(n => n.RegionData)
            .ToDictionary(g => g.Key, g => g.Count());

        int smallestRoomCount = clusterSizes.Values.Min();
        _regionMultipliers = new Dictionary<RegionData, float>();

        foreach (var kvp in clusterSizes)
        {
            float ratio = (float)kvp.Value / smallestRoomCount;
            _regionMultipliers[kvp.Key] = ratio;//Mathf.Sqrt(ratio);

        }

        //  1단계: 뼈대 세우기 (Region 단위로 거시적 노드 배치)
        await ArrangeRegionNodes(startRoomNodes);

        //  2단계: 살 붙이기 (배치된 시작 방들 주변에 나머지 방들을 흩뿌림)
        await ScatterRoomsByRegionAsync(startRoomNodes);

        //  3단계: 통합 Force Simulation (관계성에 따라 Macro/Micro 동적 적용)
        _simulationResult.Nodes = await RunForceSimulationUnifiedAsync(_simulationResult.Nodes, _ct);

        //  4단계: 루프 절단
        await CutLoopConnections();

        //  5단계: 타일 그리드에 맞게 노드 위치 조정 
        await FitNodesToTileGridAsync();

        return _simulationResult;
    }


    #region Phase 1: Arrange Region Nodes
    /// <summary>
    /// [Arrange Region Nodes 단계 ]
    /// Region Node들을 물리적으로 펼쳐서 배치 (Macro Layout)
    /// </summary>
    private async UniTask ArrangeRegionNodes(List<Node> startRoomNodes)
    {
        int regionCount = startRoomNodes.Count;

        //시작 region과 마지막 region이 연결되어 있지 않다면 루프가 아니라고 판단
        if (!_simulationResult.IsLooped)
        {
            foreach (var node in _simulationResult.Nodes)
            {
                if (node.Depth == 1) // Start Node의 경우 중심 근처 배치
                    node.Position = GetInsideUnitCircle() * _macroParameter.idealEdgeLength;
                else
                {
                    Node parentNode = _simulationResult.GetParentNode(node);
                    var safeDirection = GetDirectionAwayFromGrandparent(node, parentNode, _simulationResult.NodeConnections);
                    
                    // Region 단위 길이를 기준으로 쭉쭉 뻗어 나감
                    node.Position = safeDirection * _macroParameter.idealEdgeLength;
                }
            }
        }
        else
        {
            // [원형 배치] 도넛 모양 만들기
            var sortedNodes = startRoomNodes.OrderBy(n => n.Depth).ToList();

            // Macro 세팅의 거리를 기반으로 원의 둘레 및 반지름 산출
            float circumference = regionCount * _macroParameter.idealEdgeLength;
            float radius = circumference / (2 * Mathf.PI);

            radius = Mathf.Max(radius, _macroParameter.idealEdgeLength * 2);

            for (int i = 0; i < regionCount; i++)
            {
                float angle = i * (360f / regionCount) * Mathf.Deg2Rad;
                sortedNodes[i].Position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(0.1f), cancellationToken: _ct);
    }
    #endregion

    #region Phase 2 : Region 내 Room Node 배치 (Micro Layout)
    /// <summary>
    /// [Scatter Rooms 단계]
    /// </summary>
    private async UniTask ScatterRoomsByRegionAsync(List<Node> startNodes)
    {
        foreach (var node in _simulationResult.Nodes)
        {
            if (node.RoomDepth == 1) continue;

            Node parentNode = _simulationResult.GetParentNode(node);

            if (parentNode != null)
            {
                // Micro 기준(방 단위)으로 흩뿌림
                float scatterRadius = _microParameter.idealEdgeLength * _regionMultipliers.GetValueOrDefault(node.RegionData, 1f);

                var safeDirection = GetDirectionAwayFromGrandparent(node, parentNode, _simulationResult.NodeConnections);

                Vector2 randomOffset = safeDirection * scatterRadius;
                node.Position = parentNode.Position + randomOffset;
            }
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(0.1f), cancellationToken: _ct);
    }
    #endregion

    #region Phase 3 : Unified Force Simulation
    /// <summary>
    /// 단일 통합 Force Simulation: 노드 관계에 따라 서로 다른 세팅(Macro/Micro)을 적용합니다.
    /// </summary>
    private async UniTask<List<Node>> RunForceSimulationUnifiedAsync(
        List<Node> nodes,
        CancellationToken ct)
    {
        int totalIterations = _macroParameter.simulationIterations;

        for (int iteration = 0; iteration < totalIterations; iteration++)
        {
            ct.ThrowIfCancellationRequested();

            foreach (var node in nodes)
                node.Force = Vector2.zero;

            // 관계(구역)에 따라 척력(자석), 인력(용수철)을 분기처리하여 한 번에 계산
            nodes = CalculateRepulsionForcesUnified(nodes);
            CalculateAttractionForcesUnified(_simulationResult.NodeConnections);
            nodes = UpdateNodePositionsUnified(nodes);

            if (_worldSettings.EnableStepByStep && iteration % 10 == 0)
                await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: ct);
        }

        return nodes;
    }

    // 노드간 척력 계산 (자석의 같은 극)
    private List<Node> CalculateRepulsionForcesUnified(List<Node> nodes)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                Vector2 direction = nodes[i].Position - nodes[j].Position;
                float distSqr = direction.sqrMagnitude;
                if (distSqr < 0.01f) distSqr = 0.01f;

                float currentRepulsion;
                
                bool isSameRegion = nodes[i].RegionData != null && nodes[i].RegionData == nodes[j].RegionData;
                
                if (isSameRegion)
                {
                    // 같은 구역: Micro 단위 척력
                    currentRepulsion = _microParameter.repulsionStrength * _regionMultipliers[nodes[i].RegionData];
                }
                else
                {
                    // 다른 구역(거시적): Macro 단위 척력 적용 (구역끼리는 서로 거리를 크게 둠)
                    currentRepulsion = _macroParameter.repulsionStrength;

                    if (nodes[i].RegionData != null && nodes[j].RegionData != null)
                        currentRepulsion *= _regionMultipliers[nodes[i].RegionData] * _regionMultipliers[nodes[j].RegionData];
                }

                float forceMagnitude = currentRepulsion / distSqr;
                Vector2 force = direction.normalized * forceMagnitude;

                nodes[i].Force += force;
                nodes[j].Force -= force;
            }
        }
        return nodes;
    }

    // 노드 연결에 대한 인력 계산 (용수철)
    private void CalculateAttractionForcesUnified(List<NodeConnection> connections)
    {
        foreach (var edge in connections)
        {
            Vector2 direction = edge.ChildNode.Position - edge.ParentNode.Position;

            float currentIdealLength;
            float currentAttraction;

            bool isSameRegion = edge.ParentNode.RegionData != null && edge.ParentNode.RegionData == edge.ChildNode.RegionData;

            if (isSameRegion)
            {
                // 지역 내부 방들의 연결 -> 짧은 간격 유지
                currentIdealLength = _microParameter.idealEdgeLength * _regionMultipliers[edge.ParentNode.RegionData];
                currentAttraction = _microParameter.attractionStrength;
            }
            else
            {
                // 지역과 지역을 잇는 통로(Bridge) -> Region 간격(Macro) 유지
                currentIdealLength = _macroParameter.idealEdgeLength;
                currentAttraction = _macroParameter.attractionStrength;
            }

            float displacement = direction.magnitude - currentIdealLength;
            Vector2 force = direction.normalized * (currentAttraction * displacement);

            edge.ParentNode.Force += force;
            edge.ChildNode.Force -= force;
        }
    }

    // 노드 위치 업데이트 및 최소 거리 유지
    private List<Node> UpdateNodePositionsUnified(List<Node> nodes)
    {
        // 속도 허용치는 큰 범위인 Macro 설정을 기준으로 함
        
        float damping = _macroParameter.dampingFactor;

        foreach (var node in nodes)
        {
            float mul = _regionMultipliers.TryGetValue(node.RegionData, out var val) ? val : 1f;
            float maxVelocity = _macroParameter.idealEdgeLength * mul;
            float maxForce = _macroParameter.idealEdgeLength * mul;

            if (node.Force.magnitude > maxForce)
                node.Force = node.Force.normalized * maxForce;

            node.Velocity = (node.Velocity + node.Force) * damping;

            if (node.Velocity.magnitude > maxVelocity)
                node.Velocity = node.Velocity.normalized * maxVelocity;
                
            node.Position += node.Velocity;
        }
        // 물리적 겹침 방지 (Hard Collision)
        // 충돌 보정 벡터를 저장할 배열을 만들어 한 번에 적용합니다.
        Vector2[] collisionOffsets = new Vector2[nodes.Count];

        // 물리적 겹침 방지 
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                Vector2 direction = nodes[i].Position - nodes[j].Position;
                float distance = direction.magnitude;

                bool isSameRegion = nodes[i].RegionData != null && nodes[i].RegionData == nodes[j].RegionData;
                float currentMinDist = isSameRegion ? _microParameter.minNodeDistance : _macroParameter.minNodeDistance;

                if (distance < currentMinDist)// && distance > 0.01f)
                {
                    // 거리가 0일 경우 겹쳐서 튕겨내지 못하는 상황 방지
                    if (distance < 0.001f)
                    {
                        direction = GetInsideUnitCircle();
                        distance = 0.001f;
                    }

                    float overlap = currentMinDist - distance;
                    // 노드의 깊이(Depth) 등에 따라 가중치를 주면 중심 노드가 덜 흔들리게 할 수 있습니다.
                    Vector2 push = direction.normalized * (overlap * 0.5f);

                    collisionOffsets[i] += push;
                    collisionOffsets[j] -= push;
                    //float overlap = currentMinDist - distance;
                    //Vector2 push = direction.normalized * (overlap / 2f);

                    //nodes[i].Position += push;
                    //nodes[j].Position -= push;
                }
            }
        }
        for (int i = 0; i < nodes.Count; i++)
        {
            nodes[i].Position += collisionOffsets[i];
        }

        return nodes;
    }
    #endregion

    #region Phase 4: Loop Cutting
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

    #region Phase 5: Fit Nodes to Grid
    /// <summary>
    /// 노드들을 타일 그리드에 맞게 스케일링 및 이동하여 배치
    /// </summary>
    /// <returns></returns>
    private async UniTask FitNodesToTileGridAsync()
    {
        var nodes = _simulationResult.Nodes;
        if (nodes == null || nodes.Count == 0) return;

        // 1. 현재 노드들의 범위(Bounds) 계산
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (var node in nodes)
        {
            if (node.Position.x < minX) minX = node.Position.x;
            if (node.Position.x > maxX) maxX = node.Position.x;
            if (node.Position.y < minY) minY = node.Position.y;
            if (node.Position.y > maxY) maxY = node.Position.y;
        }

        // 2. 현재 그래프의 크기
        float currentWidth = maxX - minX;
        float currentHeight = maxY - minY;

        if (currentWidth < MIN_DIMENSION_SIZE) currentWidth = 1f;
        if (currentHeight < MIN_DIMENSION_SIZE) currentHeight = 1f;

        // 3. 목표 월드 크기 (가장자리에 여백 둠)
        float targetWidth = _worldSettings.GetWorldSize().x * (1f - DEFAULT_PADDING * 2);
        float targetHeight = _worldSettings.GetWorldSize().y * (1f - DEFAULT_PADDING * 2);

        // 4. 스케일 비율 계산 (비율 유지하면서 꽉 차게)
        float scaleX = targetWidth / currentWidth;
        float scaleY = targetHeight / currentHeight;
        float finalScale = Mathf.Min(scaleX, scaleY);

        // 5. 중심점 이동 계산
        Vector2 currentCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        Vector2 targetCenter = new Vector2(_worldSettings.GetWorldSize().x * 0.5f, _worldSettings.GetWorldSize().y * 0.5f);

        // 6. 좌표 변환 적용
        foreach (var node in nodes)
        {
            Vector2 relativePos = node.Position - currentCenter;
            node.Position = targetCenter + (relativePos * finalScale);
        }

        // 7. 검증
        ValidateNodePositions(nodes);

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }

    /// <summary>
    /// 노드 위치 검증
    /// </summary>
    private void ValidateNodePositions(List<Node> nodes)
    {
        if (nodes == null || nodes.Count == 0) return;

        bool hasInvalidNode = false;
        foreach (var node in nodes)
        {
            if (node.Position.x < 0 || node.Position.x > _worldSettings.GetWorldSize().x ||
                node.Position.y < 0 || node.Position.y > _worldSettings.GetWorldSize().y)
            {
                Debug.LogError($"🚨 노드가 맵 범위를 벗어남: {node.Position} (맵 크기: {_worldSettings.GetWorldSize()})");
                hasInvalidNode = true;
            }
        }

        if (!hasInvalidNode)
        {
            Debug.Log($"✅ 모든 노드가 맵 범위 내에 배치됨 (첫 노드: {nodes[0].Position})");
        }
    }


    #endregion

    #region Helpers
    private Vector2 GetInsideUnitCircle()
    {
        // 1. 0 ~ 360도(2π) 사이의 랜덤 각도
        float angle = (float)_prng.NextDouble() * Mathf.PI * 2f;

        // 2. 중심에 몰리지 않도록 루트(Sqrt)를 씌워서 반지름 거리 계산
        float r = Mathf.Sqrt((float)_prng.NextDouble());

        // 3. 삼각함수로 X, Y 좌표 변환
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
    }

    private Vector2 GetDirectionAwayFromGrandparent(
        Node targetNode,
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
            baseDirection = GetInsideUnitCircle().normalized;
            // 0 벡터 방지
            if (baseDirection == Vector2.zero) baseDirection = Vector2.up;
        }

        // 2. 랜덤 각도 생성 ( -120도 ~ +120도 )
        float randomAngle;
        if (targetNode.RoomDepth <= 3)
        {
            // 방 깊이가 2 이하일 때(초기 확산 및 Region 단위 배치) 넓게 퍼짐 (-120도 ~ +120도)
            randomAngle = _prng.Next(-90, 91);
        }
        else
        {
            // 깊이가 3 이상부터는 튀는 애들 없이 앞으로만 곧게 뻗어 나감 (-15도 ~ +15도 미세 분산)
            randomAngle = _prng.Next(-60, 61);
        }

        // 3. 벡터 회전 
        // Z축을 기준으로 randomAngle만큼 회전시킴
        Quaternion rotation = Quaternion.Euler(0, 0, randomAngle);
        Vector2 finalDirection = rotation * baseDirection;

        return finalDirection.normalized;
    }
    private ForceSimParameters CreateDynamicSettings(float unit, float lengthRatio, float repulsionFactor, int iterations)
    {
        float targetEdgeLength = unit * lengthRatio;

        return new ForceSimParameters
        {
            simulationIterations = iterations,
            dampingFactor = 0.85f, // 0.8~0.9 사이의 고정 감쇠 계수

            // 거리는 mapUnit의 특정 퍼센트로 계산
            idealEdgeLength = targetEdgeLength,
            minNodeDistance = targetEdgeLength * 0.4f, // 목표 거리의 40% 이내면 충돌로 간주

            // 물리학 기반: 척력(k)은 목표 거리의 제곱 ✕ 팩터
            // 이렇게 해야 거리가 targetEdgeLength일 때 밀어내는 힘이 repulsionFactor로 일정해짐
            repulsionStrength = (targetEdgeLength * targetEdgeLength) * repulsionFactor,

            // 인력은 오차에 곱해지는 스프링 상수이므로 스케일에 무관한 고정 비율 사용
            attractionStrength = 0.2f
        };
    }

    #endregion

}
