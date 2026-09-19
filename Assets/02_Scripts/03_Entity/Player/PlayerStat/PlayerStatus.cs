using System;
using UnityEngine;

public class PlayerStatus
{
    public event Action<float> OnDamaged;

    // 디버그용 무적 — 데미지/허기로 인한 체력 감소를 무시하고 체력이 0이어도 사망하지 않음
    public bool Invincible { get; set; }

    #region Fields
    #region 체력
    public float MaxHp { get; private set; }
    public float CurrentHp { get; private set; }
    #endregion

    #region 전투
    public float Attack { get; private set; }
    public float Defense { get; private set; }
    public float MinAttackPeriod { get; private set; }
    #endregion

    #region 이동
    public float MoveSpeed { get; private set; }
    public float SprintMultiplier { get; private set; }
    public float JumpForce { get; private set; }
    #endregion

    #region 허기
    public float MaxHunger { get; private set; }
    public float CurrentHunger { get; private set; }
    public float HungerDrain { get; private set; }
    public float HungerHPDecreaseRate { get; private set; }
    #endregion

    #region Ego
    public float MaxEgo { get; private set; }
    public float CurrentEgo { get; private set; }
    public float EgoDecreaseRate { get; private set; }
    #endregion

    #region 체온
    public float Temperature { get; private set; }
    public float ColdHPDecreaseRate { get; private set; }
    public float HotHPDecreaseRate { get; private set; }
    public float MaxColdTemperature { get; private set; }
    public float MaxHotTemperature { get; private set; }
    #endregion

    #region 습도
    public float Wetness { get; private set; }
    public float MaxWetness { get; private set; }
    public float WetnessTemperatureDecreaseRate { get; private set; }
    public float WaterproofRate { get; private set; }
    #endregion

    #region 도트 데미지
    private float dotDamagePerTick;
    private float dotTickInterval;
    private float dotTickTimer;    // 다음 틱까지 남은 시간
    private int dotTicksLeft;      // 남은 틱 횟수
    /// <summary>도트 데미지가 걸려 있는지.</summary>
    public bool IsDotActive => dotTicksLeft > 0;
    #endregion
    #endregion

    public PlayerStatus(PlayerStatData data)
    {
        // EntityStatData 필드 
        MaxHp = data.MaxHP;
        CurrentHp = data.CurHP;
        Attack = data.AttackDamage;
        Defense = data.Defense;
        MinAttackPeriod = data.MinAttackPeriod;
        MoveSpeed = data.MoveSpeed;
        SprintMultiplier = data.SprintMultiplier;
        JumpForce = data.JumpForce;

        // 허기
        MaxHunger = data.MaxHunger;
        CurrentHunger = data.CurHunger;
        HungerDrain = data.HungerDrain;
        HungerHPDecreaseRate = data.HungerHPDecreaseRate;

        // Ego
        MaxEgo = data.MaxEgo;
        CurrentEgo = data.CurEgo;
        EgoDecreaseRate = data.EgoDecreaseRate;

        // 체온
        Temperature = data.Temperature;
        ColdHPDecreaseRate = data.ColdHPDecreaseRate;
        HotHPDecreaseRate = data.HotHPDecreaseRate;
        MaxColdTemperature = data.MaxColdTemperature;
        MaxHotTemperature = data.MaxHotTemperature;

        // 습도
        Wetness = data.Wetness;
        MaxWetness = data.MaxWetness;
        WetnessTemperatureDecreaseRate = data.WetnessTemperatureDecreaseRate;
    }

    public void TakeDamage(float damage)
    {
        if (Invincible) return;

        float finalDamage = Mathf.Max(damage - Defense, 0f);
        CurrentHp = Mathf.Max(CurrentHp - finalDamage, 0f);
        if (finalDamage > 0f)
            OnDamaged?.Invoke(finalDamage);
    }

    /// <summary>
    /// 허기를 시간 경과에 따라 감소시킵니다.
    /// HungerDrain은 분당 감소량이므로 초당으로 환산합니다.
    /// 허기가 0이 되면 HungerHPDecreaseRate 비율로 체력을 감소시킵니다.
    /// </summary>
    public void UpdateHunger(float deltaTime)
    {
        CurrentHunger = Mathf.Max(CurrentHunger - HungerDrain / 60f * deltaTime, 0f);

        if (CurrentHunger <= 0f && !Invincible)
            CurrentHp = Mathf.Max(CurrentHp - HungerHPDecreaseRate * deltaTime, 0f);
    }

