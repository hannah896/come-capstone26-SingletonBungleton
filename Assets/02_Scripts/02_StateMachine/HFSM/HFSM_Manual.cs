#if false
// ============================================================================
//  HFSM (Hierarchical Finite State Machine) 사용 매뉴얼
//
//  이 파일은 컴파일되지 않습니다 (#if false).
//  HFSM 프레임워크를 새 엔티티에 적용할 때 참고용 매뉴얼입니다.
// ============================================================================

// ────────────────────────────────────────────────────────────────────────────
//  1. 클래스 계층 구조
// ────────────────────────────────────────────────────────────────────────────
//
//  [FSM 레이어 - 수정 금지]
//  StateBase                                    추상 클래스, 모든 상태의 최상위
//  StateMachine<T>                              상태 전환 및 업데이트 관리
//
//  [HFSM 레이어 - 제네릭]
//  RootStateBase<TEntity>  : StateBase          루트 상태 (SubStateMachine 소유)
//  SubStateBase<TEntity>   : StateBase          하위 상태
//  SubStateMachine<TEntity>: StateMachine<...>  하위 상태 머신
//  RootStateMachine<TEntity, TRoot> : StateMachine<TRoot>  루트 상태 머신
//
//  [엔티티별 구체화 - 예: Player]
//  PlayerRootStateMachine  : RootStateMachine<Player, PlayerRootStateBase>
//  PlayerRootStateBase     : RootStateBase<Player>
//  PlayerSubStateBase      : SubStateBase<Player>
//  PlayerIdleState         : PlayerRootStateBase   ← 실제 상태 구현
//
//
//  구조 다이어그램:
//
//  Player (MonoBehaviour)
//    └─ PlayerRootStateMachine
//         ├─ IdleState (PlayerRootStateBase)
//         │    └─ SubStateMachine<Player>
//         │         ├─ SubStateA (PlayerSubStateBase)
//         │         └─ SubStateB (PlayerSubStateBase)
//         ├─ MoveState (PlayerRootStateBase)
//         │    └─ SubStateMachine<Player>
//         └─ ...
//

// ────────────────────────────────────────────────────────────────────────────
//  2. 새 엔티티에 HFSM 적용하기 (예: Enemy)
// ────────────────────────────────────────────────────────────────────────────

// ── Step 1: 엔티티 클래스 ──────────────────────────────────────────────────
// MonoBehaviour를 상속하는 엔티티 클래스를 작성합니다.
// LoopManager 등록은 반드시 엔티티에서만 합니다 (상태에서 하지 않음).

public class Enemy : MonoBehaviour
{
    private EnemyRootStateMachine machine;

    private void Awake()
    {
        // 상태 머신 생성 (엔티티 자신을 Owner로 전달)
        machine = new EnemyRootStateMachine(this);
    }

    private void Start()
    {
        // 초기 상태 생성 및 설정
        var idleState = new EnemyIdleState(machine);
        machine.Init(idleState);  // Init()이 OnEnter()도 호출함
    }

    // LoopManager 등록/해제는 엔티티에서만 관리
    private void OnEnable()
    {
        Main.Loop.OnUpdate += OnLoopUpdate;
        Main.Loop.OnGameUpdate += OnLoopGameUpdate;
    }

    private void OnDisable()
    {
        Main.Loop.OnUpdate -= OnLoopUpdate;
        Main.Loop.OnGameUpdate -= OnLoopGameUpdate;
    }

    // OnUpdate → 루트 상태의 Update → 하위 상태의 Update 순으로 전파됨
    private void OnLoopUpdate(float deltaTime)
    {
        machine.OnUpdate(deltaTime);
    }

    // OnGameUpdate → 루트 상태의 FixedUpdate → 하위 상태의 FixedUpdate 순으로 전파됨
    private void OnLoopGameUpdate(float deltaTime)
    {
        machine.OnGameUpdate(deltaTime);
    }
}


// ── Step 2: 루트 상태 머신 ─────────────────────────────────────────────────
// RootStateMachine<TEntity, TRoot>을 상속합니다.
// TEntity: 엔티티 타입, TRoot: 이 엔티티의 루트 상태 베이스 타입

