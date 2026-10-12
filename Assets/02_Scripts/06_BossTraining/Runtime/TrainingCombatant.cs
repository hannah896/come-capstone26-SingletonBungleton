using UnityEngine;

namespace SingletonBungleton.BossTraining
{
    /// <summary>보스가 맞서는 대상 / 보스 자신의 체력·속도를 담는 학습용 전투 참여자. (게임 본편의 Mob/Player와는 무관한 가벼운 클래스)</summary>
    public class TrainingCombatant : MonoBehaviour
    {
        public float MaxHp { get; private set; } = 1f;
        public float Hp { get; private set; } = 1f;

        /// <summary>마지막 틱의 아레나 로컬 이동 속도(m/s). 관측값 계산용.</summary>
        public Vector3 Velocity { get; set; }

        public float HpRatio => MaxHp > 0f ? Mathf.Clamp01(Hp / MaxHp) : 0f;
        public bool IsDead => Hp <= 0f;

        public void ResetHp(float maxHp)
        {
            MaxHp = Mathf.Max(1f, maxHp);
            Hp = MaxHp;
            Velocity = Vector3.zero;
        }

        /// <summary>데미지를 적용하고 실제로 깎인 양을 반환한다.</summary>
        public float TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return 0f;

            float applied = Mathf.Min(Hp, amount);
            Hp -= applied;
            return applied;
        }
    }
}
