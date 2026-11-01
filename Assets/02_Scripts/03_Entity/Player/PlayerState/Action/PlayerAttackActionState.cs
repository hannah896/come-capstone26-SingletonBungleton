using UnityEngine;

/// <summary>
/// 전투 공격 액션. 손에 든 무기/도구로 몬스터·동물을 친다.
/// </summary>
public class PlayerAttackActionState : PlayerActionSubStateBase
{
    private enum Style { Chop, Slash, Thrust, Shoot, Cast }

    private static readonly int s_chopBeginHash = Animator.StringToHash("Action_Chop_Begin");

    // 애니메이터 PlayerAttactState의 무기별 클립(30fps)에서 손 속도로 실측한 원본 시간(초).
    // Rise = 휘두르기 시작까지, Strike = 휘두르기 시작 → 타격 프레임, Length = 클립 전체 길이
    // 실제 재생 시간은 원본 / 상태 Speed. Speed는 애니메이터 상태의 Speed와 반드시 같게 맞출 것.

    // 칼 (Sword Attack / Stable Sword Outward Slash 2.033s): 0.77s부터 베기, 0.87s에 손 속도 최대
    private const float SlashSpeed = 1.5f;
    private const float SlashRise = 0.77f / SlashSpeed;
    private const float SlashStrike = 0.10f / SlashSpeed;
    private const float SlashLength = 2.033f / SlashSpeed;

    // 창 (Spear Attack / Bayonet Stab 3.267s): 0.63s부터 찌르기, 1.0s에 앞으로 뻗음. 2.4s 이후는 거의 정지
    private const float ThrustSpeed = 2f;
    private const float ThrustRise = 0.63f / ThrustSpeed;
    private const float ThrustStrike = 0.37f / ThrustSpeed;
    private const float ThrustLength = 3.267f / ThrustSpeed;

    // 활 (Arrow Attack / Shooting Arrow 5.0s): 1.17s까지 시위를 당기고 조준을 유지하다 1.83s에 놓는다 (이 순간 화살 발사)
    private const float ShootSpeed = 2.5f;
    private const float ShootRise = 1.77f / ShootSpeed;
    private const float ShootStrike = 0.06f / ShootSpeed;
    private const float ShootLength = 5.000f / ShootSpeed;

    // 지팡이 (Staff Attack / Standing 1H Cast Spell 01 2.267s): 0.43s부터 손을 내밀어 0.6s에 시전 (이 순간 회복)
    private const float CastSpeed = 1.5f;
    private const float CastRise = 0.43f / CastSpeed;
    private const float CastStrike = 0.17f / CastSpeed;
    private const float CastLength = 2.267f / CastSpeed;

    private Style style;

    public PlayerAttackActionState(PlayerRootStateMachine machine) : base(machine) { }

    protected override int ActionTrigger => Machine.AnimData.AnimHashKey.Chop;
    protected override bool UsesToolOnComplete => true;
    protected override bool ShouldLoop => true;

    protected override int LoopStateHash => style switch
    {
        Style.Slash => PlayerAnimData.AttackSwordHash,
        Style.Thrust => PlayerAnimData.AttackSpearHash,
        Style.Shoot => PlayerAnimData.AttackArrowHash,
        Style.Cast => PlayerAnimData.AttackStaffHash,
        _ => s_chopBeginHash,
    };

    protected override float SwingRiseDuration => style switch
    {
        Style.Slash => SlashRise,
        Style.Thrust => ThrustRise,
        Style.Shoot => ShootRise,
        Style.Cast => CastRise,
        _ => 1.233f,     // Begin 0.500 + Loop 최고점 0.733
    };

    protected override float SwingStrikeDuration => style switch
    {
        Style.Slash => SlashStrike,
        Style.Thrust => ThrustStrike,
        Style.Shoot => ShootStrike,
        Style.Cast => CastStrike,
        _ => 0.100f,     // Loop 0.733 → 0.833
    };

    protected override float SwingRecoverDuration => style switch
    {
        Style.Slash => SlashLength - SlashRise - SlashStrike,
        Style.Thrust => ThrustLength - ThrustRise - ThrustStrike,
        Style.Shoot => ShootLength - ShootRise - ShootStrike,
        Style.Cast => CastLength - CastRise - CastStrike,
        _ => 1.467f,     // 타격 1.333 → 액션 종료 2.800
    };

    // 창·지팡이는 앞으로 내미는 모양, 활은 시위 당기는 모양으로 1인칭에서도 보이게 한다 (나머지는 위로 들었다 내려찍기)
    protected override ToolSwingKind SwingKind => style switch
    {
        Style.Thrust or Style.Cast => ToolSwingKind.Thrust,
        Style.Shoot => ToolSwingKind.Draw,
        _ => ToolSwingKind.Overhead,
    };

    public override void OnEnter()
    {
        style = ResolveStyle();
        Debug.Log($"[AttackDebug] hand={Entity.Inventory?.EquippedHand?.name} style={style}");
        base.OnEnter();
    }

    // 칼·창·도구 공격은 내려치기 시작에 휘두르는 소리. 활은 화살이 나갈 때(PlayerInventory.FireArrow) 따로 내고, 지팡이는 휘두르지 않는다.
    protected override void OnStrikeStart()
    {
        if (style == Style.Shoot || style == Style.Cast) return;

        Extensions.PlaySFX(AudioLibrarySounds.SwordSwing);
        PlayerNetFx.ShareSound(AudioLibrarySounds.SwordSwing, Entity.transform.position);
    }

    // 도구 공격(Chop)은 Action SM의 chop 트리거, 무기 공격은 Attack SM으로 Attack + 무기 Bool을 켜서 들어간다.
    protected override void PlayAnimation()
    {
        if (style == Style.Chop)
        {
            base.PlayAnimation();
            return;
        }

        Machine.AnimData.PlayAttackAnimation(WeaponBoolHash);
    }

    protected override void ResetAnimation()
    {
        if (style == Style.Chop)
            base.ResetAnimation();
        else
            Machine.AnimData.StopAttackAnimation();
    }

    private int WeaponBoolHash => style switch
    {
        Style.Slash => Machine.AnimData.AnimHashKey.Sword,
        Style.Thrust => Machine.AnimData.AnimHashKey.Spear,
        Style.Shoot => Machine.AnimData.AnimHashKey.Arrow,
        _ => Machine.AnimData.AnimHashKey.Staff,
    };

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
            case CombatGearType.Staff:
                return Style.Cast;
            default:
                return Style.Chop;
        }
    }
}
