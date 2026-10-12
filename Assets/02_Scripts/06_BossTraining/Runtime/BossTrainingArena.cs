using System;
using UnityEngine;

namespace SingletonBungleton.BossTraining
{
    /// <summary>보스가 한 스텝에 고를 수 있는 전투 행동.</summary>
    public enum BossCommand
    {
        Wait = 0,
        Melee = 1,
        Dash = 2,
    }

    /// <summary>스파링 상대(규칙 기반 플레이어)의 행동 성향. 에피소드마다 바뀌어 한 가지 패턴에만 과적합되는 것을 막는다.</summary>
    public enum SparringStyle
    {
        Aggressive,   // 곧장 달려들어 공격
        HitAndRun,    // 한 번 치고 물러남
        Circling,     // 보스 주위를 돌다가 틈을 봐서 파고듦
    }

    public enum EpisodeResult
    {
        BossWon,
        BossLost,
        TimeOut,
    }

    /// <summary>
    /// 보스 강화학습용 1:1 아레나 한 칸. 보스와 규칙 기반 스파링 플레이어, 체력/판정 규칙, 에피소드 시작/종료를 담당한다.
    ///
    /// - 물리 엔진 없이 아레나 로컬 좌표를 직접 갱신하는 결정론적 이동/판정이다 (학습이 빠르고 재현 가능).
    /// - 보스의 "무엇을 할지"만 외부(BossAgent)가 정한다: <see cref="SetBossInput"/>으로 이동 입력과 행동을 넘긴다.
    ///   공격이 실제로 맞는지는 이 클래스의 사거리·각도·쿨다운 규칙이 판정한다.
    /// - ML-Agents 패키지가 없어도 컴파일되고, 에이전트 없이도 상대 AI와 규칙 검증이 돌아간다.
    /// </summary>
    public class BossTrainingArena : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private TrainingCombatant boss;
        [SerializeField] private TrainingCombatant player;

        [Header("아레나")]
        [Tooltip("아레나 로컬 기준 바닥 절반 크기(X, Z). 벽 안쪽 경계로 쓴다.")]
        [SerializeField] private Vector2 halfExtents = new(11f, 11f);
        [SerializeField] private float edgeMargin = 1.2f;
        [Tooltip("시작 시 보스와 플레이어 사이 최소 거리")]
        [SerializeField] private float minSpawnDistance = 6f;
        [Tooltip("에피소드 최대 시간(초). 넘기면 무승부로 끝난다.")]
        [SerializeField] private float maxEpisodeSeconds = 60f;

        [Header("보스")]
        [SerializeField] private float bossMaxHp = 300f;
        [SerializeField] private float bossMoveSpeed = 4f;
        [SerializeField] private float bossTurnSpeed = 360f;
        [SerializeField] private float meleeRange = 3f;
        [Tooltip("근접 공격이 맞는 정면 부채꼴의 절반 각도(도)")]
        [SerializeField] private float meleeHalfAngle = 60f;
        [SerializeField] private float meleeDamage = 15f;
        [SerializeField] private float meleeCooldown = 1.2f;
        [SerializeField] private float dashSpeed = 14f;
        [SerializeField] private float dashDuration = 0.25f;
        [SerializeField] private float dashHitRadius = 1.6f;
        [SerializeField] private float dashDamage = 25f;
        [SerializeField] private float dashCooldown = 4f;

        [Header("스파링 플레이어")]
        [SerializeField] private bool runPlayerAI = true;
        [SerializeField] private float playerMaxHp = 150f;
        [SerializeField] private float playerMoveSpeed = 5f;
        [SerializeField] private float playerAttackRange = 2.2f;
        [SerializeField] private float playerDamage = 10f;
        [SerializeField] private float playerAttackCooldown = 0.8f;

        // ── 외부에 알리는 이벤트 (BossAgent가 보상 계산에 사용) ──
        /// <summary>보스가 플레이어를 맞췄다 (실제 데미지)</summary>
        public event Action<float> BossHitPlayer;
        /// <summary>플레이어가 보스를 맞췄다 (실제 데미지)</summary>
        public event Action<float> PlayerHitBoss;
        /// <summary>에피소드가 끝났다</summary>
        public event Action<EpisodeResult> EpisodeEnded;

        public TrainingCombatant Boss => boss;
        public TrainingCombatant Player => player;
        public Vector2 HalfExtents => halfExtents;
        public float MaxEpisodeSeconds => maxEpisodeSeconds;
        public float MeleeRange => meleeRange;
        public float BossMoveSpeed => bossMoveSpeed;
        public float PlayerMoveSpeed => currentPlayerSpeed;
        public bool EpisodeActive { get; private set; }
        public float EpisodeTime { get; private set; }
        public SparringStyle CurrentStyle { get; private set; }
        public bool BossIsDashing => dashRemaining > 0f;
        public float MeleeCooldownRatio => meleeCooldown > 0f ? Mathf.Clamp01(meleeTimer / meleeCooldown) : 0f;
        public float DashCooldownRatio => dashCooldown > 0f ? Mathf.Clamp01(dashTimer / dashCooldown) : 0f;