public class EnemyRootStateMachine : RootStateMachine<Enemy, EnemyRootStateBase>
{
    // 엔티티 고유 데이터를 여기에 추가 (애니메이션, 스탯 등)
    public EnemyAnimData AnimData { get; private set; }

    public EnemyRootStateMachine(Enemy enemy) : base(enemy)  // base()에 엔티티 전달 → Owner 프로퍼티로 접근 가능
    {
        AnimData = new EnemyAnimData();
    }
}


// ── Step 3: 루트 상태 베이스 ───────────────────────────────────────────────
// RootStateBase<TEntity>를 상속하는 추상 클래스를 만듭니다.
// 모든 루트 상태(Idle, Move, Attack 등)가 이 클래스를 상속합니다.

public abstract class EnemyRootStateBase : RootStateBase<Enemy>
{
    // Machine을 통해 상태 머신의 공유 데이터에 접근
    protected EnemyRootStateMachine Machine { get; private set; }

    protected EnemyRootStateBase(EnemyRootStateMachine machine) : base(machine.Owner)
    //                                                                  ^^^^^^^^^^^^
    //                                  machine.Owner = Enemy 인스턴스 → 부모의 Entity 프로퍼티에 저장됨
    {
        Machine = machine;
    }

    // ※ Entity 프로퍼티(부모)로 Enemy에 접근 가능
    // ※ Machine 프로퍼티로 상태 머신 공유 데이터(AnimData 등)에 접근 가능
}


// ── Step 4: 하위 상태 베이스 (선택사항) ─────────────────────────────────────
// SubStateBase<TEntity>를 상속합니다.
// 루트 상태 안에서 세분화된 상태가 필요할 때 사용합니다.
// 예: Attack 루트 상태 안에 Windup / Strike / Recovery 하위 상태

public abstract class EnemySubStateBase : SubStateBase<Enemy>
{
    protected EnemyRootStateMachine Machine { get; private set; }

    protected EnemySubStateBase(EnemyRootStateMachine machine) : base(machine.Owner)
    {
        Machine = machine;
    }
}


// ── Step 5: 실제 루트 상태 구현 ────────────────────────────────────────────
// 엔티티별 루트 상태 베이스를 상속하여 구체적인 상태를 만듭니다.

public class EnemyIdleState : EnemyRootStateBase
{
    public EnemyIdleState(EnemyRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();  // 부모의 OnEnter() 호출 (현재는 빈 메서드)
        // Idle 진입 시 로직 (애니메이션 재생 등)
        UnityEngine.Debug.Log("Enemy: Idle 상태 진입");
    }

    public override void OnExit()
    {
        base.OnExit();  // ★ 중요: SubStateMachine.ExitCurrentState() 호출됨
        // Idle 종료 시 정리 로직
    }

    public override void Update(float time = 1.0f)
    {
        base.Update(time);  // ★ 중요: SubStateMachine.OnUpdate() 호출됨 (하위 상태 구동)

        // 상태 전환 조건 체크
        // 예: 플레이어 감지 시 Chase 상태로 전환
        // if (playerDetected)
        //     Machine.ChangeState(new EnemyChaseState(Machine));
    }

    public override void FixedUpdate(float time = 1.0f)
    {
        base.FixedUpdate(time);  // SubStateMachine.OnGameUpdate() 호출됨
        // 물리 관련 업데이트
    }
}


// ── Step 6: 실제 하위 상태 구현 (선택사항) ──────────────────────────────────
// 루트 상태 내에서 더 세분화된 상태가 필요할 때 사용합니다.

public class EnemyAttackState : EnemyRootStateBase
{
    // 하위 상태 인스턴스
    private EnemyAttackWindupSub windupState;
    private EnemyAttackStrikeSub strikeState;

    public EnemyAttackState(EnemyRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        // 하위 상태 생성 및 초기 하위 상태 설정
        windupState = new EnemyAttackWindupSub(Machine);
        strikeState = new EnemyAttackStrikeSub(Machine);
        SubStateMachine.Init(windupState);  // 하위 상태 머신 초기화
    }

