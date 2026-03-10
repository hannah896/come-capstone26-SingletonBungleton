using UnityEngine;

public class Player : MonoBehaviour
{
    private PlayerRootStateMachine machine;
    private Animator animator;
    private Rigidbody rb;
    private PlayerStat stat;

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
        machine = new PlayerRootStateMachine(this, animator);
    }

    private void Start()
    {
        // 상태 인스턴스 생성 및 초기 상태 설정
        var idleState = new PlayerIdleState(machine);
        machine.Init(idleState);
    }

    private void OnEnable()
    {
        // LoopManager에 업데이트 등록 (Player에서만 관리)
        Main.Loop.OnGameUpdate += OnLoopGameUpdate;
        Main.Loop.OnUpdate += OnLoopUpdate;
    }

    private void OnDisable()
    {
        Main.Loop.OnGameUpdate -= OnLoopGameUpdate;
        Main.Loop.OnUpdate -= OnLoopUpdate;
    }

    private void OnLoopUpdate(float deltaTime)
    {
        machine.OnUpdate(deltaTime);
    }

    private void OnLoopGameUpdate(float deltaTime)
    {
        machine.OnGameUpdate(deltaTime);
    }
}
