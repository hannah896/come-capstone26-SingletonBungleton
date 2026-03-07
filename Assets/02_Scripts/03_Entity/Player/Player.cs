using UnityEngine;

public class Player : MonoBehaviour
{
    private PlayerRootStateMachine machine;
    private Animator animator;
    private Rigidbody rb;

    public Animator Animator => animator;
    public PlayerAnimData AnimData => machine.AnimData;

    private void OnValidate()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (rb == null)
            rb = GetComponent<Rigidbody>();
    }

    private void Awake()
    {
        machine = new(this, animator);

    }

    private void Start()
    {
        Init();
    }


    /// <summary>
    /// 처음 생성될때 Player의 상태 머신을 초기화하는 메서드입니다.
    /// </summary>
    private void Init()
    {
        //PlayerStateMachine 초기화
        machine = new PlayerRootStateMachine(this, animator);
        machine.Init(AnimData.IdleState);
    }

    private void OnEnable()
    { 
        // LoopManager의 이벤트에 Player의 업데이트 메서드 등록
        Main.Loop.OnGameUpdate += OnLoopGameUpdate;
        Main.Loop.OnUpdate += OnLoopUpdate;
    }

    private void OnDisable()
    {
        // LoopManager의 이벤트에서 등록 해제
        Main.Loop.OnGameUpdate -= OnLoopGameUpdate;
        Main.Loop.OnUpdate -= OnLoopUpdate;
    }

    private void OnLoopUpdate(float deltaTime)
    {
        // StateMachine의 Update 호출 (상태 로직)
        machine.CurrentState?.Update();
    }

    private void OnLoopGameUpdate(float deltaTime)
    {
        // StateMachine의 FixedUpdate 호출 (물리 및 게임 속도 적용)
        machine.CurrentState?.FixedUpdate();
    }
}