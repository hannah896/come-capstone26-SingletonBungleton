using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Demon의 운석 세례가 까는 개별 장판. 풀링으로 스폰되어 착탄 지점 바닥에 예고 이펙트를 띄우고,
/// 예고 시간이 끝나면 그 자리에서 폭발해 반경 내 플레이어에게 광역 데미지를 준다.
/// 폭발 이펙트가 잦아들 때까지 기다렸다가 풀로 반환된다.
/// Init() 호출 전까지는 동작하지 않는다.
/// </summary>
public class Meteor : MonoBehaviour, IPoolable
{
    #region Inspector
    [Header("판정")]
    [Tooltip("플레이어를 탐지할 레이어. 비워두면(Everything) Player 컴포넌트로만 필터링")]
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("이펙트 이벤트 이름 (VFX Graph)")]
    [Tooltip("예고(장판) 시작 시 보낼 이벤트. UNI VFX 기준 Onslaught은 buildup, 그 외는 create")]
    [SerializeField] private string warningEvent = "buildup";
    [Tooltip("폭발 시 보낼 이벤트")]
    [SerializeField] private string explodeEvent = "hit";
    [Tooltip("풀 반환 시 보낼 정지 이벤트")]
    [SerializeField] private string stopEvent = "stop";
    #endregion

    // 예고(장판) → 폭발 → 이펙트 잔여 재생 대기 → 회수
    private enum Phase { Idle, Warning, Exploding }

    private GameObject owner;      // 시전한 몬스터 (DamageContext.Instigator)
    private Vector3 impactPos;     // 폭발(지면) 지점
    private int damage;
    private float impactRadius;
    private float warningTimer;    // 예고 남은 시간
    private float lingerTimer;     // 폭발 후 회수까지 남은 시간
    private Phase phase = Phase.Idle;
    private bool loopHooked;
    private VisualEffect[] _vfx;   // 자식으로 붙은 이펙트

    // 광역 판정에서 한 플레이어가 콜라이더 여러 개로 중복 피격되지 않도록 걸러낸다.
    private readonly HashSet<Player> _damaged = new HashSet<Player>();

    private void Awake()
    {
        _vfx = GetComponentsInChildren<VisualEffect>(true);
    }

    /// <summary>장판 파라미터를 주입하고 예고 → 폭발을 시작한다.</summary>
    public void Init(GameObject owner, Vector3 impactPos, int damage, float impactRadius, float warningTime, float lingerTime)
    {
        this.owner = owner;
        this.impactPos = impactPos;
        this.damage = damage;
        this.impactRadius = Mathf.Max(impactRadius, 0f);
        warningTimer = Mathf.Max(warningTime, 0f);
        lingerTimer = Mathf.Max(lingerTime, 0f);

        transform.position = impactPos;
        phase = Phase.Warning;

        // 위치가 확정된 뒤에 이펙트를 초기 상태로 되돌리고 바닥 예고를 띄운다.
        // (풀에서 재사용될 때 이전 장판의 파티클이 남는 것을 방지)
        ResetVfx();
        SendVfxEvent(warningEvent);

        // 예고 시간이 없으면 즉시 터진다.
        if (warningTimer <= 0f)
            Explode();
    }

    #region IPoolable
    public void OnSpawn()   => phase = Phase.Idle; // Init 전까지 대기
    public void OnDespawn()
    {
        phase = Phase.Idle;
        SendVfxEvent(stopEvent);
    }
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
            if (warningTimer <= 0f)
                Explode();
            return;
        }

        // 폭발 이펙트가 다 재생될 때까지 기다렸다가 회수한다.
        lingerTimer -= deltaTime;
        if (lingerTimer <= 0f)
            Finish();
    }

    /// <summary>제자리에서 폭발: 반경 내 모든 플레이어에게 1회씩 광역 데미지를 준다.</summary>
    private void Explode()
    {
        phase = Phase.Exploding;
        SendVfxEvent(explodeEvent);
        ApplyAreaDamage();

        // 잔여 재생 시간이 없으면 바로 회수한다.
        if (lingerTimer <= 0f)
            Finish();
    }

    private void ApplyAreaDamage()
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
    }

    private void Finish()
    {
        if (phase == Phase.Idle) return;
        phase = Phase.Idle;
        Extensions.Despawn(gameObject);
    }

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
}
