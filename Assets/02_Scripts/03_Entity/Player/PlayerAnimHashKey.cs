using UnityEngine;

public class PlayerAnimHashKey
{
    #region ParameterNames
    private readonly string idle = "idle";
    private readonly string walk = "walk";
    private readonly string trace = "trace";
    private readonly string run = "run";

    private readonly string pick = "pick";
    private readonly string mine = "mine";
    private readonly string chop = "chop";
    private readonly string dig = "dig";
    private readonly string ignite = "ignite";
    private readonly string cook = "cook";
    private readonly string inspect = "inspect";
    private readonly string build = "build";

    private readonly string attack = "attack";
    private readonly string sleep = "sleep";
    private readonly string dead = "dead";
    private readonly string hurt = "hurt";
    #endregion

    #region HashProperties
    public int Idle { get; private set; }
    public int Walk { get; private set; }
    public int Trace { get; private set; }
    public int Run { get; private set; }

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
    #endregion

    public PlayerAnimHashKey()
    {
        // Locomotion
        Idle = Animator.StringToHash(idle);
        Walk = Animator.StringToHash(walk);
        Trace = Animator.StringToHash(trace);
        Run = Animator.StringToHash(run);

        // Action
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
    }
}
