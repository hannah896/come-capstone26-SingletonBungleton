using UnityEngine;

/// <summary>
/// 이륙 상태. 루트 Transform의 Y를 FlyHeight까지 끌어올린다.
/// 콜라이더가 루트에 붙어 있으므로 이륙과 함께 판정도 올라가 근접 공격이 닿지 않게 된다.
/// 연출이 끝나면 비행 공격 판단(ToAttack)으로 넘어간다.
/// </summary>
public class DevilTakeOffState : MobState<Monster>
{
    private readonly DevilStateMachine sm;
    private readonly Devil devil;

    private float startY;
    private float targetY;
    private float progress;

    public DevilTakeOffState(Devil owner, DevilStateMachine machine) : base(owner, machine)
    {
        devil = owner;
        sm = machine;
    }

    // 이륙 도중 피격돼도 Hit 상태로 끊기지 않는다.
    // (중간에 끊기면 루트 Y가 어중간한 높이에 멈춘 채 남는다)
    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        // 착지 때 돌아갈 지면 높이를 먼저 기억한다.
        devil.RememberGroundY();

        startY = devil.transform.position.y;
        targetY = startY + devil.FlyHeight;
        progress = 0f;

        devil.EnterFlyStance();
        devil.PlayAnim(MonsterAnimId.TakeOff);
    }

    public override void Update(float time = 1.0f)
    {
        float duration = Mathf.Max(devil.TakeOffDuration, 0.0001f);
        progress += time / duration;
        float p = Mathf.Clamp01(progress);

        Vector3 pos = devil.transform.position;
        devil.transform.position = new Vector3(pos.x, Mathf.Lerp(startY, targetY, p), pos.z);

        if (progress < 1f) return;

        if (!devil.IsTargetValid())
        {
            devil.ClearTarget();
            // 타깃을 잃었어도 이미 공중이다. 그대로 내려앉는다.
            sm.ToLand();
            return;
        }

        sm.ToAttack();
    }
}
