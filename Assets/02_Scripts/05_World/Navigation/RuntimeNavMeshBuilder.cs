using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 타깃(로컬 플레이어) 주변 일정 영역의 NavMesh를 런타임에 비동기로 (재)빌드한다.
/// 월드가 청크 단위로 스트리밍되므로 맵 전체를 한 번에 굽지 않고, 타깃이 이동하거나 주기가 지나면
/// 그 시점에 로드된 지형 콜라이더로 주변 영역만 다시 굽는다.
///
/// 동물 AI는 호스트(또는 싱글)만 돌리므로 클라이언트에서는 빌드하지 않는다.
/// </summary>
public class RuntimeNavMeshBuilder : MonoBehaviour
{
    #region Inspector
    [Header("수집 대상")]
    [Tooltip("NavMesh를 만들 지형 레이어. 월드 지형은 Ground 레이어를 쓴다")]
    [SerializeField] private LayerMask includeLayers = 1 << 3; // Ground
    [SerializeField] private NavMeshCollectGeometry geometry = NavMeshCollectGeometry.PhysicsColliders;
    [Tooltip("NavMesh Agent Type ID (0 = Humanoid)")]
    [SerializeField] private int agentTypeId = 0;

    [Header("빌드 영역")]
    [Tooltip("타깃 중심으로 굽는 영역 크기(m)")]
    [SerializeField] private Vector3 buildSize = new Vector3(120f, 80f, 120f);
    [Tooltip("재빌드 주기(초). 새로 로드된 청크를 반영한다")]
    [SerializeField] private float rebuildInterval = 5f;
    [Tooltip("마지막 빌드 중심에서 이 거리(m) 이상 벗어나면 주기와 상관없이 재빌드한다")]
    [SerializeField] private float rebuildMoveThreshold = 20f;
    #endregion

    private Transform target;
    private NavMeshData navMeshData;
    private NavMeshDataInstance navMeshInstance;
    private AsyncOperation buildOperation;
    private readonly List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
    private readonly List<NavMeshBuildMarkup> markups = new List<NavMeshBuildMarkup>();

    private Vector3 lastCenter;
    private bool hasBuilt;
    private float timer;
    private float findTargetTimer;

    /// <summary>NavMesh를 구울 중심 타깃을 지정한다. 지정하지 않으면 씬의 Player를 찾아 쓴다.</summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        timer = 0f; // 다음 프레임에 즉시 빌드
    }

    private void OnEnable()
    {
        Main.Loop.OnUpdate -= OnUpdate; // 재활성화 시 중복 구독 방지
        Main.Loop.OnUpdate += OnUpdate;
    }

    // 구독 해제는 OnDisable이 아니라 파괴 시점에만 수행한다.
    private void OnDestroy()
    {
        if (Main.Loop != null)
            Main.Loop.OnUpdate -= OnUpdate;

        if (navMeshInstance.valid)
            navMeshInstance.Remove();
    }

    private void OnUpdate(float deltaTime)
    {
        if (!isActiveAndEnabled) return;
        if (!Animal.IsSimulatedPeer) return;
        if (buildOperation != null && !buildOperation.isDone) return;

        Transform center = ResolveTarget(deltaTime);
        if (center == null) return;

        timer -= deltaTime;
        bool movedFar = hasBuilt &&
                        (center.position - lastCenter).sqrMagnitude > rebuildMoveThreshold * rebuildMoveThreshold;

        if (timer > 0f && !movedFar) return;

        timer = rebuildInterval;
        Rebuild(center.position);
    }

    // 타깃이 없으면 1초마다 Player를 찾아본다.
    private Transform ResolveTarget(float deltaTime)
    {
        if (target != null && target.gameObject.activeInHierarchy) return target;

        findTargetTimer -= deltaTime;
        if (findTargetTimer > 0f) return null;
        findTargetTimer = 1f;

        Player player = FindAnyObjectByType<Player>();
        if (player != null) SetTarget(player.transform);
        return target;
    }

    private void Rebuild(Vector3 center)
    {
        var bounds = new Bounds(center, buildSize);

        sources.Clear();
        NavMeshBuilder.CollectSources(bounds, includeLayers, geometry, 0, markups, sources);

        // 움직이는 액터(동물/플레이어)의 콜라이더가 NavMesh에 구멍을 내지 않도록 제외한다.
        sources.RemoveAll(IsDynamicActor);

        if (navMeshData == null)
        {
            navMeshData = new NavMeshData(agentTypeId);
            navMeshInstance = NavMesh.AddNavMeshData(navMeshData);
        }

        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(agentTypeId);
        buildOperation = NavMeshBuilder.UpdateNavMeshDataAsync(navMeshData, settings, sources, bounds);

        lastCenter = center;
        hasBuilt = true;
    }

    private static bool IsDynamicActor(NavMeshBuildSource source)
    {
        if (source.component == null) return false;
        GameObject go = source.component.gameObject;
        return go.GetComponentInParent<Animal>() != null
            || go.GetComponentInParent<Player>() != null
            || go.GetComponentInParent<Monster>() != null;
    }

    private void OnDrawGizmosSelected()
    {
        if (!hasBuilt) return;
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireCube(lastCenter, buildSize);
    }
}
