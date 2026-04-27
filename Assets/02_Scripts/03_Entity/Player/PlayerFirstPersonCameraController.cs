using Cysharp.Threading.Tasks;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 1인칭 시점 카메라 컨트롤러 (Cinemachine 3.x 연동)
/// - 마우스 X → 플레이어 몸체 Yaw 회전
/// - 마우스 Y → 눈 피벗(EyePivot) Pitch 회전
/// - CinemachineCamera는 EyePivot을 Follow + 회전 상속하여 1인칭 시점 구현
/// </summary>
public class PlayerFirstPersonCameraController : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Transform eyePivot;         // 눈 위치 피벗 (플레이어 자식)
    [SerializeField] private CinemachineCamera fpCamera; // 1인칭 시네머신 카메라

    [Header("회전 설정")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    private float yaw;
    private float pitch;
    private PlayerInputData inputData;
    private Transform playerBody;

    /// <summary>
    /// Player.Start()에서 호출 — 입력 데이터와 몸체 Transform 바인딩
    /// </summary>
    public void Bind(PlayerInputData data, Transform body)
    {
        inputData = data;
        playerBody = body;
        yaw = body.eulerAngles.y;
        pitch = 0f;
    }

    /// <summary>
    /// FP_CinemachineCamera 어드레서블을 로드하여 카메라를 동적 생성한다.
    /// fpCamera가 이미 주입된 경우(SetCamera) 스킵.
    /// </summary>
    public async UniTask InitCameraAsync()
    {
        if (fpCamera != null) return;

        var cam = await Extensions.Instantiate<CinemachineCamera>("FP_CinemachineCamera");
        fpCamera = cam;
        fpCamera.Follow = eyePivot;
    }

    /// <summary>
    /// 외부에서 이미 생성된 카메라를 직접 주입할 때 사용
    /// </summary>
    public void SetCamera(CinemachineCamera cam)
    {
        fpCamera = cam;
        if (fpCamera != null) fpCamera.Follow = eyePivot;
    }

    public Transform EyePivot => eyePivot;
    public CinemachineCamera FPCamera => fpCamera;

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
        if (inputData == null || playerBody == null || eyePivot == null) return;

        Vector2 look = inputData.LookInput;

        // Yaw: 플레이어 몸체를 좌우 회전 (1인칭에서 몸이 카메라 방향과 일치)
        yaw += look.x * mouseSensitivity;
        playerBody.rotation = Quaternion.Euler(0f, yaw, 0f);

        // Pitch: 눈 피벗만 상하 회전 (몸은 기울지 않음)
        pitch -= look.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        eyePivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
