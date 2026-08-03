using UnityEngine;
using UnityEngine.VFX;

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

    [Header("이펙트 이벤트 이름 (VFX Graph)")]
    [Tooltip("발사 시 보낼 생성 이벤트")]
    [SerializeField] private string spawnEvent = "create";
    [Tooltip("비행 중 지속 이벤트. 필요 없으면 비워둔다")]
    [SerializeField] private string loopEvent = "loop";
    [Tooltip("명중/소멸 시 보낼 이벤트")]
    [SerializeField] private string hitEvent = "hit";
    [Tooltip("풀 반환 시 보낼 정지 이벤트")]
    [SerializeField] private string stopEvent = "stop";
    [Tooltip("명중 후 이펙트가 다 재생될 때까지 기다리는 시간(초)")]
    [SerializeField] private float hitLingerTime = 1f;
    #endregion

    private GameObject owner;      // 발사한 몬스터 (DamageContext.Instigator)
    private Vector3 direction;
    private float speed;
    private int damage;
    private float lifeTimer;
    private float lingerTimer;     // 명중 후 회수까지 남은 시간
    private bool active;
    private bool hitting;          // 명중 이펙트 재생 중 (더 이상 비행/판정하지 않는다)
    private bool loopHooked;
    private VisualEffect[] _vfx;   // 자식으로 붙은 이펙트

    private void Awake()
    {
        _vfx = GetComponentsInChildren<VisualEffect>(true);
    }

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
        hitting = false;
        lingerTimer = 0f;

        // 위치/방향이 확정된 뒤에 이펙트를 초기 상태로 되돌리고 재생을 시작한다.
        // (풀에서 재사용될 때 이전 발사분의 파티클이 남는 것을 방지)
        ResetVfx();
        SendVfxEvent(spawnEvent);
        SendVfxEvent(loopEvent);
    }

    #region IPoolable
    public void OnSpawn()   => active = false; // Init 전까지 대기
    public void OnDespawn()
    {
        active = false;
        hitting = false;
        SendVfxEvent(stopEvent);
    }
    #endregion

    /// <summary>이펙트를 초기 상태로 되돌린다.</summary>
    private void ResetVfx()
    {
        if (_vfx == null) return;
        foreach (var vfx in _vfx)
        {
            if (vfx == null) continue;
            vfx.Reinit();
        }
    }

    /// <summary>
    /// 자식 이펙트에 VFX Graph 이벤트를 보낸다.
    /// UNI VFX는 초기 이벤트가 비어 있어 이 호출 없이는 아무것도 재생되지 않는다.
    /// </summary>
    private void SendVfxEvent(string eventName)
    {
        if (_vfx == null || string.IsNullOrEmpty(eventName)) return;
        foreach (var vfx in _vfx)
        {
            if (vfx == null) continue;
            vfx.SendEvent(eventName);
        }
    }

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

        // 명중 이펙트가 다 재생될 때까지 기다렸다가 회수한다.
        if (hitting)
        {
            lingerTimer -= deltaTime;
            if (lingerTimer <= 0f)
                Finish();
            return;
        }

        lifeTimer -= deltaTime;
        if (lifeTimer <= 0f)
        {
            BeginHit(); // 미명중 소멸
            return;
        }

        transform.position += direction * (speed * deltaTime);

        TryHitPlayer();
    }

    /// <summary>비행을 멈추고 명중 이펙트를 재생한다. 재생이 끝나면 회수된다.</summary>
    private void BeginHit()
    {
        if (hitting) return;
        hitting = true;
        lingerTimer = Mathf.Max(hitLingerTime, 0f);
        SendVfxEvent(hitEvent);

        if (lingerTimer <= 0f)
            Finish();
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

            BeginHit();
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
