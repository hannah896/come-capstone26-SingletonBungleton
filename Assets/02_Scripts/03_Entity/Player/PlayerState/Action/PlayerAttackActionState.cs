using UnityEngine;

/// <summary>
/// 전투 공격 액션. 손에 든 무기/도구로 몬스터·동물을 친다.
/// </summary>
public class PlayerAttackActionState : PlayerActionSubStateBase
{
    private enum Style { Chop, Slash, Thrust, Shoot }

    private static readonly int s_chopBeginHash = Animator.StringToHash("Action_Chop_Begin");

    // 실제로 보면서 어긋나면 Rise 값만 조정할 것. (Strike는 타격 연출 길이)
    // 칼: 타격 시점(Rise+Strike = 0.55s)은 유지하고 내려치기를 0.25s로 늘렸다.
    // Strike가 0.1s면 1인칭에서 칼이 내려오는 장면이 안 보여서 "클릭하자마자 맞는" 느낌이 났다.
    private const float SlashRise = 0.30f;
    private const float SlashStrike = 0.25f;
    private const float SlashLength = 1.467f;

    private const float ThrustRise = 0.50f;
    private const float ThrustStrike = 0.10f;
    private const float ThrustLength = 1.533f;   // 2.3s / 1.5배속

    // 활: Rise = 시위 당기는 시간(끝나는 순간 화살 발사), Strike = 놓는 순간
    private const float ShootRise = 0.35f;
    private const float ShootStrike = 0.06f;
    private const float ShootLength = 0.80f;

    private Style style;

    public PlayerAttackActionState(PlayerRootStateMachine machine) : base(machine) { }

    protected override int ActionTrigger => Machine.AnimData.AnimHashKey.Chop;
    protected override bool UsesToolOnComplete => true;
    protected override bool ShouldLoop => true;

    protected override int LoopStateHash => style switch
    {
        Style.Slash => PlayerAnimData.AttackSlashHash,
        Style.Thrust => PlayerAnimData.AttackThrustHash,
        Style.Shoot => PlayerAnimData.AttackShootHash,
        _ => s_chopBeginHash,
    };

    protected override float SwingRiseDuration => style switch
    {
        Style.Slash => SlashRise,
        Style.Thrust => ThrustRise,
        Style.Shoot => ShootRise,
        _ => 1.233f,     // Begin 0.500 + Loop 최고점 0.733
    };

    protected override float SwingStrikeDuration => style switch
    {
        Style.Slash => SlashStrike,
        Style.Thrust => ThrustStrike,
        Style.Shoot => ShootStrike,
        _ => 0.100f,     // Loop 0.733 → 0.833
    };

    protected override float SwingRecoverDuration => style switch
    {
        Style.Slash => SlashLength - SlashRise - SlashStrike,
        Style.Thrust => ThrustLength - ThrustRise - ThrustStrike,
        Style.Shoot => ShootLength - ShootRise - ShootStrike,
        _ => 1.467f,     // 타격 1.333 → 액션 종료 2.800
    };

    // 창은 앞으로 찌르는 모양, 활은 시위 당기는 모양으로 1인칭에서도 보이게 한다 (나머지는 위로 들었다 내려찍기)
    protected override ToolSwingKind SwingKind => style switch
    {
        Style.Thrust => ToolSwingKind.Thrust,
        Style.Shoot => ToolSwingKind.Draw,
        _ => ToolSwingKind.Overhead,
    };

    public override void OnEnter()
    {
        style = ResolveStyle();
        Debug.Log($"[AttackDebug] hand={Entity.Inventory?.EquippedHand?.name} style={style}");
        base.OnEnter();
    }

    // 칼·창·도구 공격은 내려치기 시작에 휘두르는 소리. 활은 화살이 나갈 때(PlayerInventory.FireArrow) 따로 낸다.
    protected override void OnStrikeStart()
    {
        if (style != Style.Shoot)
            Extensions.PlaySFX(AudioLibrarySounds.SwordSwing);
    }

    protected override void PlayAnimation()
    {
        if (style == Style.Chop)
        {
            base.PlayAnimation();
            return;
        }

        Machine.AnimData.PlayActionState(LoopStateHash);
    }

    protected override void ResetAnimation()
    {
        if (style == Style.Chop)
            base.ResetAnimation();
    }

    private Style ResolveStyle()
    {
        ItemDataSO hand = Entity.Inventory != null ? Entity.Inventory.EquippedHand : null;
        if (hand == null || hand.itemType != ItemType.CombatGear) return Style.Chop;

        switch (hand.combatGearType)
        {
            case CombatGearType.Sword:
                return Style.Slash;
            case CombatGearType.Spear:
                return Style.Thrust;
            case CombatGearType.Bow:
                return Style.Shoot;
            default:
                return Style.Chop;
        }
    }
}
