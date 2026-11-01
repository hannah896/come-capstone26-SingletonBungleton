using UnityEngine;

namespace SingletonBungleton.BossTraining
{
    /// <summary>
    /// 학습된 판단층이 고르는 드래곤 행동. 드래곤의 지상 패턴 4종 + "다가가기".
    /// 패턴의 실제 실행(애니메이션, 판정)은 본편 FSM(DragonSkillState 등)이 하고, 정책은 "무엇을 쓸지"만 정한다.
    /// </summary>
    public enum DragonAction
    {
        Approach = 0,   // 타깃 쪽으로 걷는다 (정지 거리 안이면 제자리에서 몸만 돌림)
        Bite = 1,       // 서리 물기
        Breath = 2,     // 서리 브레스
        Charge = 3,     // 얼음 돌진
        Spit = 4,       // 빙탄 3연사
    }

    /// <summary>
    /// 정책이 보는 "지금 상황" 한 장면. 학습 아레나와 본편 드래곤이 같은 구조로 채운다 —
    /// 같은 관측 함수(<see cref="DragonObs"/>)를 쓰기 때문에, 학습한 모델이 본편에서도 같은 뜻으로 입력을 받는다.
    /// </summary>
    public struct DragonSituation
    {
        /// <summary>타깃까지 수평 거리(m)</summary>
        public float distance;
        /// <summary>드래곤 정면 → 타깃의 부호 있는 각도(도, -180~180)</summary>
        public float angleToTarget;
        /// <summary>타깃의 드래곤 기준 속도: 멀어지는 방향이 +(m/s)</summary>
        public float targetRadialSpeed;
        /// <summary>타깃의 드래곤 기준 속도: 드래곤 오른쪽 방향이 +(m/s)</summary>
        public float targetTangentSpeed;

        public float hpRatio;
        public float targetHpRatio;

        /// <summary>패턴 쿨타임 남은 비율 (0 = 사용 가능, 1 = 막 썼음)</summary>
        public float biteCooldown;
        public float breathCooldown;
        public float chargeCooldown;
        public float spitCooldown;

        /// <summary>아레나 중심 기준 위치를 아레나 반경으로 나눈 값 (-1~1)</summary>
        public Vector2 selfPos;
        public Vector2 targetPos;

        // 정규화용 상수 (패턴 사거리, 아레나 지름, 속도 기준)
        public float biteRange;
        public float breathRange;
        public float chargeMinRange;
        public float spitMinRange;
        public float arenaSize;
        public float speedNorm;
    }

    /// <summary>상황을 제공하는 쪽 (학습 아레나 / 본편 드래곤의 어댑터).</summary>
    public interface IDragonSituationSource
    {
        bool TryGetSituation(out DragonSituation situation);
    }

    /// <summary>
    /// 드래곤의 다음 행동을 정해 주는 판단층 (ML-Agents 에이전트가 구현한다).
    /// 본편 FSM은 이 인터페이스만 알면 되므로, ML-Agents 패키지가 없거나 모델이 없을 때는 기존 규칙 기반 선택으로 되돌아간다.
    /// </summary>
    public interface IDragonBrain
    {
        /// <summary>모델이 연결돼 바로 판단할 수 있는지</summary>
        bool IsReady { get; }

        /// <summary>다음 행동 판단을 요청한다. 결과는 같은 프레임 또는 다음 물리 스텝에 <see cref="TryTakeDecision"/>으로 나온다.</summary>
        void Request();

        /// <summary>요청한 판단이 나왔으면 가져간다 (한 번 가져가면 비워진다).</summary>
        bool TryTakeDecision(out DragonAction action);
    }

    /// <summary>관측 벡터 구성. 학습과 추론이 반드시 같은 함수를 써야 한다.</summary>
    public static class DragonObs
    {
        public const int Size = 19;
        public const int ActionCount = 5;
        public const string BehaviorName = "DragonBoss";

        public static void Fill(in DragonSituation s, float[] o)
        {
            float arena = Mathf.Max(1f, s.arenaSize);
            float angle = s.angleToTarget * Mathf.Deg2Rad;
            float speed = Mathf.Max(1f, s.speedNorm);

            // 거리: 아레나 대비 + 각 패턴 사거리 대비(0~4배를 0~1로). 사거리 대비 값 덕분에 아레나 크기가 달라도 같은 뜻이 된다.
            o[0] = Mathf.Clamp01(s.distance / arena);
            o[1] = RangeRatio(s.distance, s.biteRange);
            o[2] = RangeRatio(s.distance, s.breathRange);
            o[3] = RangeRatio(s.distance, s.chargeMinRange);
            o[4] = RangeRatio(s.distance, s.spitMinRange);

            o[5] = Mathf.Sin(angle);
            o[6] = Mathf.Cos(angle);

            o[7] = Mathf.Clamp(s.targetRadialSpeed / speed, -1f, 1f);
            o[8] = Mathf.Clamp(s.targetTangentSpeed / speed, -1f, 1f);

            o[9] = Mathf.Clamp01(s.hpRatio);
            o[10] = Mathf.Clamp01(s.targetHpRatio);

            o[11] = Mathf.Clamp01(s.biteCooldown);
            o[12] = Mathf.Clamp01(s.breathCooldown);
            o[13] = Mathf.Clamp01(s.chargeCooldown);
            o[14] = Mathf.Clamp01(s.spitCooldown);

            o[15] = Mathf.Clamp(s.selfPos.x, -1f, 1f);
            o[16] = Mathf.Clamp(s.selfPos.y, -1f, 1f);
            o[17] = Mathf.Clamp(s.targetPos.x, -1f, 1f);
            o[18] = Mathf.Clamp(s.targetPos.y, -1f, 1f);
        }

        private static float RangeRatio(float distance, float range)
            => Mathf.Clamp01(distance / Mathf.Max(0.01f, range) / 4f);

        /// <summary>패턴이 쿨타임 중이 아닌지 (행동 마스크에 사용)</summary>
        public static bool IsReady(in DragonSituation s, DragonAction action)
        {
            switch (action)
            {
                case DragonAction.Bite: return s.biteCooldown <= 0f;
                case DragonAction.Breath: return s.breathCooldown <= 0f;
                case DragonAction.Charge: return s.chargeCooldown <= 0f;
                case DragonAction.Spit: return s.spitCooldown <= 0f;
                default: return true;
            }
        }

        /// <summary>
        /// 기존 규칙 기반 선택 (DragonGroundState.TrySelect와 같은 우선순위). 비교 기준(baseline)과 모델이 없을 때의 대체용.
        /// ② 정면 가까이 → 물기 ③ 브레스 사거리 → 브레스 ④ 멀리 → 돌진 ⑤ 거리를 벌림 → 빙탄 ⑥ 전부 쿨 → 다가가기
        /// </summary>
        public static DragonAction RuleBased(in DragonSituation s)
        {
            bool facing = Mathf.Abs(s.angleToTarget) <= 45f;
            if (IsReady(s, DragonAction.Bite) && s.distance <= s.biteRange && facing) return DragonAction.Bite;
            if (IsReady(s, DragonAction.Breath) && s.distance <= s.breathRange * 0.85f) return DragonAction.Breath;
            if (IsReady(s, DragonAction.Charge) && s.distance >= s.chargeMinRange) return DragonAction.Charge;
            if (IsReady(s, DragonAction.Spit) && s.distance >= s.spitMinRange) return DragonAction.Spit;
            return DragonAction.Approach;
        }
    }
}
