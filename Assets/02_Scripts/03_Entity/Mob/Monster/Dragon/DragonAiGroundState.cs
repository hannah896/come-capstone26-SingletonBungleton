using SingletonBungleton.BossTraining;
using UnityEngine;

/// <summary>
/// 드래곤 지상 교전 상태 — 강화학습(ML-Agents/PPO)으로 학습한 판단층이 다음 패턴을 고른다.
///
/// 기존 <see cref="DragonGroundState"/>는 "거리·방향·쿨타임" 규칙 우선순위로 패턴을 골랐다.
/// 이 상태는 같은 자리에서 학습된 정책(<see cref="IDragonBrain"/>)에 묻는다:
///   정책이 고른 행동 → 물기/브레스/돌진/빙탄이면 해당 패턴 상태로, 다가가기면 DecisionInterval 동안 걷는다.
/// 이륙·응시·발악 같은 체력 기반 페이즈 전환과 패턴의 실제 실행(DragonSkillState·DragonChargeState)은 기존 그대로다.
///
/// 정책이 없거나 준비되지 않았으면 <see cref="CanUse"/>가 false라 기존 규칙 상태가 쓰인다.
/// </summary>
public class DragonAiGroundState : DragonGroundState
{
    /// <summary>'다가가기'를 고른 뒤 다시 판단하기까지 걷는 시간(초). 학습 아레나의 decisionInterval과 같다.</summary>
    private const float DecisionInterval = 0.5f;

    private readonly DragonStateMachine sm;
    private readonly DragonBoss boss;
    private readonly IDragonBrain brain;

    private float gapTimer;
    private float walkTimer;
    private bool requested;

    public DragonAiGroundState(DragonBoss owner, DragonStateMachine machine, IDragonBrain dragonBrain) : base(owner, machine)
    {
        boss = owner;
        sm = machine;
        brain = dragonBrain;
    }

    /// <summary>호스트(또는 싱글)에서 학습 모델이 준비돼 있을 때만 쓴다. 멀티 클라는 호스트가 계산한 결과를 복제로 받는다.</summary>
    public static bool CanUse(DragonBoss owner, out IDragonBrain brain)
    {
        brain = null;
        if (owner == null || Monster.IsSimulatedPeer) return false;

        brain = owner.GetComponent<IDragonBrain>();
        return brain != null && brain.IsReady;
    }

    public override void OnEnter()
    {
        gapTimer = boss.SkillGap;
        walkTimer = 0f;
        requested = false;
    }

    public override void Update(float time = 1.0f)
    {
        if (!boss.IsTargetValid() && !boss.AcquireTarget())
        {
            boss.ClearTarget();
            sm.ToIdle();
            return;
        }

        // 체력 기반 페이즈 전환은 판단층과 무관하게 규칙대로 (하던 패턴이 끝난 뒤 맨 처음에)
        if (boss.IsTakeOffPending)
        {
            sm.ToTakeOff();
            return;
        }

        if (boss.IsGazePending)
        {
            sm.ToGaze();
            return;
        }

        if (boss.IsFrenzyPending)
        {
            sm.ToFrenzy();
            return;
        }

        if (gapTimer > 0f)
        {
            gapTimer -= time;
            Approach(time);
            return;
        }

        if (walkTimer > 0f)
        {
            walkTimer -= time;
            Approach(time);
            return;
        }

        // 판단 요청 → 결과가 나오면 실행. 결과가 나오기 전 몇 프레임은 접근 동작을 이어간다.
        if (!requested)
        {
            brain.Request();
            requested = true;
        }

        if (requested && brain.TryTakeDecision(out DragonAction action))
        {
            requested = false;
            if (TryExecute(action)) return;

            walkTimer = DecisionInterval;
        }

        Approach(time);
    }

    private bool TryExecute(DragonAction action)
    {
        DragonSkill skill;
        switch (action)
        {
            case DragonAction.Bite: skill = DragonSkill.Bite; break;
            case DragonAction.Breath: skill = DragonSkill.Breath; break;
            case DragonAction.Charge: skill = DragonSkill.Charge; break;
            case DragonAction.Spit: skill = DragonSkill.Spit; break;
            default: return false;
        }

        if (!boss.IsSkillReady(skill)) return false;

        // 돌진은 기존 규칙처럼 가장 먼 플레이어를 노린다
        if (skill == DragonSkill.Charge)
        {
            var players = boss.CollectPlayers();
            if (players.Count > 0) boss.SetTarget(players[players.Count - 1]);
        }

        sm.ToSkill(skill);
        return true;
    }

    // 정지 거리 밖이면 걷고, 안이면 제자리에서 몸만 돌린다 (DragonGroundState.Approach와 같은 동작)
    private void Approach(float time)
    {
        var d = boss.Data;
        float stop = d != null ? d.StopDistance : 6f;
        if (boss.TargetDistance > stop)
        {
            boss.PlayAnim(MonsterAnimId.Move);
            boss.ChaseStep(time);
        }
        else
        {
            boss.PlayAnim(MonsterAnimId.Idle);
            boss.FaceTargetStep(time);
        }
    }
}
