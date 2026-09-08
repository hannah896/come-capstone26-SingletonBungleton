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
        Main.Input.AddInput<UIMapInputHandler>();       // UI 핸들러 추가함.

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
        {
            Main.Input.RemoveInput<InputActions_PlayerInputHandler>();
            Main.Input.RemoveInput<UIMapInputHandler>();                // UI 핸들러 
        }
    }

    private void OnLoopUpdate(float deltaTime)
    {
        if (motor == null || machine == null) return;

        // 원격 플레이어(입력 권한 없음)는 로컬 시뮬레이션을 돌리지 않는다.
        // 위치/회전은 NetworkPlayerSync가 복제하므로, 여기서 motor/상태머신을 돌리면 충돌한다.
        // (단일 플레이어는 NetworkObject가 없어 IsLocalPlayerObject()==true → 그대로 동작)
        if (!IsLocalPlayerObject())
            return;

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
        // 원격 플레이어는 로컬 시뮬레이션을 돌리지 않는다(NetworkPlayerSync가 위치 복제).
        if (!IsLocalPlayerObject())
            return;

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

    #region Revive

    // 부활 지점의 지면을 찾을 때 사용하는 레이캐스트 파라미터
    private const string GroundLayerName = "Ground";
    private const float RespawnRayHeight = 50f;      // 스폰 좌표 위쪽 이 높이에서 아래로 쏜다
    private const float RespawnGroundClearance = 0.5f; // 지면에서 띄울 높이 (콜라이더 겹침 방지)

    /// <summary>
    /// 사망 상태에서 부활한다.
    /// 생존 지표를 최대치로 회복하고 리스폰 지점으로 이동시킨 뒤 Locomotion 상태로 되돌린다.
    /// 게임 진행 상태(GameProcessing) 복구와 사망 팝업 닫기는 PlayerDeadState.OnExit이 담당한다.
    /// </summary>
    /// <returns>부활했으면 true. 이미 사망 상태가 아니면 false.</returns>
    public bool Revive()
    {
        if (stat == null || machine == null) return false;
        if (machine.CurrentState is not PlayerDeadState) return false;

        // 허기가 0인 채로 부활하면 즉시 다시 체력이 깎이므로 함께 회복한다.
        stat.RestoreHp(stat.MaxHp);
        stat.RestoreHunger(stat.MaxHunger);
        stat.RestoreEgo(stat.MaxEgo);

        // 리스폰 지점으로 이동. 지점을 못 구하면(테스트 씬 등) 제자리에서 부활한다.
        if (TryGetRespawnPosition(out Vector3 respawnPosition))
            motor?.Teleport(respawnPosition);
        else
            motor?.ResetVelocity();

        // 사망 직전 입력이 남아 부활 첫 프레임에 이동·공격이 나가는 것을 막는다.
        inputData?.SuppressAllInputs();

        machine.ChangeState(new PlayerLocomotionState(machine));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Player] Revived at {transform.position}");
#endif
        return true;
    }

    /// <summary>
    /// 월드 생성 시 확정된 StartRegion 스폰 좌표를 기준으로 부활 위치를 구한다.
    /// 스폰 좌표는 낙하 여유를 두고 공중에 잡혀 있으므로, 지면을 찾아 그 위에 세운다.
    /// </summary>
    private bool TryGetRespawnPosition(out Vector3 position)
    {
        position = transform.position;

        WorldGenManager world = WorldGenManager.Instance;
        if (world == null || !world.HasPlayerSpawnPosition)
            return false;

        Vector3 spawn = world.PlayerSpawnPosition;
        Vector3 rayOrigin = new Vector3(spawn.x, spawn.y + RespawnRayHeight, spawn.z);
        int groundMask = LayerMask.GetMask(GroundLayerName);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                            RespawnRayHeight * 4f, groundMask, QueryTriggerInteraction.Ignore))
        {
            position = hit.point + Vector3.up * RespawnGroundClearance;
            return true;
        }

        // 지면을 찾지 못하면 최초 스폰과 동일하게 공중에서 낙하시킨다.
        position = spawn;
        return true;
    }

    #endregion

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

    private NetworkObject _networkObject;
    private bool _networkObjectCached;

    // Host 모드: 자기 캐릭터(InputAuthority 보유)만 로컬 시뮬레이션을 돌린다.
    // 원격 캐릭터는 NetworkPlayerSync가 호스트 확정 값으로 transform을 복제한다.
    private bool IsLocalPlayerObject()
    {
        if (!_networkObjectCached)
        {
            _networkObject = GetComponent<NetworkObject>();
            _networkObjectCached = true;
        }

        // 프리팹에 NetworkObject가 붙어 있어도 Runner.Spawn을 거치지 않으면 러너에 등록되지 않아 IsValid가 false다.
        // (싱글플레이는 WorldGenManager가 프리팹을 그대로 Instantiate한다)
        // 이때는 네트워크에 참여하지 않는 로컬 캐릭터이므로 로컬로 취급한다.
        if (_networkObject == null || !_networkObject.IsValid)
            return true;

        return _networkObject.HasInputAuthority;
    }
}
