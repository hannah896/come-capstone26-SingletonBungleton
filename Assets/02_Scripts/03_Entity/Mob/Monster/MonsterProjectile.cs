using UnityEngine;

/// <summary>
/// 몬스터가 발사하는 직선 투사체. 풀링으로 스폰되어 Player에 닿으면 데미지를 주고 풀로 반환된다.
/// Init() 호출 전까지는 비행하지 않는다.
/// </summary>
public class MonsterProjectile : MonoBehaviour, IPoolable
{
    #region Inspector
    [Header("판정")]
    [Tooltip("히트 판정 반경")]
    [SerializeField] private float hitRadius = 0.3f;
    [Tooltip("플레이어를 탐지할 레이어. 비워두면(Everything) Player 컴포넌트로만 필터링")]
    [SerializeField] private LayerMask hitMask = ~0;
    #endregion

    private GameObject owner;      // 발사한 몬스터 (DamageContext.Instigator)
    private Vector3 direction;
    private float speed;
    private int damage;
    private float lifeTimer;
    private bool active;
    private bool loopHooked;

    /// <summary>발사 파라미터를 주입하고 비행을 시작한다.</summary>
    public void Init(GameObject owner, Vector3 origin, Vector3 direction, float speed, int damage, float lifeTime)
    {
        this.owner = owner;
        this.direction = direction.normalized;
        this.speed = speed;
        this.damage = damage;
        lifeTimer = lifeTime;

        transform.position = origin;
        if (this.direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(this.direction);

        active = true;
    }

    #region IPoolable
    public void OnSpawn()   => active = false; // Init 전까지 대기
    public void OnDespawn() => active = false;
    #endregion

    private void OnEnable()
    {
        if (loopHooked) return;
        Main.Loop.OnGameUpdate += HandleGameUpdate;
        loopHooked = true;
    }

    // 구독 해제는 OnDisable이 아니라 파괴 시점에만 수행한다.
    private void OnDestroy()
    {
        if (!loopHooked) return;
        if (Main.Loop != null)
            Main.Loop.OnGameUpdate -= HandleGameUpdate;
        loopHooked = false;
    }

    private void HandleGameUpdate(float deltaTime)
    {
        if (!active || !isActiveAndEnabled) return;

        lifeTimer -= deltaTime;
        if (lifeTimer <= 0f)
        {
            Finish(); // 미명중 소멸
            return;
        }

        transform.position += direction * (speed * deltaTime);

        TryHitPlayer();
    }

    /// <summary>현재 위치 주변의 플레이어에게 명중 판정. 성공 시 데미지 적용 후 회수.</summary>
    private void TryHitPlayer()
    {
        var hits = Physics.OverlapSphere(transform.position, hitRadius, hitMask, QueryTriggerInteraction.Collide);
        foreach (var col in hits)
        {
            var player = col.GetComponentInParent<Player>();
            if (player == null || player.Stat == null || player.Stat.IsDead) continue;

            var ctx = new DamageContext(
                instigator: owner,
                point: transform.position,
                amount: damage,
                toolId: string.Empty);

            if (player.TryGetComponent<IDamageable>(out var dmg) && dmg.CanDamage(ctx))
                dmg.ApplyDamage(ctx);

            Finish();
            return;
        }
    }

    private void Finish()
    {
        if (!active) return;
        active = false;
        Extensions.Despawn(gameObject);
    }
}
