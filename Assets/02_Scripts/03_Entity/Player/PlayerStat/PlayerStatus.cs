using UnityEngine;

public class PlayerStatus
{
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

    #region 정신력
    public float MaxMental { get; private set; }
    public float CurrentMental { get; private set; }
    public float MentalDecreaseRate { get; private set; }
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

        // 정신력
        MaxMental = data.MaxMental;
        CurrentMental = data.CurMental;
        MentalDecreaseRate = data.MentalDecreaseRate;

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
    }

    public bool IsDead => CurrentHp <= 0f;
}