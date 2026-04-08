using UnityEngine;

/// <summary>
/// 3인칭 카메라 컨트롤러
/// Player와 분리되어 독립적으로 작동합니다.
/// 마우스 입력으로 카메라 회전을 제어합니다.
/// </summary>
public class PlayerCameraController : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Transform followTarget;

    [Header("회전 설정")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 70f;

    private float yaw;
    private float pitch;
    private PlayerInputData inputData;

    /// <summary>
    /// 외부에서 InputData를 바인딩한다 (로컬 플레이어만)
    /// </summary>
    public void Bind(PlayerInputData data, Transform target)
    {
        inputData = data;
        followTarget = target;
    }

    private void OnEnable()
    {
        Main.Loop.OnLateUpdate += OnLateUpdateLoop;
    }

    private void OnDisable()
    {
        Main.Loop.OnLateUpdate -= OnLateUpdateLoop;
    }

    private void OnLateUpdateLoop(float deltaTime)
    {
        if (inputData == null || followTarget == null) return;

        // 입력으로 Yaw/Pitch 갱신
        Vector2 look = inputData.LookInput;
        yaw += look.x * mouseSensitivity;
        pitch -= look.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // 카메라 피벗 위치 및 회전 적용
        transform.position = followTarget.position;
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}

