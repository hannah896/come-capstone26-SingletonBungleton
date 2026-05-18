using Fusion;
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

    #region Inventory
    [SerializeField] private PlayerInventory playerInventory;
    #endregion

    #region Properties
    public Animator Animator => animator;
    public PlayerMotor Motor => motor;
    public PlayerAnimData AnimData => machine.AnimData;
    public PlayerInputData InputData => inputData;
    public PlayerStatus Stat => stat;
    public PlayerInventory Inventory => playerInventory;
    public string CurrentStateName => machine?.CurrentStateName ?? "None";
    public string CurrentSubStateName => machine?.CurrentSubStateName ?? "None";
    public bool IsGrounded => motor != null && motor.IsGrounded;
    public bool IsLocalPlayer => IsLocalPlayerObject();
    #endregion

    private void OnValidate()
    {
        if (animator == null) 
            animator = GetComponentInChildren<Animator>();
        if (motor == null)
            motor = GetComponent<PlayerMotor>();
        if (playerTracer == null)  
            playerTracer = GetComponent<PlayerTracer>();
        if (playerInventory == null)
            playerInventory = GetComponent<PlayerInventory>();
    }

    private async void Awake()
    {
        // machine과 inputData는 동기적으로 먼저 생성 (Start()가 await 복귀 전에 실행될 수 있으므로)
        inputData = new PlayerInputData();
        machine = new PlayerRootStateMachine(this, animator);
        if (playerInventory == null)
            playerInventory = Extensions.GetOrAddComponent<PlayerInventory>(gameObject);

        var _statData = await Extensions.LoadAssetAsync<PlayerStatData>("PlayerStatData");
        stat = new(_statData);
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[Player] Initialized - Motor: " + (motor != null) + ", Animator: " + (animator != null));
#endif
    }
    private async void Start()
    {
        bool isLocalPlayer = IsLocalPlayerObject();
        fpCameraController?.SetLocalView(isLocalPlayer);

        if (!isLocalPlayer)
        {
            var remoteLocomotionState = new PlayerLocomotionState(machine);
            machine.Init(remoteLocomotionState);
            return;
        }

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
        fpCameraController?.BindInventory(playerInventory);
        if (fpCameraController != null)
            await fpCameraController.InitCameraAsync();

        // Trace 레이캐스터 바인딩
        playerTracer?.Bind(inputData);
        playerInventory?.Bind(this, inputData);

        // 초기 상태: Locomotion
        await InventoryDisplayUI.ShowFor(playerInventory);
        UI_PlayerStatus statusUI = await Extensions.ShowHud<UI_PlayerStatus>("UI_Hud_Status");
        if (statusUI != null)
        {
            statusUI.gameObject.SetActive(true);
            statusUI.Set(this);
        }

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
        if (IsLocalPlayerObject() && Main.Input != null)
            Main.Input.RemoveInput<InputActions_PlayerInputHandler>();
    }

    private void OnLoopUpdate(float deltaTime)
    {
        if (motor == null || machine == null) return;

        machine.OnUpdate(deltaTime);
        motor.Tick(deltaTime);  // 중력 시뮬레이션, 착지 감지, 이동 수행
        if (IsLocalPlayerObject())
        {
            playerInventory?.Tick();
            inputData?.ConsumeEventInputs();
        }
    }

    private void OnLoopGameUpdate(float deltaTime)
    {
        machine.OnGameUpdate(deltaTime);

        if (stat == null) return;
        stat.UpdateHunger(deltaTime);
        stat.UpdateEgo(deltaTime);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void OnGUI()
    {
        if (motor == null) return;
        
        string debugInfo = 
            $"=== Player Debug ===\n" +
            $"RootState: {CurrentStateName}\n" +
            $"SubState: {CurrentSubStateName}\n" +
            $"IsGrounded: {motor.IsGrounded}\n" +
            $"WasGroundedRecently: {motor.WasGroundedRecently}\n" +
            $"VerticalVelocity: {motor.VerticalVelocity:F2}\n" +
            $"HorizontalVelocity: {motor.Velocity.magnitude - Mathf.Abs(motor.VerticalVelocity):F2}\n" +
            $"Position: {transform.position:F2}\n" +
            $"IsOnSlope: {motor.IsOnSlope}\n" +
            $"SlopeAngle: {motor.SlopeAngle:F1}°";
        
        GUI.Label(new Rect(10, 10, 300, 220), debugInfo);
        
        // CharacterController 정보
        var cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            GUI.Label(new Rect(10, 230, 300, 160),
                $"=== CharacterController ===\n" +
                $"Radius: {cc.radius:F2}\n" +
                $"Height: {cc.height:F2}\n" +
                $"Center: {cc.center:F2}\n" +
                $"SkinWidth: {cc.skinWidth:F2}\n" +
                $"Velocity: {cc.velocity.magnitude:F2}\n" +
                $"MoveInput: {inputData?.MoveInput ?? Vector2.zero}\n" +
                $"JumpPressed: {inputData?.JumpPressed ?? false}\n" +
                $"AttackPressed: {inputData?.AttackPressed ?? false}");
        }
    }
#endif

    private bool IsLocalPlayerObject()
    {
        NetworkObject networkObject = GetComponent<NetworkObject>();
        return networkObject == null || networkObject.HasInputAuthority;
    }
}
