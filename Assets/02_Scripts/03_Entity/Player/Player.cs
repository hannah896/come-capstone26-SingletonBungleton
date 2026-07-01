using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;

public class Player : MonoBehaviour, IDamageable
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
    public PlayerFirstPersonCameraController FPCameraController => fpCameraController;
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
        if (playerInventory == null)
            playerInventory = GetComponent<PlayerInventory>();
    }

    private async void Awake()
    {
        var token = this.GetCancellationTokenOnDestroy();

        // machine과 inputData는 동기적으로 먼저 생성 (Start()가 await 복귀 전에 실행될 수 있으므로)
        inputData = new PlayerInputData();
        machine = new PlayerRootStateMachine(this, animator);

        // 1인칭에서 몸 렌더러를 숨겨도(컬링) 애니 상태가 멈추지 않도록 항상 평가하게 강제.
        // (컬링되면 액션 애니가 진행/종료되지 않아 PlayerActionState가 timeout으로만 꺼지는 문제 방지)
        if (animator != null)
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (playerInventory == null)
            playerInventory = Extensions.GetOrAddComponent<PlayerInventory>(gameObject);

        var statData = await Extensions.LoadAssetAsync<PlayerStatData>("PlayerStatData")
            .AttachExternalCancellation(token);
        stat = new(statData);
        stat.OnDamaged += HandleDamaged;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[Player] Initialized - Motor: " + (motor != null) + ", Animator: " + (animator != null));
#endif
    }

    private async void Start()
    {
        var token = this.GetCancellationTokenOnDestroy();

        bool isLocalPlayer = IsLocalPlayerObject();
        fpCameraController?.SetLocalView(isLocalPlayer);

        if (!isLocalPlayer)
        {
            // 원격 플레이어도 몸체 렌더러 초기화 필요 (그림자/가시성 설정 적용을 위해)
            fpCameraController?.InitBodyRenderers(transform);
            var remoteLocomotionState = new PlayerLocomotionState(machine);
            machine.Init(remoteLocomotionState);
            return;
        }

        // InputManager 초기화 완료 대기
        while (!Main.Input.IsInitialized)
            await UniTask.Delay(10, cancellationToken: token);

        // InputHandler 바인딩 및 활성화
        var handler = Main.Input.GetOrCreateAction<InputActions_PlayerInputHandler>();
        handler.Bind(this, inputData);
        Main.Input.AddInput<InputActions_PlayerInputHandler>();

        // 1인칭 카메라 컨트롤러 바인딩 및 카메라 동적 생성
        fpCameraController?.Bind(inputData, transform);
        fpCameraController?.BindInventory(playerInventory);
        if (fpCameraController != null)
            await fpCameraController.InitCameraAsync();

        playerInventory?.Bind(this, inputData);
        if (playerInventory != null)
        {
            CraftingManager.Instance?.Bind(playerInventory);
            var craftingUI = Object.FindObjectOfType<CraftingUI>(true);
            craftingUI?.Bind(playerInventory);
        }

        var hud = await Extensions.ShowHud<UI_Hud_Player>();
        hud.Player = this;

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
        if (IsLocalPlayerObject() && Main.Input != null)
            Main.Input.RemoveInput<InputActions_PlayerInputHandler>();
    }

    private void OnLoopUpdate(float deltaTime)
    {
        if (motor == null || machine == null) return;

        // 액션 연출 중에는 카메라 외 모든 입력 차단
        if (IsLocalPlayerObject() && machine.CurrentState is PlayerActionState)
            inputData?.SuppressAllInputs();

        machine.OnUpdate(deltaTime);
        motor.Tick(deltaTime);  // 중력 시뮬레이션, 착지 감지, 이동 수행
        if (IsLocalPlayerObject())
        {
            playerInventory?.Tick();
            inputData?.ConsumeEventInputs();
        }

        // 허기·Ego는 일시정지(Stopping) 외에는 항상 소모 (GameProcessing.None인 테스트 씬 포함)
        if (stat != null && GameScene.GameProcessing != GameProcessing.Stopping)
        {
            stat.UpdateHunger(deltaTime);
            stat.UpdateEgo(deltaTime);

            // 자연사 감지 (허기·Ego로 HP가 0이 된 경우)
            if (stat.IsDead
                && machine.CurrentState is not PlayerDeadState
                && machine.CurrentState is not PlayerHurtState)
            {
                machine.ChangeState(new PlayerDeadState(machine, wasHit: false));
            }
        }
    }

    private void OnLoopGameUpdate(float deltaTime)
    {
        machine.OnGameUpdate(deltaTime);
    }

    /// <summary>
    /// 액션 애니메이션의 Stop 프레임 Animation Event(PlayerAnimEventRelay 경유)를
    /// 현재 액션 상태로 전달한다.
    /// </summary>
    public void OnActionAnimationEvent()
    {
        if (machine?.CurrentState is PlayerActionState action)
            action.OnActionEvent();
    }

    #region IDamageable
    // 몬스터 등 외부 공격 수신구. 자원 채집과 동일한 DamageContext 계약을 사용한다.
    public bool CanDamage(DamageContext damageCtx)
        => stat != null && !stat.IsDead && damageCtx.Amount > 0;

    public void ApplyDamage(DamageContext damageCtx)
    {
        if (!CanDamage(damageCtx)) return;
        // TakeDamage → OnDamaged → HandleDamaged 로 PlayerHurtState 전환까지 이어진다.
        stat.TakeDamage(damageCtx.Amount);
    }
    #endregion

    private void HandleDamaged(float damage)
    {
        if (machine == null) return;
        if (machine.CurrentState is PlayerHurtState || machine.CurrentState is PlayerDeadState) return;
        machine.ChangeState(new PlayerHurtState(machine));
    }

    private bool IsLocalPlayerObject()
    {
        NetworkObject networkObject = GetComponent<NetworkObject>();
        return networkObject == null || networkObject.HasInputAuthority;
    }
}
