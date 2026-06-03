using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// 로비 배경 연출용 — 맵 곳곳을 누비는 Spline 경로를 따라 메인 카메라가 이동한다.
/// 런타임에 소환된 맵 bounds를 기준으로 맵 안팎을 구불구불 도는 닫힌 Spline을 자동 생성한다.
/// (LobbyScene이 환경 오브젝트 소환 후 Setup 호출. 로비에선 Cinemachine 미사용 — GameScene 전용)
/// </summary>
public class LobbyBackgroundOrbit : MonoBehaviour
{
    [Header("Path")]
    [SerializeField] private float tourDuration = 60f;       // 경로 한 바퀴 도는 시간(초) — 클수록 천천히
    [SerializeField] private int waypointCount = 8;          // 경로 웨이포인트 수
    [SerializeField] private int wanderSeed = 12345;         // 경로 형태 시드 (같으면 동일 경로)
    [SerializeField] private float innerRadiusScale = 0.25f; // 맵 중심에 가장 가까운 지점 비율
    [SerializeField] private float outerRadiusScale = 0.9f;  // 맵 가장자리에 가장 가까운 지점 비율
    [SerializeField] private float heightScale = 0.25f;      // 맵 평면 반경 대비 기본 카메라 높이
    [SerializeField] private float heightVariation = 0.2f;   // 지점별 높이 변주 폭

    [Header("Look")]
    [SerializeField] private float lookAhead = 20f;          // 진행 방향 전방 주시 거리
    [SerializeField] private float lookDownStrength = 8f;    // 아래로 기울여 맵을 보이게 하는 정도

    private Camera _camera;
    private Behaviour _brain;   // CinemachineBrain (있으면 도는 동안 잠시 비활성)
    private SplineContainer _splineContainer;
    private float _t;

    /// <summary>맵 bounds를 받아 맵 곳곳을 누비는 Spline을 만들고 메인 카메라 순회를 시작한다.</summary>
    public void Setup(Bounds mapBounds)
    {
        Vector3 center = mapBounds.center;
        float planarExtent = Mathf.Max(mapBounds.extents.x, mapBounds.extents.z, 5f);
        float baseHeight = center.y + planarExtent * heightScale;

        BuildWanderSpline(center, planarExtent, baseHeight);
        AcquireMainCamera();

        Main.Loop.OnUpdate += OnUpdate;
    }

    // 맵 안팎을 구불구불 누비는 닫힌 Spline 생성
    private void BuildWanderSpline(Vector3 center, float planarExtent, float baseHeight)
    {
        _splineContainer = gameObject.GetOrAddComponent<SplineContainer>();

        var spline = new Spline();
        var rng = new System.Random(wanderSeed);
        int count = Mathf.Max(4, waypointCount);

        for (int i = 0; i < count; i++)
        {
            // 각도는 균등하게 돌되 반경/높이를 지점마다 변주 → 맵 곳곳을 누비는 경로
            float ang = (i / (float)count) * Mathf.PI * 2f;
            float r = planarExtent * Mathf.Lerp(innerRadiusScale, outerRadiusScale, (float)rng.NextDouble());
            float h = baseHeight + planarExtent * heightVariation * ((float)rng.NextDouble() - 0.5f);

            float3 p = new float3(
                center.x + Mathf.Cos(ang) * r,
                h,
                center.z + Mathf.Sin(ang) * r);
            spline.Add(new BezierKnot(p), TangentMode.AutoSmooth);
        }

        spline.Closed = true;
        _splineContainer.Spline = spline;
    }

    // 로비 배경은 메인 카메라가 직접 경로를 돈다 (로비에선 Cinemachine 미사용)
    private void AcquireMainCamera()
    {
        _camera = Camera.main;
        if (_camera == null)
        {
            Debug.LogWarning("[LobbyBackgroundOrbit] Camera.main을 찾지 못해 배경 순회를 시작할 수 없습니다.");
            return;
        }

        // 혹시 CinemachineBrain이 카메라 transform을 매 프레임 덮어쓰면 도는 동안 잠시 비활성화
        _brain = _camera.GetComponent("CinemachineBrain") as Behaviour;
        if (_brain != null) _brain.enabled = false;
    }

    private void OnUpdate(float deltaTime)
    {
        if (_camera == null || _splineContainer == null) return;

        _t = (_t + deltaTime / Mathf.Max(0.01f, tourDuration)) % 1f;

        Vector3 pos = _splineContainer.EvaluatePosition(_t);
        Vector3 tangent = _splineContainer.EvaluateTangent(_t);
        _camera.transform.position = pos;

        // 진행 방향 전방을 보되 살짝 아래로 기울여 맵이 화면에 들어오게
        if (tangent.sqrMagnitude > 0.0001f)
        {
            Vector3 look = pos + tangent.normalized * lookAhead - Vector3.up * lookDownStrength;
            _camera.transform.LookAt(look);
        }
    }

    private void OnDestroy()
    {
        Main.Loop.OnUpdate -= OnUpdate;

        // 로비를 떠날 때 Cinemachine 복구
        if (_brain != null) _brain.enabled = true;
    }
}
