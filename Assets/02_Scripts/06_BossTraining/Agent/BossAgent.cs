using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace SingletonBungleton.BossTraining
{
    /// <summary>
    /// 강화학습으로 학습시키는 보스 에이전트. (ML-Agents 패키지가 설치돼 있을 때만 컴파일된다 — asmdef의 BOSS_MLAGENTS 조건)
    ///
    /// 관측(15): 상대 위치/거리/속도, 내 속도, 양쪽 체력, 쿨다운, 대시 중 여부, 아레나 안 내 위치, 정면 각도
    /// 행동: 연속 2 (좌우, 앞뒤 이동) + 이산 1 분기 3종 (대기 / 근접 / 대시)
    /// 보상: 적중 +, 피격 -, 적정 거리 유지 소량, 시간 소량 페널티, 승리 +1 / 패배 -1
    ///
    /// 행동의 "의도"만 정하고, 공격이 실제로 맞는지는 BossTrainingArena의 사거리·각도·쿨다운 규칙이 판정한다.
    /// </summary>
    [RequireComponent(typeof(TrainingCombatant))]
    public class BossAgent : Agent
    {
        public const int ObservationSize = 15;
        public const string BehaviorNameDefault = "BossAgent";

        [SerializeField] private BossTrainingArena arena;

        [Header("보상")]
        [SerializeField] private float hitReward = 0.15f;
        [SerializeField] private float damagedPenalty = 0.12f;
        [SerializeField] private float winReward = 1f;
        [SerializeField] private float loseReward = -1f;
        [Tooltip("가장 높은 거리 보상을 주는 적정 사거리(m). 비우면(0) 근접 사거리의 80%를 쓴다.")]
        [SerializeField] private float preferredRange;
        [SerializeField] private float rangeRewardPerStep = 0.002f;
        [SerializeField] private float timePenaltyPerStep = 0.0005f;

        private TrainingCombatant self;

        public override void Initialize()
        {
            self = GetComponent<TrainingCombatant>();
            if (arena == null) arena = GetComponentInParent<BossTrainingArena>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (arena == null) arena = GetComponentInParent<BossTrainingArena>();
            if (arena == null) return;

            arena.BossHitPlayer += OnBossHitPlayer;
            arena.PlayerHitBoss += OnPlayerHitBoss;
            arena.EpisodeEnded += OnArenaEpisodeEnded;
        }

        protected override void OnDisable()
        {
            if (arena != null)
            {
                arena.BossHitPlayer -= OnBossHitPlayer;
                arena.PlayerHitBoss -= OnPlayerHitBoss;
                arena.EpisodeEnded -= OnArenaEpisodeEnded;
            }

            base.OnDisable();
        }

        public override void OnEpisodeBegin()
        {
            arena.ResetEpisode();
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            TrainingCombatant opponent = arena.Player;
            Vector3 toPlayerWorld = opponent.transform.position - transform.position;
            toPlayerWorld.y = 0f;
            Vector3 toPlayer = transform.InverseTransformDirection(toPlayerWorld);

            float maxDist = Mathf.Max(arena.HalfExtents.x, arena.HalfExtents.y) * 2f;
            float distance = toPlayerWorld.magnitude;

            // 보스 기준 상대 위치 (보스가 어느 쪽을 보든 같은 의미가 되도록 로컬 좌표) + 거리
            sensor.AddObservation(toPlayer.x / maxDist);
            sensor.AddObservation(toPlayer.z / maxDist);
            sensor.AddObservation(distance / maxDist);

            // 속도 (상대 / 내 것) — 로컬 기준
            Vector3 oppVel = transform.InverseTransformDirection(opponent.Velocity);
            Vector3 selfVel = transform.InverseTransformDirection(self.Velocity);
            float speedNorm = Mathf.Max(1f, arena.BossMoveSpeed, arena.PlayerMoveSpeed * 2.2f);
            sensor.AddObservation(oppVel.x / speedNorm);
            sensor.AddObservation(oppVel.z / speedNorm);
            sensor.AddObservation(selfVel.x / speedNorm);
            sensor.AddObservation(selfVel.z / speedNorm);

            // 체력 / 쿨다운 / 대시 중
            sensor.AddObservation(self.HpRatio);
            sensor.AddObservation(opponent.HpRatio);
            sensor.AddObservation(arena.MeleeCooldownRatio);
            sensor.AddObservation(arena.DashCooldownRatio);
            sensor.AddObservation(arena.BossIsDashing ? 1f : 0f);

            // 아레나 안에서 내가 어디쯤인지 (벽에 몰렸는지 판단)
            Vector3 local = arena.transform.InverseTransformPoint(transform.position);
            sensor.AddObservation(local.x / arena.HalfExtents.x);
            sensor.AddObservation(local.z / arena.HalfExtents.y);

            // 정면에서 상대까지의 부호 있는 각도
            sensor.AddObservation(Vector3.SignedAngle(transform.forward, toPlayerWorld, Vector3.up) / 180f);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            var move = new Vector2(actions.ContinuousActions[0], actions.ContinuousActions[1]);
            var command = (BossCommand)actions.DiscreteActions[0];
            arena.SetBossInput(move, command);

            // 적정 사거리 근처일수록 소량 보상 (가까울수록 무조건 높게 주지 않는다)
            float range = preferredRange > 0f ? preferredRange : arena.MeleeRange * 0.8f;
            float distance = Vector3.Distance(transform.position, arena.Player.transform.position);
            float maxDist = Mathf.Max(arena.HalfExtents.x, arena.HalfExtents.y) * 2f;
            float closeness = 1f - Mathf.Clamp01(Mathf.Abs(distance - range) / maxDist);
            AddReward(rangeRewardPerStep * closeness);
            AddReward(-timePenaltyPerStep);
        }

        // 학습 없이 환경만 확인할 때(Behavior Type = Heuristic Only) 쓰는 단순 규칙: 다가가서 가까우면 근접, 멀면 대시.
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var continuous = actionsOut.ContinuousActions;
            var discrete = actionsOut.DiscreteActions;

            Vector3 toPlayer = arena.Player.transform.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;

            continuous[0] = 0f;
            continuous[1] = distance > arena.MeleeRange * 0.7f ? 1f : 0f;
            discrete[0] = distance <= arena.MeleeRange ? (int)BossCommand.Melee
                        : distance > 7f ? (int)BossCommand.Dash
                        : (int)BossCommand.Wait;
        }

        #region 아레나 이벤트 → 보상

        private void OnBossHitPlayer(float damage) => AddReward(hitReward);

        private void OnPlayerHitBoss(float damage) => AddReward(-damagedPenalty);

        private void OnArenaEpisodeEnded(EpisodeResult result)
        {
            switch (result)
            {
                case EpisodeResult.BossWon:
                    AddReward(winReward);
                    EndEpisode();
                    break;
                case EpisodeResult.BossLost:
                    AddReward(loseReward);
                    EndEpisode();
                    break;
                default:
                    // 시간 초과는 패배가 아니라 "중단"으로 알려 가치 추정이 틀어지지 않게 한다
                    EpisodeInterrupted();
                    break;
            }
        }

        #endregion
    }
}

