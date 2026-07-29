using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Demon의 운석 세례가 떨어뜨리는 개별 운석. 풀링으로 스폰되어 지정된 착탄 지점 상공에서
/// 잠시 예고(경고) 후 수직 낙하하고, 지면에 닿으면 반경 내 플레이어에게 광역 데미지를 준 뒤 풀로 반환된다.
/// Init() 호출 전까지는 동작하지 않는다.
/// </summary>
public class Meteor : MonoBehaviour, IPoolable
{
    #region Inspector
    [Header("판정")]
    [Tooltip("플레이어를 탐지할 레이어. 비워두면(Everything) Player 컴포넌트로만 필터링")]
    [SerializeField] private LayerMask hitMask = ~0;
    #endregion

    private enum Phase { Idle, Warning, Falling }

    private GameObject owner;      // 시전한 몬스터 (DamageContext.Instigator)
    private Vector3 impactPos;     // 착탄(지면) 지점
    private float fallSpeed;
    private int damage;
    private float impactRadius;
    private float warningTimer;    // 예고 남은 시간
    private Phase phase = Phase.Idle;
    private bool loopHooked;

    // 광역 판정에서 한 플레이어가 콜라이더 여러 개로 중복 피격되지 않도록 걸러낸다.
    private readonly HashSet<Player> _damaged = new HashSet<Player>();

    /// <summary>낙하 파라미터를 주입하고 예고 → 낙하를 시작한다.</summary>
    public void Init(GameObject owner, Vector3 impactPos, float spawnHeight, float fallSpeed, int damage, float impactRadius, float warningTime)
    {
        this.owner = owner;
        this.impactPos = impactPos;
        this.fallSpeed = Mathf.Max(fallSpeed, 0.01f);
        this.damage = damage;
        this.impactRadius = Mathf.Max(impactRadius, 0f);
        warningTimer = Mathf.Max(warningTime, 0f);

        // 착탄 지점 상공에서 시작
        transform.position = impactPos + Vector3.up * Mathf.Max(spawnHeight, 0f);
        phase = warningTimer > 0f ? Phase.Warning : Phase.Falling;
    }

    #region IPoolable
    public void OnSpawn()   => phase = Phase.Idle; // Init 전까지 대기
    public void OnDespawn() => phase = Phase.Idle;
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
        if (phase == Phase.Idle || !isActiveAndEnabled) return;

        if (phase == Phase.Warning)
        {
            warningTimer -= deltaTime;
            if (warningTimer > 0f) return;
            phase = Phase.Falling;
        }

        // 수직 낙하. 지면(impactPos.y)에 닿으면 착탄.
        Vector3 pos = transform.position;
        pos.y -= fallSpeed * deltaTime;
        if (pos.y <= impactPos.y)
        {
            pos.y = impactPos.y;
            transform.position = pos;
            Impact();
            return;
        }

        transform.position = pos;
    }

    /// <summary>착탄: 반경 내 모든 플레이어에게 1회씩 광역 데미지를 주고 회수한다.</summary>
    private void Impact()
    {
        _damaged.Clear();

        var hits = Physics.OverlapSphere(impactPos, impactRadius, hitMask, QueryTriggerInteraction.Collide);
        foreach (var col in hits)
        {
            var player = col.GetComponentInParent<Player>();
            if (player == null || player.Stat == null || player.Stat.IsDead) continue;
            if (!_damaged.Add(player)) continue; // 같은 플레이어 중복 피격 방지

            var ctx = new DamageContext(
                instigator: owner,
                point: impactPos,
                amount: damage,
                toolId: string.Empty);

            if (player.TryGetComponent<IDamageable>(out var dmg) && dmg.CanDamage(ctx))
                dmg.ApplyDamage(ctx);
        }

        Finish();
    }

    private void Finish()
    {
        if (phase == Phase.Idle) return;
        phase = Phase.Idle;
        Extensions.Despawn(gameObject);
    }
}
