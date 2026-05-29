using System;
using UnityEngine;

public class PlayerStatus
{
    public event Action<float> OnDamaged;
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

        if (CurrentHunger <= 0f)
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

    public void RestoreHunger(float amount)
        => CurrentHunger = Mathf.Min(CurrentHunger + amount, MaxHunger);

    public void RestoreHp(float amount)
        => CurrentHp = Mathf.Min(CurrentHp + amount, MaxHp);

    public void RestoreEgo(float amount)
        => CurrentEgo = Mathf.Min(CurrentEgo + amount, MaxEgo);

    public bool IsDead => CurrentHp <= 0f;
}
