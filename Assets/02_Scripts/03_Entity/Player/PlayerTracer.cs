using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 마우스 좌클릭으로 자원 레이어 오브젝트를 감지하고 자동이동 목적지를 계산합니다.
/// - 설정된 레이어에 속한 오브젝트만 감지
/// - 클릭 지점 표면에서 stopDistance(1f) 만큼 바깥쪽 위치를 목적지로 기록
/// </summary>
public class PlayerTracer : MonoBehaviour
{
    [Header("레이캐스트 설정")]
    [SerializeField] private LayerMask resourceLayer;
    [SerializeField] private float stopDistance = 1f;

    private PlayerInputData inputData;

    /// <summary>
    /// Player.Start()에서 InputData 바인딩
    /// </summary>
    public void Bind(PlayerInputData data)
    {
        inputData = data;
    }

    private void OnEnable()
    {
        Main.Loop.OnUpdate += OnUpdateLoop;
    }

    private void OnDisable()
    {
        Main.Loop.OnUpdate -= OnUpdateLoop;
    }

    private void OnUpdateLoop(float deltaTime)
    {
        if (inputData == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, resourceLayer)) return;

        inputData.TraceDestination = CalcDestination(hit);
        inputData.TracePressed = true;
    }

    /// <summary>
    /// 히트 정보로부터 플레이어가 멈출 목적지를 계산합니다.
    /// 히트 노멀의 수평 성분을 우선 사용하고,
    /// 수평 노멀이 없는 면(위/아래 방향)이면 플레이어→타겟 방향으로 대체합니다.
    /// </summary>
    private Vector3 CalcDestination(RaycastHit hit)
    {
        Vector3 horizontalNormal = new Vector3(hit.normal.x, 0f, hit.normal.z);

        Vector3 awayDir;
        if (horizontalNormal.sqrMagnitude > 0.1f)
        {
            // 측면 히트: 노멀 방향으로 물러남
            awayDir = horizontalNormal.normalized;
        }
        else
        {
            // 수평 노멀 없음(위/아래 면): 플레이어 → 히트 포인트 방향 사용
            Vector3 toPlayer = transform.position - hit.point;
            toPlayer.y = 0f;
            awayDir = toPlayer.sqrMagnitude > 0.01f
                ? toPlayer.normalized
                : Vector3.ProjectOnPlane(-Camera.main.transform.forward, Vector3.up).normalized;
        }

        // Y는 플레이어 발 높이 유지 (CharacterController가 지면 처리)
        Vector3 dest = hit.point + awayDir * stopDistance;
        dest.y = transform.position.y;
        return dest;
    }
}
