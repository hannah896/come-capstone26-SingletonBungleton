using UnityEngine;

public class Player : MonoBehaviour
{
    #region State
    [SerializeField] private PlayerRootStateMachine machine;
    #endregion

    #region Animation
    [SerializeField] private Animator animator;
    #endregion

    #region Move
    [SerializeField] private PlayerMotor motor;
    #endregion

    #region Status& Data
    [SerializeField] private PlayerStatus stat;
    #endregion

    #region Input
    [SerializeField] private PlayerInputData inputData;
    #endregion

    #region Camera
    [SerializeField] private PlayerFirstPersonCameraController fpCameraController;
    #endregion

    #region Trace
    [SerializeField] private PlayerTracer playerTracer;
    #endregion

    #region Properties
    public Animator Animator => animator;
    public PlayerMotor Motor => motor;
    public PlayerAnimData AnimData => machine.AnimData;
    public PlayerInputData InputData => inputData;
    public PlayerStatus Stat => stat;
    public bool IsGrounded => motor != null && motor.IsGrounded;
    #endregion

    private void OnValidate()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (motor == null)
            motor = GetComponent<PlayerMotor>();
        if (playerTracer == null)
            playerTracer = GetComponent<PlayerTracer>();
    }

    private async void Awake()
    {
        var _statData = await Extensions.LoadAssetAsync<PlayerStatData>("PlayerStatData");
        stat = new(_statData);

        inputData = new PlayerInputData();
        machine = new PlayerRootStateMachine(this, animator);
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[Player] Initialized - Motor: " + (motor != null) + ", Animator: " + (animator != null));
#endif
    }
    private async void Start()
    {
        // InputManager 초기화 완료 대기
        while (!Main.Input.IsInitialized)
        {
            await Cysharp.Threading.Tasks.UniTask.Delay(10);
        }

        // InputHandler 바인딩 및 활성화
        var handler = Main.Input.GetOrCreateAction<InputActions_PlayerInputHandler>();
        handler.Bind(this, inputData);
        Main.Input.AddInput<InputActions_PlayerInputHandler>();

        // 1인칭 카메라 컨트롤러 바인딩 및 카메라 동적 생성
        fpCameraController?.Bind(inputData, transform);
        if (fpCameraController != null)
            await fpCameraController.InitCameraAsync();

        // Trace 레이캐스터 바인딩
        playerTracer?.Bind(inputData);

        // 초기 상태: Locomotion
        var locomotionState = new PlayerLocomotionState(machine);
        machine.Init(locomotionState);
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[Player] Started - Locomotion State initialized");
#endif
    }

    private void OnEnable()
    {
        Main.Loop.OnGameUpdate += OnLoopGameUpdate;
        Main.Loop.OnUpdate += OnLoopUpdate;
    }

    private void OnDisable()
    {
        Main.Loop.OnGameUpdate -= OnLoopGameUpdate;
        Main.Loop.OnUpdate -= OnLoopUpdate;
        Main.Input.RemoveInput<InputActions_PlayerInputHandler>();
    }

    private void OnLoopUpdate(float deltaTime)
    {
        if (motor == null) return;
        
        machine.OnUpdate(deltaTime);
        motor.Tick(deltaTime);  // 중력 시뮬레이션, 착지 감지, 이동 수행
        inputData.ConsumeEventInputs();
    }

    private void OnLoopGameUpdate(float deltaTime)
    {
        machine.OnGameUpdate(deltaTime);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void OnGUI()
    {
        if (motor == null) return;
        
        string debugInfo = 
            $"=== Player Debug ===\n" +
            $"IsGrounded: {motor.IsGrounded}\n" +
            $"WasGroundedRecently: {motor.WasGroundedRecently}\n" +
            $"VerticalVelocity: {motor.VerticalVelocity:F2}\n" +
            $"HorizontalVelocity: {motor.Velocity.magnitude - Mathf.Abs(motor.VerticalVelocity):F2}\n" +
            $"Position: {transform.position:F2}\n" +
            $"IsOnSlope: {motor.IsOnSlope}\n" +
            $"SlopeAngle: {motor.SlopeAngle:F1}°";
        
        GUI.Label(new Rect(10, 10, 250, 180), debugInfo);
        
        // CharacterController 정보
        var cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            GUI.Label(new Rect(10, 190, 250, 120),
                $"=== CharacterController ===\n" +
                $"Radius: {cc.radius:F2}\n" +
                $"Height: {cc.height:F2}\n" +
                $"Center: {cc.center:F2}\n" +
                $"SkinWidth: {cc.skinWidth:F2}\n" +
                $"Velocity: {cc.velocity.magnitude:F2}");
        }
    }
#endif
}