        // 보스 입력 (한 스텝에 에이전트가 정한 값)
        private Vector2 bossMove;
        private BossCommand pendingCommand;

        // 보스 상태
        private float meleeTimer;
        private float dashTimer;
        private float dashRemaining;
        private bool dashHitDone;

        // 플레이어 상태
        private float currentPlayerSpeed;
        private float playerAttackTimer;
        private float playerRetreatTimer;
        private float playerDodgeTimer;
        private Vector3 playerDodgeDir;
        private float dodgeChance;
        private float circleSign = 1f;
        private float circleRadius = 4.5f;
        private float circleLungeTimer;

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

        /// <summary>
        /// 보스의 이번 스텝 입력. move.x = 좌우(오른쪽 +), move.y = 앞뒤(앞 +), 각각 -1~1.
        /// 공격 행동은 쿨다운이 찼을 때만 실제로 나가고, 한 번 소비되면 사라진다.
        /// </summary>
        public void SetBossInput(Vector2 move, BossCommand command)
        {
            bossMove = Vector2.ClampMagnitude(move, 1f);
            if (command != BossCommand.Wait)
                pendingCommand = command;
        }

        /// <summary>새 에피소드: 위치·체력·쿨다운·상대 성향을 무작위로 초기화한다.</summary>
        public void ResetEpisode()
        {
            if (boss == null || player == null) return;

            boss.ResetHp(bossMaxHp);
            player.ResetHp(playerMaxHp);

            Vector3 bossPos = RandomInnerPoint();
            Vector3 playerPos = RandomInnerPoint();
            for (int i = 0; i < 30 && (playerPos - bossPos).magnitude < minSpawnDistance; i++)
                playerPos = RandomInnerPoint();

            boss.transform.localPosition = bossPos;
            player.transform.localPosition = playerPos;
            FaceToward(boss.transform, playerPos, 360f, 1f);
            FaceToward(player.transform, bossPos, 360f, 1f);

            bossMove = Vector2.zero;
            pendingCommand = BossCommand.Wait;
            meleeTimer = 0f;
            dashTimer = 0f;
            dashRemaining = 0f;
            dashHitDone = false;

            // 상대 성향과 수치를 매번 조금씩 바꿔 과적합을 막는다
            CurrentStyle = (SparringStyle)UnityEngine.Random.Range(0, 3);
            currentPlayerSpeed = playerMoveSpeed * UnityEngine.Random.Range(0.8f, 1.2f);
            dodgeChance = UnityEngine.Random.Range(0f, 0.6f);
            circleSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            circleRadius = UnityEngine.Random.Range(3.5f, 6f);
            circleLungeTimer = UnityEngine.Random.Range(1.5f, 3.5f);
            playerAttackTimer = UnityEngine.Random.Range(0f, playerAttackCooldown);
            playerRetreatTimer = 0f;
            playerDodgeTimer = 0f;

            EpisodeTime = 0f;
            EpisodeActive = true;
        }

        #endregion

        #region 틱

        private void Tick(float dt)
        {
            EpisodeTime += dt;

            meleeTimer = Mathf.Max(0f, meleeTimer - dt);
            dashTimer = Mathf.Max(0f, dashTimer - dt);
            playerAttackTimer = Mathf.Max(0f, playerAttackTimer - dt);
            playerRetreatTimer = Mathf.Max(0f, playerRetreatTimer - dt);
            playerDodgeTimer = Mathf.Max(0f, playerDodgeTimer - dt);
            circleLungeTimer -= dt;

            TickBoss(dt);
            if (runPlayerAI) TickPlayer(dt);

            if (player.IsDead) EndEpisode(EpisodeResult.BossWon);
            else if (boss.IsDead) EndEpisode(EpisodeResult.BossLost);
            else if (EpisodeTime >= maxEpisodeSeconds) EndEpisode(EpisodeResult.TimeOut);
        }

        private void EndEpisode(EpisodeResult result)
        {
            EpisodeActive = false;
            bool hasListener = EpisodeEnded != null;
            EpisodeEnded?.Invoke(result);

            // 에이전트가 없으면(씬 단독 검증) 스스로 다음 에피소드를 시작한다
            if (!hasListener) ResetEpisode();
        }

        #endregion

        #region 보스

