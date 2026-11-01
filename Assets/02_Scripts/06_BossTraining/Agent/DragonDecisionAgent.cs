using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace SingletonBungleton.BossTraining
{
    /// <summary>
    /// 드래곤의 "판단층" 에이전트. (ML-Agents 패키지가 설치돼 있을 때만 컴파일된다 — asmdef의 BOSS_MLAGENTS 조건)
    ///
    /// 한 가지 클래스를 두 곳에서 쓴다.
    ///  1) 학습: DragonTrainingArena 안에서 PPO로 학습한다. 아레나가 패턴이 끝나 새 판단이 필요할 때마다 결정을 요청한다.
    ///  2) 본편: 최종 보스 프리팹에 붙어 학습된 모델(.onnx)로 추론한다. DragonAiGroundState가 <see cref="IDragonBrain"/>으로 판단을 요청한다.
    ///
    /// 관측(19): 거리(아레나 대비 + 패턴 사거리 대비 4개), 타깃 방향 sin/cos, 타깃 상대 속도 2, 양쪽 체력, 쿨타임 4, 양쪽 아레나 내 위치 4
    /// 행동: 이산 1 분기 5종 (다가가기 / 물기 / 브레스 / 돌진 / 빙탄). 쿨타임 중인 패턴은 마스크로 막는다.
    /// 보상: 플레이어에게 준 피해(비율) +, 받은 피해 -, 헛친 패턴 소량 -, 시간 소량 -, 승리 +1 / 패배 -1
    ///
    /// 패턴의 실행(애니메이션, 판정)은 이 클래스가 아니라 본편 FSM(또는 아레나 시뮬레이션)이 맡는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class DragonDecisionAgent : Agent, IDragonBrain
    {
        [Header("학습 아레나 (본편 프리팹에서는 비워 둔다)")]
        [SerializeField] private DragonTrainingArena arena;

        [Header("보상")]
        [Tooltip("플레이어 최대 체력 대비 준 피해 비율에 곱한다. 플레이어를 쓰러뜨리면 합쳐서 약 1이 된다.")]
        [SerializeField] private float damageDealtScale = 1f;
        [Tooltip("드래곤 최대 체력 대비 받은 피해 비율에 곱한다.")]
        [SerializeField] private float damageTakenScale = 0.5f;
        [SerializeField] private float whiffPenalty = 0.03f;
        [SerializeField] private float timePenaltyPerDecision = 0.003f;
        [SerializeField] private float winReward = 1f;
        [SerializeField] private float loseReward = -1f;

        [Header("평가 실행 (파이썬 트레이너 없이 Play할 때)")]
        [Tooltip("평가용 실행의 배속. 학습 중에는 mlagents-learn의 --time-scale이 우선한다.")]
        [SerializeField] private float standaloneTimeScale = 20f;
        [Tooltip("평가용 실행에서 이 판 수가 쌓이면 에디터 Play를 자동으로 멈춘다. 0이면 멈추지 않는다.")]
        [SerializeField] private int standaloneStopAfterEpisodes = 300;

        private bool isTraining;
        private IDragonSituationSource source;
        private DragonSituation situation;
        private bool hasSituation;
        private readonly float[] obs = new float[DragonObs.Size];

        private bool decisionRequested;
        private bool hasDecision;
        private DragonAction decision;

        private static readonly string[] SkillStatNames = { "Bite", "Breath", "Charge", "Spit" };

        #region IDragonBrain (본편)

        public bool IsReady
        {
            get
            {
                if (!isActiveAndEnabled || source == null) return false;

                var behavior = GetComponent<BehaviorParameters>();
                return behavior != null && behavior.Model != null && behavior.BehaviorType != BehaviorType.HeuristicOnly;
            }
        }

        void IDragonBrain.Request()
        {
            if (decisionRequested) return;

            decisionRequested = true;
            hasDecision = false;
            RequestDecision();
        }

        public bool TryTakeDecision(out DragonAction action)
        {
            action = decision;
            if (!hasDecision) return false;

            hasDecision = false;
            decisionRequested = false;
            return true;
        }

        #endregion

        public override void Initialize()
        {
            if (arena == null) arena = GetComponentInParent<DragonTrainingArena>();
            source = arena != null ? arena : GetComponent<IDragonSituationSource>();

            if (arena == null) return;

            // 파이썬 트레이너가 붙지 않은 실행(= 평가)이면 배속을 올리고, 규칙 기반/학습 모델 실행을 CSV에 라벨을 달아 남긴다
            isTraining = Academy.IsInitialized && Academy.Instance.IsCommunicatorOn;
            if (!isTraining)
            {
                Time.timeScale = standaloneTimeScale;

                var behavior = GetComponent<BehaviorParameters>();
                if (string.IsNullOrEmpty(arena.EvalLabel) && behavior != null)
                {
                    bool useModel = behavior.BehaviorType == BehaviorType.InferenceOnly
                                    || (behavior.BehaviorType == BehaviorType.Default && behavior.Model != null);
                    arena.EvalLabel = useModel ? "trained" : "rule";
                }
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (arena == null) arena = GetComponentInParent<DragonTrainingArena>();
            if (arena == null) return;

            arena.BossHitPlayer += OnBossHitPlayer;
            arena.PlayerHitBoss += OnPlayerHitBoss;
            arena.SkillFinished += OnSkillFinished;
            arena.EpisodeEnded += OnArenaEpisodeEnded;
        }

        protected override void OnDisable()
        {
            if (arena != null)
            {
                arena.BossHitPlayer -= OnBossHitPlayer;
                arena.PlayerHitBoss -= OnPlayerHitBoss;
                arena.SkillFinished -= OnSkillFinished;
                arena.EpisodeEnded -= OnArenaEpisodeEnded;
            }

            base.OnDisable();
        }

        // 학습 아레나에서는 드래곤이 새 판단을 받을 수 있을 때만 결정을 요청한다 (패턴 도중에는 요청하지 않는다)
        private void FixedUpdate()
        {
            if (arena == null || !arena.CanDecide || decisionRequested) return;

            decisionRequested = true;
            RequestDecision();
        }

        public override void OnEpisodeBegin()
        {
            decisionRequested = false;
            hasDecision = false;
            if (arena != null) arena.ResetEpisode();
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            hasSituation = source != null && source.TryGetSituation(out situation);
            if (hasSituation) DragonObs.Fill(situation, obs);
            else System.Array.Clear(obs, 0, obs.Length);

            sensor.AddObservation(obs);
        }

        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
        {
            if (!hasSituation) return;

            // 쿨타임 중인 패턴은 고르지 못하게 한다 (다가가기는 항상 가능)
            for (int a = 1; a < DragonObs.ActionCount; a++)
                if (!DragonObs.IsReady(situation, (DragonAction)a))
                    actionMask.SetActionEnabled(0, a, false);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            decision = (DragonAction)actions.DiscreteActions[0];
            hasDecision = true;

            if (arena != null)
            {
                arena.SetBossAction(decision);
                decisionRequested = false;
                AddReward(-timePenaltyPerDecision);
            }
        }

        // 학습 없이 규칙 기반 선택(기존 DragonGroundState와 같은 우선순위)으로 돌려 비교 기준(baseline)을 만든다.
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var discrete = actionsOut.DiscreteActions;
            discrete[0] = hasSituation ? (int)DragonObs.RuleBased(situation) : 0;
        }

        #region 아레나 이벤트 → 보상

        private void OnBossHitPlayer(float damage, DragonAction source)
            => AddReward(damageDealtScale * damage / Mathf.Max(1f, arena.PlayerMaxHp));

        private void OnPlayerHitBoss(float damage)
            => AddReward(-damageTakenScale * damage / Mathf.Max(1f, arena.BossMaxHp));

        private void OnSkillFinished(DragonAction skill, bool landed)
        {
            if (!landed) AddReward(-whiffPenalty);
        }

        private void OnArenaEpisodeEnded(EpisodeResult result, DragonEpisodeSummary summary)
        {
            // TensorBoard에 찍히는 사용자 통계 (학습 곡선과 함께 발표 자료로 쓴다)
            var stats = Academy.Instance.StatsRecorder;
            stats.Add("Dragon/EpisodeSeconds", summary.seconds);
            stats.Add("Dragon/DamageDealt", summary.damageDealt);
            stats.Add("Dragon/DamageTaken", summary.damageTaken);
            stats.Add("Dragon/PlayerKilled", result == EpisodeResult.BossWon ? 1f : 0f);
            for (int i = 0; i < SkillStatNames.Length; i++)
            {
                stats.Add($"Dragon/{SkillStatNames[i]}Uses", summary.uses[i]);
                if (summary.uses[i] > 0)
                    stats.Add($"Dragon/{SkillStatNames[i]}HitRate", (float)summary.hits[i] / summary.uses[i]);
            }

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

#if UNITY_EDITOR
            if (!isTraining && standaloneStopAfterEpisodes > 0 && DragonTrainingStats.Episodes >= standaloneStopAfterEpisodes)
            {
                Debug.Log($"[용 학습] 평가 {DragonTrainingStats.Episodes}판을 채워 Play를 멈춥니다. 결과: results/dragon_eval.csv");
                UnityEditor.EditorApplication.isPlaying = false;
            }
#endif
        }

        #endregion
    }
}
