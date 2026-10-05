using UnityEngine;

/// <summary>
/// 트리앤트 미니언 구르기 돌진 상태. 진입 시점의 타깃 방향으로 직선을 고정하고 그대로 밀고 나간다.
/// 조준이 진입 순간에 굳기 때문에 플레이어는 옆으로 피할 수 있고, 그래서 선행기(뿌리 속박)가 의미를 갖는다.
///
/// 돌진 중 몸통에 닿은 플레이어를 들이받아 데미지와 넉백을 주되, 한 번의 돌진에서 같은 상대는 한 번만 때린다.
/// 돌진이 끝나면 RollRecoverTime만큼 멈춰 숨을 고른 뒤(반격 창) 다시 공격 판단으로 돌아간다.
/// </summary>
public class TreantMinionRollState : MobState<Monster>
{
    private readonly TreantMinionStateMachine sm;
    private readonly TreantMinion treant;

    private Vector3 direction;  // 진입 시점에 고정한 돌진 방향
    private float remaining;    // 남은 돌진 시간
    private float recovering;   // 돌진 후 경직 남은 시간
    private Player hitPlayer;   // 이번 돌진에서 이미 들이받은 상대 (중복 타격 방지)

    public TreantMinionRollState(TreantMinion owner, TreantMinionStateMachine machine) : base(owner, machine)
    {
        treant = owner;
        sm = machine;
    }

    // 구르는 중에 Hit 상태로 끊기면 직선 돌진이 어중간하게 멈춘다. 돌진은 끝까지 유지한다.
    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        direction = treant.RollDirection();

        // 돌진 방향을 즉시 바라보게 한다(이후로는 회전하지 않는다 = 직선).
        if (direction.sqrMagnitude > 0.0001f)
            treant.transform.rotation = Quaternion.LookRotation(direction);

        treant.PlayAnim(MonsterAnimId.RollAttack);
        treant.StartRollCooldown();

        remaining = treant.RollDuration;
        recovering = 0f;
        hitPlayer = null;
    }

    public override void Update(float time = 1.0f)
    {
        // 돌진이 끝난 뒤의 경직. 이 동안은 멈춰 서 있어 플레이어에게 반격 창을 준다.
        if (recovering > 0f)
        {
            recovering -= time;
            if (recovering > 0f) return;

            if (!treant.IsTargetValid())
            {
                treant.ClearTarget();
                sm.ToIdle();
                return;
            }

            sm.ToAttack();
            return;
        }

        treant.transform.position += direction * (treant.RollSpeed * time);

        // 아직 아무도 못 받았을 때만 판정한다(한 돌진 = 한 타격).
        if (hitPlayer == null)
            hitPlayer = treant.RollHitStep();

        remaining -= time;
        if (remaining > 0f) return;

        // 돌진 종료 → 숨 고르기
        recovering = treant.RollRecoverTime;
        treant.PlayAnim(MonsterAnimId.Idle);

        // 경직 시간이 0이면 곧바로 다음 판단으로 넘어간다.
        if (recovering <= 0f)
        {
            if (!treant.IsTargetValid())
            {
                treant.ClearTarget();
                sm.ToIdle();
                return;
            }

            sm.ToAttack();
        }
    }
}