        private void TickBoss(float dt)
        {
            Transform t = boss.transform;
            Vector3 toPlayer = Flat(player.transform.localPosition - t.localPosition);

            // 보스는 항상 플레이어를 향해 일정 속도로 회전한다 (이동 방향 선택에 집중하게 하려는 규칙)
            FaceToward(t, player.transform.localPosition, bossTurnSpeed, dt);

            // 행동 요청 처리
            if (pendingCommand == BossCommand.Melee && meleeTimer <= 0f && !BossIsDashing)
            {
                meleeTimer = meleeCooldown;
                if (toPlayer.magnitude <= meleeRange && Vector3.Angle(t.forward, toPlayer) <= meleeHalfAngle)
                    BossHits(meleeDamage);
            }
            else if (pendingCommand == BossCommand.Dash && dashTimer <= 0f && !BossIsDashing)
            {
                dashTimer = dashCooldown;
                dashRemaining = dashDuration;
                dashHitDone = false;

                // 대시가 시작되는 순간, 상대가 가끔 옆으로 피한다
                if (runPlayerAI && UnityEngine.Random.value < dodgeChance)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, t.forward) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
                    playerDodgeDir = side.normalized;
                    playerDodgeTimer = 0.3f;
                }
            }

            pendingCommand = BossCommand.Wait;

            // 이동
            Vector3 velocity;
            bool dashingThisTick = BossIsDashing;
            if (dashingThisTick)
            {
                velocity = t.forward * dashSpeed;
                dashRemaining -= dt;
            }
            else
            {
                velocity = (t.forward * bossMove.y + t.right * bossMove.x) * bossMoveSpeed;
            }

            MoveWithin(t, velocity * dt);
            boss.Velocity = velocity;

            // 대시는 지나가며 닿은 순간 한 번만 맞는다
            if (dashingThisTick && !dashHitDone)
            {
                float dist = Flat(player.transform.localPosition - t.localPosition).magnitude;
                if (dist <= dashHitRadius)
                {
                    dashHitDone = true;
                    BossHits(dashDamage);
                }
            }
        }

        private void BossHits(float damage)
        {
            float applied = player.TakeDamage(damage);
            if (applied > 0f) BossHitPlayer?.Invoke(applied);
        }

        #endregion

        #region 스파링 플레이어

        private void TickPlayer(float dt)
        {
            Transform t = player.transform;
            Vector3 toBoss = Flat(boss.transform.localPosition - t.localPosition);
            float dist = toBoss.magnitude;
            Vector3 dir = dist > 0.001f ? toBoss / dist : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, dir) * circleSign;

            Vector3 move = Vector3.zero;
            float speed = currentPlayerSpeed;

            if (playerDodgeTimer > 0f)
            {
                move = playerDodgeDir;
                speed = currentPlayerSpeed * 2.2f;
            }
            else
            {
                switch (CurrentStyle)
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
                            // 일정 거리를 유지하며 보스 주위를 돈다
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
            MoveWithin(t, move * speed * dt);
            player.Velocity = move * speed;
            if (dist > 0.001f) FaceToward(t, boss.transform.localPosition, 720f, dt);

            if (dist <= playerAttackRange && playerAttackTimer <= 0f && playerDodgeTimer <= 0f)
            {
                playerAttackTimer = playerAttackCooldown;
                float applied = boss.TakeDamage(playerDamage);
                if (applied > 0f) PlayerHitBoss?.Invoke(applied);

                if (CurrentStyle == SparringStyle.HitAndRun) playerRetreatTimer = UnityEngine.Random.Range(0.8f, 1.6f);
                if (CurrentStyle == SparringStyle.Circling) circleLungeTimer = UnityEngine.Random.Range(1.5f, 3.5f);
            }
        }

        #endregion

        #region 유틸

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private Vector3 RandomInnerPoint()
        {
            float x = Mathf.Max(0.1f, halfExtents.x - edgeMargin - 1f);
            float z = Mathf.Max(0.1f, halfExtents.y - edgeMargin - 1f);
            return new Vector3(UnityEngine.Random.Range(-x, x), 0f, UnityEngine.Random.Range(-z, z));
        }

        // 아레나 안쪽 경계로 이동량을 제한한다
        private void MoveWithin(Transform t, Vector3 delta)
        {
            Vector3 p = t.localPosition + new Vector3(delta.x, 0f, delta.z);
            p.x = Mathf.Clamp(p.x, -halfExtents.x + edgeMargin, halfExtents.x - edgeMargin);
            p.z = Mathf.Clamp(p.z, -halfExtents.y + edgeMargin, halfExtents.y - edgeMargin);
            p.y = 0f;
            t.localPosition = p;
        }

        // 목표 로컬 좌표 쪽으로 Y축 회전만 한다 (turnSpeed: 초당 도)
        private void FaceToward(Transform t, Vector3 targetLocal, float turnSpeed, float dt)
        {
            Vector3 d = Flat(targetLocal - t.localPosition);
            if (d.sqrMagnitude < 0.0001f) return;

            Quaternion target = Quaternion.LookRotation(transform.TransformDirection(d), Vector3.up);
            t.rotation = Quaternion.RotateTowards(t.rotation, target, turnSpeed * dt);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.up * 0.1f, new Vector3(halfExtents.x * 2f, 0.2f, halfExtents.y * 2f));
        }

        #endregion
    }
}
