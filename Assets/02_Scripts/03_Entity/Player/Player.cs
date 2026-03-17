using UnityEngine;

public class Player : MonoBehaviour
{
    private PlayerRootStateMachine machine;
    private Animator animator;
    private Rigidbody rb;
    private PlayerStatus stat;
    private PlayerInputData inputData;

    #region Ground Check
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask groundLayer;
    private bool isGrounded;
    #endregion

    public Animator Animator => animator;
    public Rigidbody Rb => rb;
    public PlayerAnimData AnimData => machine.AnimData;
    public PlayerInputData InputData => inputData;
    public PlayerStatus Stat => stat;
    public bool IsGrounded => isGrounded;

    private void OnValidate()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (rb == null)
            rb = GetComponent<Rigidbody>();
    }

    private void Awake()
    {
        inputData = new PlayerInputData();
        machine = new PlayerRootStateMachine(this, animator);
    }

    private void Start()
    {
        // InputHandler 바인딩 및 활성화
        var handler = Main.Input.GetOrCreateAction<InputActions_PlayerInputHandler>();
        handler.Bind(inputData);
        Main.Input.AddInput<InputActions_PlayerInputHandler>();

        // 초기 상태: Ground
        var groundState = new PlayerGroundState(machine);
        machine.Init(groundState);
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
        UpdateGroundCheck();
        machine.OnUpdate(deltaTime);
        inputData.ConsumeEventInputs();
    }

    private void OnLoopGameUpdate(float deltaTime)
    {
        machine.OnGameUpdate(deltaTime);
    }

    /// <summary>
    /// Raycast를 이용한 착지 감지
    /// </summary>
    private void UpdateGroundCheck()
    {
        var capsule = GetComponent<CapsuleCollider>();
        Vector3 rayOrigin = transform.position + Vector3.up * (capsule.bounds.extents.y - groundCheckDistance);
        isGrounded = Physics.Raycast(rayOrigin, Vector3.down, groundCheckDistance, groundLayer);
        
        // 디버그 표시
        if (isGrounded)
            Debug.Log("Grounded");
    }
}
