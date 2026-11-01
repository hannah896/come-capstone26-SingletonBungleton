using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace SingletonBungleton.BossTraining
{
    /// <summary>한 에피소드의 결과 요약 (TensorBoard 통계, CSV 평가 기록, 학습 화면 HUD에 쓴다).</summary>
    public class DragonEpisodeSummary
    {
        public EpisodeResult result;
        public float seconds;
        public float damageDealt;   // 드래곤이 플레이어에게 준 피해
        public float damageTaken;   // 드래곤이 받은 피해
        public readonly int[] uses = new int[4];   // 물기, 브레스, 돌진, 빙탄 순
        public readonly int[] hits = new int[4];
    }

    /// <summary>
    /// 최종 보스 드래곤 판단층 학습용 1:1 아레나 한 칸.
    ///
    /// - 드래곤의 사거리·선딜·쿨타임·피해량은 본편 DragonStatData(PolarDragon)의 값을 그대로 쓴다. 학습 결과가 본편에서도 같은 뜻이 되게 하려는 것.
    /// - 물리 엔진 없이 아레나 로컬 좌표를 직접 갱신하는 결정론적 시뮬레이션이라 학습이 빠르다 (원형 아레나, 반경 32m = 본편 ArenaRadius).
    /// - 드래곤은 "다음에 무엇을 쓸지"만 외부(에이전트)가 정한다: <see cref="SetBossAction"/>. 선택한 패턴이 실제로 맞는지는 이 클래스의
    ///   타이밍 규칙이 판정한다 (물기 선딜 0.55s, 돌진 예고 1.0s, 빙탄 비행 0.9s 등 — 규칙 기반 상대는 이 예고를 보고 가끔 피한다).
    /// - ML-Agents 패키지가 없어도 컴파일되고, 에이전트 없이도 상대 AI와 규칙 검증이 돌아간다.
    /// </summary>
    public class DragonTrainingArena : MonoBehaviour, IDragonSituationSource
    {
        private enum Phase { Free, Bite, Breath, Charge, Spit }

        private struct SpitShot
        {
            public Vector3 impactPos;
            public float impactTime;
        }

        [Header("참조")]
        [SerializeField] private TrainingCombatant boss;
        [SerializeField] private TrainingCombatant player;

        [Header("아레나")]
        [Tooltip("아레나 반경(m). 본편 DragonStatData.ArenaRadius와 같게 둔다.")]
        [SerializeField] private float arenaRadius = 32f;
        [SerializeField] private float edgeMargin = 1.5f;
        [SerializeField] private float minSpawnDistance = 12f;
        [Tooltip("에피소드 최대 시간(초). 넘기면 무승부(중단)로 끝난다.")]
        [SerializeField] private float maxEpisodeSeconds = 90f;

        [Header("드래곤 (본편 PolarDragon 값)")]
        [Tooltip("학습용 체력. 본편은 4인 기준 6000이지만 1:1 스파링에 맞춰 줄였다.")]
        [SerializeField] private float bossMaxHp = 1500f;
        [SerializeField] private float moveSpeed = 2.5f;
        [SerializeField] private float turnSpeed = 70f;
        [SerializeField] private float aimTurnSpeed = 240f;
        [SerializeField] private float skillGap = 0.3f;
        [SerializeField] private float stopDistance = 7.5f;
        [Tooltip("'다가가기'를 고르면 이 시간(초) 동안 걷고 다시 판단한다.")]
        [SerializeField] private float decisionInterval = 0.5f;
        [Tooltip("패턴 시작 직후 타깃 쪽으로 몸을 돌리는 시간(초). 이후 방향이 고정된다.")]
        [SerializeField] private float aimTime = 0.4f;

        [Header("서리 물기")]
        [SerializeField] private float biteRange = 8f;
        [SerializeField] private float biteAngle = 60f;
        [SerializeField] private float biteDamage = 45f;
        [SerializeField] private float biteHitTime = 0.55f;
        [SerializeField] private float biteDuration = 1.33f;
        [SerializeField] private float biteCooldown = 4f;

        [Header("서리 브레스")]
        [SerializeField] private float breathRange = 14f;
        [SerializeField] private float breathHalfAngle = 15f;
        [SerializeField] private float breathStartTime = 1f;
        [SerializeField] private float breathEndTime = 2.85f;
        [SerializeField] private float breathDuration = 3.33f;
        [SerializeField] private float breathSweepAngle = 90f;
        [SerializeField] private float breathTickInterval = 0.2f;
        [SerializeField] private float breathTickDamage = 8f;
        [SerializeField] private float breathCooldown = 14f;

        [Header("얼음 돌진")]
        [SerializeField] private float chargeWarningTime = 1f;
        [SerializeField] private float chargeSpeed = 9f;
        [SerializeField] private float chargeMaxDistance = 28f;
        [SerializeField] private float chargeOvershoot = 4f;
        [SerializeField] private float chargeWidth = 4.5f;
        [SerializeField] private float chargeDamage = 40f;
        [SerializeField] private float chargeRecovery = 0.6f;
        [SerializeField] private float chargeMinRange = 10f;
        [SerializeField] private float chargeCooldown = 12f;

        [Header("빙탄 3연사")]
        [SerializeField] private int spitCount = 3;
        [SerializeField] private float spitReleaseTime = 0.45f;
        [SerializeField] private float spitInterval = 1f;
        [SerializeField] private float spitFlightTime = 0.9f;
        [SerializeField] private float spitImpactRadius = 2.2f;
        [SerializeField] private float spitDamage = 25f;
        [SerializeField] private float spitMinRange = 7f;
        [SerializeField] private float spitCooldown = 9f;

        [Header("스파링 플레이어")]
        [SerializeField] private bool runPlayerAI = true;
        [Tooltip("스파링 상대 체력. 규칙 기반 AI가 절반쯤만 쓰러뜨리는 수준으로 맞춰 학습 전후 차이가 보이게 한다. (프리팹에 저장된 값이 우선)")]
        [SerializeField] private float playerMaxHp = 400f;
        [SerializeField] private float playerMoveSpeed = 5f;
        [Tooltip("드래곤 몸이 커서 루트에서 이 거리 안이면 공격이 닿는다.")]
        [SerializeField] private float playerAttackRange = 7f;
        [SerializeField] private float playerDamage = 22f;
        [SerializeField] private float playerAttackCooldown = 0.8f;
        [Tooltip("패턴 예고를 보고 피하는 확률의 범위. 에피소드마다 이 안에서 새로 뽑아 상대 실력이 매번 다르다.")]
        [SerializeField] private Vector2 dodgeChanceRange = new(0.1f, 0.85f);

        [Header("평가 기록")]
        [Tooltip("비워두면 CSV를 남기지 않는다. 값이 있으면 results/dragon_eval.csv에 에피소드마다 한 줄 추가한다. (에이전트가 Heuristic/Inference 모드일 때 자동 지정)")]
        [SerializeField] private string evalLabel = "";

        // ── 외부에 알리는 이벤트 (에이전트가 보상 계산에 사용) ──
        /// <summary>드래곤이 플레이어를 맞췄다 (실제 피해, 어떤 패턴인지)</summary>
        public event Action<float, DragonAction> BossHitPlayer;
        /// <summary>플레이어가 드래곤을 맞췄다 (실제 피해)</summary>
        public event Action<float> PlayerHitBoss;
        /// <summary>패턴이 끝났다 (패턴, 한 번이라도 맞췄는지)</summary>
        public event Action<DragonAction, bool> SkillFinished;
        /// <summary>에피소드가 끝났다</summary>
        public event Action<EpisodeResult, DragonEpisodeSummary> EpisodeEnded;

        public TrainingCombatant Boss => boss;
        public TrainingCombatant Player => player;
        public bool EpisodeActive { get; private set; }
        public float EpisodeTime { get; private set; }
        public float PlayerMaxHp => playerMaxHp;
        public float BossMaxHp => bossMaxHp;
        public string EvalLabel { get => evalLabel; set => evalLabel = value ?? ""; }

        /// <summary>드래곤이 새 판단을 받을 수 있는 상태인지 (패턴 중이 아니고, 패턴 간격이 지났고, 걷는 중이 아님)</summary>
        public bool CanDecide => EpisodeActive && phase == Phase.Free && gapTimer <= 0f && approachTimer <= 0f && !actionPending;

        // 드래곤 상태
        private Vector3 bossPos;
        private float bossYaw;
        private Phase phase;
        private float phaseTime;
        private DragonAction activeSkill;
        private bool skillLanded;
        private float gapTimer;
        private float approachTimer;
        private bool actionPending;
        private DragonAction pendingAction;
        private readonly float[] cooldowns = new float[4];
        private readonly bool[] landedThisUse = new bool[4];   // 패턴 1회 사용당 적중은 한 번만 센다 (브레스 틱 중복 방지)

        // 브레스
        private float breathBaseYaw;
        private float breathNextTick;
        private float breathConeYaw;

        // 돌진
        private int chargeStage;     // 0 예고, 1 돌진, 2 후딜
        private Vector3 chargeDir;
        private float chargeRemain;
        private bool chargeHitDone;
        private float chargeRecoverTimer;

        // 빙탄
        private int spitReleased;
        private readonly SpitShot[] spitShots = new SpitShot[8];
        private int spitShotCount;

        // 플레이어 상태
        private Vector3 playerPos;
        private Vector3 playerVel;
        private float playerSpeed;
        private float playerAttackTimer;
        private float playerRetreatTimer;
        private float circleLungeTimer;
        private float circleSign = 1f;
        private float circleRadius = 5f;
        private float dodgeChance;
        private SparringStyle style;
        // 예고를 본 뒤 피하기
        private bool reactPending;
        private float reactTimer;
        private DragonAction reactAction;
        private Vector3 reactMarker;
        private float evadeTimer;
        private Vector3 evadeDir;
        private float evadeSpeedMul;

        private DragonEpisodeSummary summary;

        private void Start()
        {
            if (boss != null && player != null && !EpisodeActive)
                ResetEpisode();
        }

        private void FixedUpdate()
        {
            if (!EpisodeActive || boss == null || player == null) return;
            Tick(Time.fixedDeltaTime);
        }

        #region 외부 API

        /// <summary>다음 행동. 패턴은 쿨타임이 찼을 때만 시작되고, 아니면 '다가가기'로 대체된다.</summary>
        public void SetBossAction(DragonAction action)
        {
            pendingAction = action;
            actionPending = true;
        }

        public bool TryGetSituation(out DragonSituation s)
        {
            s = default;
            if (boss == null || player == null) return false;

            Vector3 to = Flat(playerPos - bossPos);
            float dist = to.magnitude;
            float wantYaw = dist > 0.001f ? Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg : bossYaw;
            Vector3 away = dist > 0.001f ? to / dist : Vector3.forward;
            Vector3 right = new Vector3(Mathf.Cos(bossYaw * Mathf.Deg2Rad), 0f, -Mathf.Sin(bossYaw * Mathf.Deg2Rad));

            s.distance = dist;
            s.angleToTarget = Mathf.DeltaAngle(bossYaw, wantYaw);
            s.targetRadialSpeed = Vector3.Dot(playerVel, away);
            s.targetTangentSpeed = Vector3.Dot(playerVel, right);
            s.hpRatio = boss.HpRatio;
            s.targetHpRatio = player.HpRatio;
            s.biteCooldown = Ratio(cooldowns[0], biteCooldown);
            s.breathCooldown = Ratio(cooldowns[1], breathCooldown);
            s.chargeCooldown = Ratio(cooldowns[2], chargeCooldown);
            s.spitCooldown = Ratio(cooldowns[3], spitCooldown);
            s.selfPos = new Vector2(bossPos.x, bossPos.z) / arenaRadius;
            s.targetPos = new Vector2(playerPos.x, playerPos.z) / arenaRadius;
            s.biteRange = biteRange;
            s.breathRange = breathRange;
            s.chargeMinRange = chargeMinRange;
            s.spitMinRange = spitMinRange;
            s.arenaSize = arenaRadius * 2f;
            s.speedNorm = Mathf.Max(10f, playerMoveSpeed * 2.2f);
            return true;
        }

        /// <summary>새 에피소드: 위치·체력·쿨타임·상대 성향을 무작위로 초기화한다.</summary>
        public void ResetEpisode()
        {
            if (boss == null || player == null) return;

            boss.ResetHp(bossMaxHp);
            player.ResetHp(playerMaxHp);

            bossPos = RandomInnerPoint(0.5f);
            playerPos = RandomInnerPoint(0.9f);
            for (int i = 0; i < 30 && Flat(playerPos - bossPos).magnitude < minSpawnDistance; i++)
                playerPos = RandomInnerPoint(0.9f);

            bossYaw = YawTo(bossPos, playerPos);
            playerVel = Vector3.zero;
            phase = Phase.Free;
            phaseTime = 0f;
            gapTimer = 0f;
            approachTimer = 0f;
            actionPending = false;
            skillLanded = false;
            Array.Clear(cooldowns, 0, cooldowns.Length);
            Array.Clear(landedThisUse, 0, landedThisUse.Length);
            spitShotCount = 0;

            // 상대 성향과 수치를 매번 바꿔 한 가지 패턴에만 과적합되는 것을 막는다
            style = (SparringStyle)UnityEngine.Random.Range(0, 3);
            playerSpeed = playerMoveSpeed * UnityEngine.Random.Range(0.85f, 1.15f);
            dodgeChance = UnityEngine.Random.Range(dodgeChanceRange.x, dodgeChanceRange.y);
            circleSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            circleRadius = UnityEngine.Random.Range(5f, 9f);
            circleLungeTimer = UnityEngine.Random.Range(1.5f, 3.5f);
            playerAttackTimer = UnityEngine.Random.Range(0f, playerAttackCooldown);
            playerRetreatTimer = 0f;
            reactPending = false;
            evadeTimer = 0f;

            summary = new DragonEpisodeSummary();
            EpisodeTime = 0f;
            EpisodeActive = true;
            SyncTransforms();
        }

        #endregion

        #region 틱

        private void Tick(float dt)
        {
            EpisodeTime += dt;

            for (int i = 0; i < cooldowns.Length; i++)
                cooldowns[i] = Mathf.Max(0f, cooldowns[i] - dt);
            playerAttackTimer = Mathf.Max(0f, playerAttackTimer - dt);
            playerRetreatTimer = Mathf.Max(0f, playerRetreatTimer - dt);
            circleLungeTimer -= dt;

            Vector3 playerBefore = playerPos;
            TickBoss(dt);
            if (runPlayerAI) TickPlayer(dt);
            TickSpitImpacts();

            playerVel = dt > 0f ? (playerPos - playerBefore) / dt : Vector3.zero;
            SyncTransforms();

            if (player.IsDead) EndEpisode(EpisodeResult.BossWon);
            else if (boss.IsDead) EndEpisode(EpisodeResult.BossLost);
            else if (EpisodeTime >= maxEpisodeSeconds) EndEpisode(EpisodeResult.TimeOut);
        }

        private void EndEpisode(EpisodeResult result)
        {
            EpisodeActive = false;
            summary.result = result;
            summary.seconds = EpisodeTime;

            DragonTrainingStats.Record(summary);
            if (!string.IsNullOrEmpty(evalLabel)) DragonEvalLog.Append(evalLabel, summary);

            bool hasListener = EpisodeEnded != null;
            EpisodeEnded?.Invoke(result, summary);

            // 에이전트가 없으면(씬 단독 검증) 스스로 다음 에피소드를 시작한다
            if (!hasListener) ResetEpisode();
        }

        #endregion

        #region 드래곤

        private void TickBoss(float dt)
        {
            switch (phase)
            {
                case Phase.Free: TickFree(dt); break;
                case Phase.Bite: TickBite(dt); break;
                case Phase.Breath: TickBreath(dt); break;
                case Phase.Charge: TickCharge(dt); break;
                case Phase.Spit: TickSpit(dt); break;
            }

            boss.Velocity = Vector3.zero;
        }

        private void TickFree(float dt)
        {
            if (gapTimer > 0f)
            {
                gapTimer -= dt;
                Approach(dt);
                return;
            }

            if (actionPending)
            {
                actionPending = false;
                if (TryStartSkill(pendingAction)) return;

                // 쿨타임 중이거나 '다가가기'를 골랐다
                approachTimer = decisionInterval;
            }

            if (approachTimer > 0f)
            {
                approachTimer -= dt;
                Approach(dt);
            }
        }

        // 정지 거리 밖이면 걷고, 안이면 제자리에서 몸만 돈다 (본편 DragonGroundState.Approach)
        private void Approach(float dt)
        {
            FaceToward(playerPos, turnSpeed, dt);
            if (Flat(playerPos - bossPos).magnitude > stopDistance)
            {
                Vector3 step = Dir(bossYaw) * (moveSpeed * dt);
                bossPos = ClampToArena(bossPos + step);
                boss.Velocity = Dir(bossYaw) * moveSpeed;
            }
        }

        private bool TryStartSkill(DragonAction action)
        {
            if (action == DragonAction.Approach) return false;

            int idx = (int)action - 1;
            if (cooldowns[idx] > 0f) return false;

            phase = (Phase)(idx + 1);
            phaseTime = 0f;
            activeSkill = action;
            skillLanded = false;
            summary.uses[idx]++;
            landedThisUse[idx] = false;

            switch (action)
            {
                case DragonAction.Bite:
                    cooldowns[idx] = biteCooldown;
                    ScheduleReaction(DragonAction.Bite, Vector3.zero, UnityEngine.Random.Range(0.2f, 0.4f));
                    break;
                case DragonAction.Breath:
                    cooldowns[idx] = breathCooldown;
                    breathNextTick = breathStartTime;
                    ScheduleReaction(DragonAction.Breath, Vector3.zero, UnityEngine.Random.Range(0.3f, 0.6f));
                    break;
                case DragonAction.Charge:
                    cooldowns[idx] = chargeCooldown;
                    chargeStage = 0;
                    chargeHitDone = false;
                    // 돌진 방향은 aimTime 뒤에 굳는다. 상대는 굳은 뒤에야 방향을 알 수 있으니 반응이 그보다 늦다.
                    ScheduleReaction(DragonAction.Charge, Vector3.zero, aimTime + UnityEngine.Random.Range(0.05f, 0.3f));
                    break;
                case DragonAction.Spit:
                    cooldowns[idx] = spitCooldown;
                    spitReleased = 0;
                    spitShotCount = 0;
                    break;
            }

            return true;
        }

        private void FinishSkill()
        {
            DragonAction finished = activeSkill;
            bool landed = skillLanded;
            phase = Phase.Free;
            phaseTime = 0f;
            gapTimer = skillGap;
            SkillFinished?.Invoke(finished, landed);
        }

        private void TickBite(float dt)
        {
            float before = phaseTime;
            phaseTime += dt;

            if (phaseTime < aimTime) FaceToward(playerPos, aimTurnSpeed, dt);

            if (before < biteHitTime && phaseTime >= biteHitTime)
            {
                Vector3 to = Flat(playerPos - bossPos);
                bool inRange = to.magnitude <= biteRange;
                bool inFan = Mathf.Abs(Mathf.DeltaAngle(bossYaw, YawTo(bossPos, playerPos))) <= biteAngle * 0.5f;
                if (inRange && inFan) DamagePlayer(biteDamage, DragonAction.Bite);
            }

            if (phaseTime >= biteDuration) FinishSkill();
        }

        private void TickBreath(float dt)
        {
            phaseTime += dt;

            if (phaseTime < aimTime)
            {
                FaceToward(playerPos, aimTurnSpeed, dt);
                breathBaseYaw = bossYaw;
            }

            // 뿜는 구간 동안 부채꼴 중심이 좌→우로 쓸린다 (몸통 회전)
            float u = Mathf.InverseLerp(breathStartTime, breathEndTime, phaseTime);
            breathConeYaw = breathBaseYaw + Mathf.Lerp(-breathSweepAngle * 0.5f, breathSweepAngle * 0.5f, u);
            if (phaseTime >= breathStartTime) bossYaw = breathConeYaw;

            while (phaseTime >= breathNextTick && breathNextTick <= breathEndTime)
            {
                breathNextTick += breathTickInterval;

                Vector3 to = Flat(playerPos - bossPos);
                bool inRange = to.magnitude <= breathRange;
                bool inCone = Mathf.Abs(Mathf.DeltaAngle(breathConeYaw, YawTo(bossPos, playerPos))) <= breathHalfAngle;
                if (inRange && inCone) DamagePlayer(breathTickDamage, DragonAction.Breath);
            }

            if (phaseTime >= breathDuration) FinishSkill();
        }

        private void TickCharge(float dt)
        {
            phaseTime += dt;

            switch (chargeStage)
            {
                case 0: // 예고: 잠깐 조준한 뒤 방향이 굳는다
                    if (phaseTime < aimTime) FaceToward(playerPos, aimTurnSpeed, dt);
                    chargeDir = Dir(bossYaw);

                    if (phaseTime >= chargeWarningTime)
                    {
                        chargeStage = 1;
                        float toTarget = Flat(playerPos - bossPos).magnitude;
                        chargeRemain = Mathf.Min(toTarget + chargeOvershoot, chargeMaxDistance);
                    }
                    break;

                case 1: // 돌진
                {
                    float step = Mathf.Min(chargeSpeed * dt, chargeRemain);
                    Vector3 next = ClampToArena(bossPos + chargeDir * step);
                    float moved = Flat(next - bossPos).magnitude;
                    bossPos = next;
                    boss.Velocity = chargeDir * chargeSpeed;
                    chargeRemain -= step;
                    if (moved < step * 0.5f) chargeRemain = 0f;   // 벽에 막힘

                    if (!chargeHitDone && Flat(playerPos - bossPos).magnitude <= chargeWidth * 0.5f + 1f)
                    {
                        chargeHitDone = true;
                        DamagePlayer(chargeDamage, DragonAction.Charge);
                    }

                    if (chargeRemain <= 0f)
                    {
                        chargeStage = 2;
                        chargeRecoverTimer = chargeRecovery;
                    }
                    break;
                }

                default: // 후딜
                    chargeRecoverTimer -= dt;
                    if (chargeRecoverTimer <= 0f) FinishSkill();
                    break;
            }
        }

        private void TickSpit(float dt)
        {
            phaseTime += dt;

            // 다음 발사 직전까지 타깃을 따라 몸을 돌린다
            if (spitReleased < spitCount)
            {
                float nextRelease = spitReleased * spitInterval + spitReleaseTime;
                if (phaseTime < nextRelease - 0.1f) FaceToward(playerPos, aimTurnSpeed, dt);
            }

            while (spitReleased < spitCount && phaseTime >= spitReleased * spitInterval + spitReleaseTime)
            {
                spitReleased++;
                Vector3 marker = Flat(playerPos);   // 발사 순간 타깃 위치가 착탄 지점이 된다
                if (spitShotCount < spitShots.Length)
                    spitShots[spitShotCount++] = new SpitShot { impactPos = marker, impactTime = EpisodeTime + spitFlightTime };

                ScheduleReaction(DragonAction.Spit, marker, UnityEngine.Random.Range(0.2f, 0.4f));
            }

            if (phaseTime >= spitCount * spitInterval) FinishSkill();
        }

        // 빙탄은 패턴이 끝난 뒤에도 날아가는 중일 수 있어 드래곤 상태와 별개로 착탄을 처리한다
        private void TickSpitImpacts()
        {
            for (int i = spitShotCount - 1; i >= 0; i--)
            {
                if (EpisodeTime < spitShots[i].impactTime) continue;

                if (Flat(playerPos - spitShots[i].impactPos).magnitude <= spitImpactRadius)
                {
                    if (phase == Phase.Spit) skillLanded = true;
                    DamagePlayer(spitDamage, DragonAction.Spit);
                }

                spitShots[i] = spitShots[--spitShotCount];
            }
        }

        private void DamagePlayer(float damage, DragonAction source)
        {
            float applied = player.TakeDamage(damage);
            if (applied <= 0f) return;

            int idx = (int)source - 1;
            if (!landedThisUse[idx])
            {
                landedThisUse[idx] = true;
                summary.hits[idx]++;
            }
            summary.damageDealt += applied;
            if (source == activeSkill) skillLanded = true;
            BossHitPlayer?.Invoke(applied, source);
        }

        #endregion

        #region 스파링 플레이어

        // 패턴 예고를 보고 확률적으로 피한다. 반응 시간이 지나면 방향을 정해 짧게 구른다.
        private void ScheduleReaction(DragonAction action, Vector3 marker, float delay)
        {
            if (!runPlayerAI || UnityEngine.Random.value >= dodgeChance) return;

            reactPending = true;
            reactTimer = delay;
            reactAction = action;
            reactMarker = marker;
        }

        private void TickPlayer(float dt)
        {
            Vector3 toBoss = Flat(bossPos - playerPos);
            float dist = toBoss.magnitude;
            Vector3 dir = dist > 0.001f ? toBoss / dist : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, dir) * circleSign;

            if (reactPending)
            {
                reactTimer -= dt;
                if (reactTimer <= 0f)
                {
                    reactPending = false;
                    StartEvade(dir, dist);
                }
            }

            Vector3 move = Vector3.zero;
            float speed = playerSpeed;

            if (evadeTimer > 0f)
            {
                evadeTimer -= dt;
                move = evadeDir;
                speed = playerSpeed * evadeSpeedMul;
            }
            else
            {
                switch (style)
                {
                    case SparringStyle.Aggressive:
                        if (dist > playerAttackRange * 0.9f) move = dir;
                        break;

                    case SparringStyle.HitAndRun:
                        if (playerRetreatTimer > 0f) move = -dir;
                        else if (dist > playerAttackRange * 0.9f) move = dir;
                        break;

                    case SparringStyle.Circling:
                        if (circleLungeTimer > 0f)
                        {
                            float radial = Mathf.Clamp((dist - circleRadius) * 0.5f, -1f, 1f);
                            move = side + dir * radial;
                        }
                        else if (dist > playerAttackRange * 0.9f)
                        {
                            move = dir;
                        }
                        break;
                }
            }

            move = Vector3.ClampMagnitude(move, 1f);
            playerPos = ClampToArena(playerPos + move * speed * dt);

            if (dist <= playerAttackRange && playerAttackTimer <= 0f && evadeTimer <= 0f)
            {
                playerAttackTimer = playerAttackCooldown;
                float applied = boss.TakeDamage(playerDamage);
                if (applied > 0f)
                {
                    summary.damageTaken += applied;
                    PlayerHitBoss?.Invoke(applied);
                }

                if (style == SparringStyle.HitAndRun) playerRetreatTimer = UnityEngine.Random.Range(0.8f, 1.6f);
                if (style == SparringStyle.Circling) circleLungeTimer = UnityEngine.Random.Range(1.5f, 3.5f);
            }
        }

        private void StartEvade(Vector3 towardBoss, float dist)
        {
            Vector3 away = -towardBoss;
            Vector3 sideDir = Vector3.Cross(Vector3.up, towardBoss) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);

            switch (reactAction)
            {
                case DragonAction.Bite:
                    if (dist > biteRange * 1.3f) return;
                    evadeDir = (away + sideDir).normalized;
                    evadeTimer = 0.45f;
                    evadeSpeedMul = 2.2f;
                    break;

                case DragonAction.Breath:
                    evadeDir = (away * 0.7f + sideDir).normalized;
                    evadeTimer = 0.9f;
                    evadeSpeedMul = 1.6f;
                    break;

                case DragonAction.Charge:
                {
                    // 돌진 경로의 옆으로 비킨다
                    Vector3 lane = chargeDir;
                    Vector3 laneSide = Vector3.Cross(Vector3.up, lane);
                    float sign = Vector3.Dot(Flat(playerPos - bossPos), laneSide) >= 0f ? 1f : -1f;
                    evadeDir = (laneSide * sign + lane * 0.2f).normalized;
                    evadeTimer = 0.8f;
                    evadeSpeedMul = 2.2f;
                    break;
                }

                case DragonAction.Spit:
                {
                    Vector3 fromMarker = Flat(playerPos - reactMarker);
                    if (fromMarker.magnitude > spitImpactRadius * 2f) return;   // 이미 멀리 있음
                    evadeDir = fromMarker.sqrMagnitude > 0.0001f ? fromMarker.normalized : sideDir;
                    evadeTimer = 0.6f;
                    evadeSpeedMul = 2f;
                    break;
                }
            }
        }

        #endregion

        #region 유틸

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static Vector3 Dir(float yaw)
        {
            float r = yaw * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
        }

        private static float YawTo(Vector3 from, Vector3 to)
        {
            Vector3 d = Flat(to - from);
            return d.sqrMagnitude < 0.0001f ? 0f : Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        private static float Ratio(float remaining, float total) => total > 0f ? Mathf.Clamp01(remaining / total) : 0f;

        private void FaceToward(Vector3 target, float degPerSec, float dt)
        {
            if (Flat(target - bossPos).sqrMagnitude < 0.0001f) return;
            bossYaw = Mathf.MoveTowardsAngle(bossYaw, YawTo(bossPos, target), degPerSec * dt);
        }

        private Vector3 RandomInnerPoint(float radiusFraction)
        {
            float maxR = Mathf.Max(0.1f, (arenaRadius - edgeMargin) * radiusFraction);
            Vector2 p = UnityEngine.Random.insideUnitCircle * maxR;
            return new Vector3(p.x, 0f, p.y);
        }

        private Vector3 ClampToArena(Vector3 p)
        {
            p.y = 0f;
            float maxR = arenaRadius - edgeMargin;
            return p.magnitude > maxR ? p.normalized * maxR : p;
        }

        private void SyncTransforms()
        {
            boss.transform.localPosition = bossPos;
            boss.transform.localRotation = Quaternion.Euler(0f, bossYaw, 0f);
            player.transform.localPosition = playerPos;
            player.transform.localRotation = Quaternion.Euler(0f, YawTo(playerPos, bossPos), 0f);
            player.Velocity = playerVel;
        }

        // 씬 뷰 / Gizmos를 켠 게임 뷰에서 패턴 예고를 눈으로 확인한다 (스크린샷용)
        private void OnDrawGizmos()
        {
            if (!EpisodeActive || boss == null) return;

            Gizmos.matrix = transform.localToWorldMatrix;
            Vector3 origin = bossPos + Vector3.up * 0.2f;

            switch (phase)
            {
                case Phase.Bite:
                    Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
                    DrawFan(origin, bossYaw, biteAngle * 0.5f, biteRange);
                    break;
                case Phase.Breath:
                    Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
                    DrawFan(origin, breathConeYaw, breathHalfAngle, breathRange);
                    break;
                case Phase.Charge:
                    Gizmos.color = chargeStage == 0 ? new Color(1f, 0.2f, 0.2f, 0.9f) : new Color(1f, 0f, 0f, 1f);
                    Vector3 end = origin + Dir(bossYaw) * Mathf.Min(chargeMaxDistance, 28f);
                    Gizmos.DrawLine(origin, end);
                    break;
            }

            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.9f);
            for (int i = 0; i < spitShotCount; i++)
                Gizmos.DrawWireSphere(spitShots[i].impactPos + Vector3.up * 0.2f, spitImpactRadius);

            Gizmos.color = new Color(1f, 1f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(Vector3.up * 0.1f, arenaRadius);
        }

        private static void DrawFan(Vector3 origin, float centerYaw, float halfAngle, float range)
        {
            Gizmos.DrawLine(origin, origin + Dir(centerYaw - halfAngle) * range);
            Gizmos.DrawLine(origin, origin + Dir(centerYaw + halfAngle) * range);
            Gizmos.DrawLine(origin + Dir(centerYaw - halfAngle) * range, origin + Dir(centerYaw + halfAngle) * range);
        }

        #endregion
    }

    /// <summary>학습 중 전체 아레나의 누적 통계. 학습 화면 HUD가 보여 주고, 발표용 스크린샷에 쓴다.</summary>
    public static class DragonTrainingStats
    {
        public static int Episodes { get; private set; }
        public static int Wins { get; private set; }
        public static int Losses { get; private set; }
        public static int Timeouts { get; private set; }
        public static float TotalSeconds { get; private set; }
        public static float TotalDamageDealt { get; private set; }
        public static float TotalDamageTaken { get; private set; }
        public static readonly int[] Uses = new int[4];
        public static readonly int[] Hits = new int[4];

        // 최근 N판 승률 (학습이 진행되며 오르는 것을 화면에서 바로 본다)
        private const int RecentSize = 50;
        private static readonly bool[] recentWins = new bool[RecentSize];
        private static int recentCount;
        private static int recentIndex;

        public static float RecentWinRate
        {
            get
            {
                if (recentCount == 0) return 0f;
                int w = 0;
                for (int i = 0; i < recentCount; i++) if (recentWins[i]) w++;
                return (float)w / recentCount;
            }
        }

        public static int RecentCount => recentCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Episodes = Wins = Losses = Timeouts = 0;
            TotalSeconds = TotalDamageDealt = TotalDamageTaken = 0f;
            Array.Clear(Uses, 0, Uses.Length);
            Array.Clear(Hits, 0, Hits.Length);
            recentCount = recentIndex = 0;
        }

        public static void Record(DragonEpisodeSummary s)
        {
            Episodes++;
            TotalSeconds += s.seconds;
            TotalDamageDealt += s.damageDealt;
            TotalDamageTaken += s.damageTaken;
            for (int i = 0; i < 4; i++) { Uses[i] += s.uses[i]; Hits[i] += s.hits[i]; }

            bool won = s.result == EpisodeResult.BossWon;
            if (won) Wins++;
            else if (s.result == EpisodeResult.BossLost) Losses++;
            else Timeouts++;

            recentWins[recentIndex] = won;
            recentIndex = (recentIndex + 1) % RecentSize;
            recentCount = Mathf.Min(recentCount + 1, RecentSize);
        }
    }

    /// <summary>평가 에피소드를 CSV로 남긴다 (규칙 기반 vs 학습 모델 비교 표/그래프용). 경로: 프로젝트 폴더/results/dragon_eval.csv</summary>
    public static class DragonEvalLog
    {
        private const string Header = "label,result,seconds,damage_dealt,damage_taken,bite_use,bite_hit,breath_use,breath_hit,charge_use,charge_hit,spit_use,spit_hit";

        public static string FilePath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "results", "dragon_eval.csv"));

        public static void Append(string label, DragonEpisodeSummary s)
        {
            try
            {
                string path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                bool exists = File.Exists(path);

                CultureInfo c = CultureInfo.InvariantCulture;
                string line = string.Join(",",
                    label, s.result.ToString(), s.seconds.ToString("F2", c),
                    s.damageDealt.ToString("F1", c), s.damageTaken.ToString("F1", c),
                    s.uses[0], s.hits[0], s.uses[1], s.hits[1], s.uses[2], s.hits[2], s.uses[3], s.hits[3]);

                using (var writer = new StreamWriter(path, true))
                {
                    if (!exists) writer.WriteLine(Header);
                    writer.WriteLine(line);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[용 학습] 평가 기록을 남기지 못했습니다: {e.Message}");
            }
        }
    }
}