    /// <summary>
    /// Ego을 시간 경과에 따라 감소시킵니다.
    /// EgoDecreaseRate는 초당 감소량입니다.
    /// </summary>
    public void UpdateEgo(float deltaTime)
    {
        CurrentEgo = Mathf.Max(CurrentEgo - EgoDecreaseRate * deltaTime, 0f);
    }

    /// <summary>
    /// 도트 데미지를 건다. tickInterval마다 damagePerTick씩 duration 동안 체력을 깎는다.
    /// 이미 걸려 있으면 중첩하지 않고 갱신한다(더 센 틱 데미지·더 긴 남은 횟수를 유지).
    /// 틱 타이머는 이어서 돌리므로 연속 피격으로 틱이 계속 밀리지 않는다.
    /// </summary>
    public void ApplyDot(float damagePerTick, float duration, float tickInterval = 1f)
    {
        if (damagePerTick <= 0f || duration <= 0f) return;

        tickInterval = Mathf.Max(tickInterval, 0.01f);
        int ticks = Mathf.Max(Mathf.RoundToInt(duration / tickInterval), 1);

        if (IsDotActive)
        {
            dotDamagePerTick = Mathf.Max(dotDamagePerTick, damagePerTick);
            dotTicksLeft = Mathf.Max(dotTicksLeft, ticks);
            return;
        }

        dotDamagePerTick = damagePerTick;
        dotTickInterval = tickInterval;
        dotTicksLeft = ticks;
        dotTickTimer = tickInterval; // 첫 틱은 interval 뒤에 들어간다
    }

    /// <summary>
    /// 도트 데미지를 시간 경과에 따라 적용한다.
    /// 틱마다 방어력을 무시하고 체력을 깎으며, 피격 경직(OnDamaged)은 발생시키지 않는다.
    /// </summary>
    public void UpdateDot(float deltaTime)
    {
        if (!IsDotActive) return;

        if (IsDead)
        {
            ClearDot();
            return;
        }

        dotTickTimer -= deltaTime;
        while (dotTickTimer <= 0f && dotTicksLeft > 0)
        {
            if (!Invincible)
                CurrentHp = Mathf.Max(CurrentHp - dotDamagePerTick, 0f);

            dotTicksLeft--;
            dotTickTimer += dotTickInterval;
        }
    }

    /// <summary>걸려 있는 도트 데미지를 제거한다.</summary>
    public void ClearDot() => dotTicksLeft = 0;

    public void RestoreHunger(float amount)
        => CurrentHunger = Mathf.Clamp(CurrentHunger + amount, 0f, MaxHunger);

    public void RestoreHp(float amount)
        => CurrentHp = Mathf.Clamp(CurrentHp + amount, 0f, MaxHp);

    public void RestoreEgo(float amount)
        => CurrentEgo = Mathf.Clamp(CurrentEgo + amount, 0f, MaxEgo);

    public PlayerStatusSaveData CaptureSaveData() => new()
    {
        hp = CurrentHp, hunger = CurrentHunger, ego = CurrentEgo,
        temperature = Temperature, wetness = Wetness,
        dotDamagePerTick = dotDamagePerTick, dotTickInterval = dotTickInterval,
        dotTickTimer = dotTickTimer, dotTicksLeft = dotTicksLeft
    };

    public void RestoreSaveData(PlayerStatusSaveData saved)
    {
        if (saved == null) throw new ArgumentNullException(nameof(saved));
        CurrentHp = Mathf.Clamp(saved.hp, 0f, MaxHp);
        CurrentHunger = Mathf.Clamp(saved.hunger, 0f, MaxHunger);
        CurrentEgo = Mathf.Clamp(saved.ego, 0f, MaxEgo);
        Temperature = saved.temperature;
        Wetness = Mathf.Clamp(saved.wetness, 0f, MaxWetness);
        dotDamagePerTick = Mathf.Max(0f, saved.dotDamagePerTick);
        dotTickInterval = Mathf.Max(0.01f, saved.dotTickInterval);
        dotTickTimer = Mathf.Clamp(saved.dotTickTimer, 0f, dotTickInterval);
        dotTicksLeft = Mathf.Max(0, saved.dotTicksLeft);
    }

    public bool IsDead => !Invincible && CurrentHp <= 0f;
}
