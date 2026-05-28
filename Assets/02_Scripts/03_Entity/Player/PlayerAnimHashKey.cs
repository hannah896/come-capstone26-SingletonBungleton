using UnityEngine;

public class PlayerAnimHashKey
{
    #region ParameterNames
    // 애니메이터 Trigger 파라미터명과 정확히 일치해야 함 (대소문자 구분)

    // 루트 상태 라우팅용 Bool (RootState 진입 시 해당 bool만 켜고 나머지는 끈다)
    private readonly string locomotion = "Locomotion";
    private readonly string action = "Action";

    private readonly string idle = "Idle";
    private readonly string walk = "Walk";
    private readonly string trace = "Trace";
    private readonly string run = "Run";
    private readonly string jump = "Jump";

    private readonly string pick = "pick";
    private readonly string mine = "mine";
    private readonly string chop = "chop";
    private readonly string dig = "dig";
    private readonly string ignite = "ignite";
    private readonly string cook = "cook";
    private readonly string inspect = "inspect";
    private readonly string build = "build";

    private readonly string attack = "Attack";
    private readonly string sleep = "sleep";
    private readonly string dead = "Dead";
    private readonly string hurt = "Hurt";
    private readonly string hit = "Hit";
    #endregion

    #region HashProperties
    // 루트 상태 라우팅용 Bool
    public int Locomotion { get; private set; }
    public int Action { get; private set; }

    public int Idle { get; private set; }
    public int Walk { get; private set; }
    public int Trace { get; private set; }
    public int Run { get; private set; }
    public int Jump { get; private set; }

    public int Pick { get; private set; }
    public int Mine { get; private set; }
    public int Chop { get; private set; }
    public int Dig { get; private set; }
    public int Ignite { get; private set; }
    public int Cook { get; private set; }
    public int Inspect { get; private set; }
    public int Build { get; private set; }

    public int Attack { get; private set; }
    public int Sleep { get; private set; }
    public int Dead { get; private set; }
    public int Hurt { get; private set; }
    public int Hit { get; private set; }
    #endregion

    public PlayerAnimHashKey()
    {
        // 루트 상태 라우팅용 Bool
        Locomotion = Animator.StringToHash(locomotion);
        Action = Animator.StringToHash(action);

        // Locomotion
        Idle = Animator.StringToHash(idle);
        Walk = Animator.StringToHash(walk);
        Trace = Animator.StringToHash(trace);
        Run = Animator.StringToHash(run);
        Jump = Animator.StringToHash(jump);

        // Action (트리거)
        Pick = Animator.StringToHash(pick);
        Mine = Animator.StringToHash(mine);
        Chop = Animator.StringToHash(chop);
        Dig = Animator.StringToHash(dig);
        Ignite = Animator.StringToHash(ignite);
        Cook = Animator.StringToHash(cook);
        Inspect = Animator.StringToHash(inspect);
        Build = Animator.StringToHash(build);

        // Combat & Special
        Attack = Animator.StringToHash(attack);
        Sleep = Animator.StringToHash(sleep);
        Dead = Animator.StringToHash(dead);
        Hurt = Animator.StringToHash(hurt);
        Hit = Animator.StringToHash(hit);
    }
}