    public override void Update(float time = 1.0f)
    {
        base.Update(time);  // ← 이 호출이 SubStateMachine.OnUpdate()를 실행하여
                            //    현재 활성 하위 상태의 Update()를 호출함
    }
}

// 하위 상태 구현 예시
public class EnemyAttackWindupSub : EnemySubStateBase
{
    private float timer;

    public EnemyAttackWindupSub(EnemyRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        timer = 0f;
        UnityEngine.Debug.Log("Attack Windup 시작");
    }

    public override void Update(float time = 1.0f)
    {
        timer += UnityEngine.Time.deltaTime;
        // 일정 시간 후 Strike 하위 상태로 전환
        // if (timer > 0.5f)
        //     Machine의 SubStateMachine에 접근하여 ChangeState 호출
    }
}

public class EnemyAttackStrikeSub : EnemySubStateBase
{
    public EnemyAttackStrikeSub(EnemyRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        UnityEngine.Debug.Log("Attack Strike!");
    }
}


// ────────────────────────────────────────────────────────────────────────────
//  3. 업데이트 흐름 (호출 순서)
// ────────────────────────────────────────────────────────────────────────────
//
//  엔티티.OnLoopUpdate(deltaTime)
//    → machine.OnUpdate(deltaTime)                     [StateMachine<T>]
//      → CurrentState.Update(time)                     [현재 루트 상태]
//        → base.Update(time)                           [RootStateBase<T>]
//          → SubStateMachine.OnUpdate(time)            [SubStateMachine<T>]
//            → CurrentSubState.Update(time)            [현재 하위 상태]
//
//  FixedUpdate도 동일한 흐름:
//  엔티티.OnLoopGameUpdate → machine.OnGameUpdate → CurrentState.FixedUpdate
//    → SubStateMachine.OnGameUpdate → CurrentSubState.FixedUpdate
//

// ────────────────────────────────────────────────────────────────────────────
//  4. 상태 전환 방법
// ────────────────────────────────────────────────────────────────────────────

// ── 루트 상태 전환 ──
// Machine.ChangeState()를 호출합니다.
// 기존 루트 상태의 OnExit() → 새 루트 상태의 OnEnter() 순으로 실행됩니다.
// OnExit() 시 하위 상태도 자동으로 정리됩니다 (SubStateMachine.ExitCurrentState).

// 예: 루트 상태 내에서 다른 루트 상태로 전환
//   Machine.ChangeState(new EnemyChaseState(Machine));

// ── 하위 상태 전환 ──
// SubStateMachine.ChangeState()를 호출합니다.
// 루트 상태 내부에서만 호출합니다.

// 예: 루트 상태 내에서 하위 상태 전환
//   SubStateMachine.ChangeState(strikeState);


// ────────────────────────────────────────────────────────────────────────────
//  5. 핵심 규칙 요약
// ────────────────────────────────────────────────────────────────────────────
//
//  ✓ LoopManager 등록은 엔티티(MonoBehaviour)에서만 — 상태에서 등록하지 않음
//  ✓ base.Update()/base.FixedUpdate() 반드시 호출 — 하위 상태 머신이 구동됨
//  ✓ base.OnExit() 반드시 호출 — 하위 상태가 정리됨
//  ✓ 엔티티 접근은 Entity 프로퍼티 사용 — 다운캐스팅 불필요
//  ✓ 상태 머신 공유 데이터는 Machine 프로퍼티로 접근
//  ✓ FSM 레이어(StateBase, StateMachine<T>)는 수정하지 않음
//
//  새 엔티티 적용 시 최소 필요 파일:
//    1. {Entity}RootStateMachine.cs  — RootStateMachine<Entity, EntityRootStateBase> 상속
//    2. {Entity}RootStateBase.cs     — RootStateBase<Entity> 상속 (추상)
//    3. {Entity}SubStateBase.cs      — SubStateBase<Entity> 상속 (추상, 하위 상태 사용 시)
//    4. 구체 상태 클래스들             — {Entity}RootStateBase 또는 {Entity}SubStateBase 상속
//

#endif
