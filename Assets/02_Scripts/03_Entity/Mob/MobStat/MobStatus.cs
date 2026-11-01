using System;
using UnityEngine;

/// <summary>
/// 몬스터·동물 공통 런타임 스탯. EntityStatData(SO)로부터 값을 복사해 사용한다.
/// Player와 독립적인 Mob 전용 클래스.
/// </summary>
public class MobStatus
{
    public event Action<float> OnDamaged; // 실제 입은 피해량
    public event Action OnDead;

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
    #endregion

    public bool IsDead => CurrentHp <= 0f;

    public MobStatus(EntityStatData data)
    {
        MaxHp = data.MaxHP;
        CurrentHp = data.CurHP;
        Attack = data.AttackDamage;
        Defense = data.Defense;
        MinAttackPeriod = data.MinAttackPeriod;
        MoveSpeed = data.MoveSpeed;
    }

    public void TakeDamage(float damage)
    {
        if (IsDead) return;

        float finalDamage = Mathf.Max(damage - Defense, 0f);
        CurrentHp = Mathf.Max(CurrentHp - finalDamage, 0f);

        if (finalDamage > 0f)
            OnDamaged?.Invoke(finalDamage);

        if (CurrentHp <= 0f)
            OnDead?.Invoke();
    }

    public void RestoreHp(float amount)
        => CurrentHp = Mathf.Min(CurrentHp + amount, MaxHp);

    /// <summary>최대 체력에 배율을 건다 (보스 인원 보정, 만월 보스 반복 강화). 현재 체력도 같은 비율을 유지한다.</summary>
    public void ScaleMaxHp(float multiplier)
    {
        if (multiplier <= 0f || IsDead) return;
        float ratio = MaxHp > 0f ? CurrentHp / MaxHp : 1f;
        MaxHp *= multiplier;
        CurrentHp = Mathf.Max(MaxHp * ratio, 0.001f);
    }

    /// <summary>저장된 살아 있는 몬스터의 체력을 복원합니다. 사망 이벤트는 발생시키지 않습니다.</summary>
    public void SetCurrentHp(float value)
        => CurrentHp = Mathf.Clamp(value, 0.001f, MaxHp);
}
