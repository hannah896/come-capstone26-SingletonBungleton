using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 드래곤이 쏘는 얼음탄(2페이즈는 산성탄). 입에서 착탄 지점까지 정해진 시간 동안 곧장 날아가
/// 착탄 순간 반경 안 플레이어에게 피해를 주고, 그 자리에 지속 장판(DragonHazardZone)을 남긴다.
/// 착탄 지점 예고 원은 드래곤이 따로 깐다(DragonGroundMarker).
///
/// 멀티: 호스트 원본만 판정·장판 생성을 하고, 클라에는 비행·착탄 연출만 하는 복제본(InitVisual)을 띄운다.
/// (장판은 호스트가 깔 때 스스로 복제된다)
/// </summary>
public class DragonFrostBall : MonoBehaviour, IPoolable
{
    #region Inspector
    [Tooltip("비행 중 파티클/트레일의 부모 (착탄하면 끈다)")]
    [SerializeField] private Transform flightFx;
    [Tooltip("착탄 이펙트 (DragonVfx 프리팹 Addressable 키). 비우면 없음")]
    [SerializeField] private string impactVfxKey = "DragonFrostImpact";
    [SerializeField] private float impactVfxScale = 1.5f;
    [Tooltip("착탄 후 비행 이펙트 잔상이 사라지기까지 기다리는 시간(초)")]
    [SerializeField] private float lingerTime = 0.6f;
    #endregion

    public struct ZoneSettings
    {
        public string Key;
        public float Radius;
        public float Duration;
        public float SlowMultiplier;
        public float DotPerSecond;
    }

    private ParticleSystem[] _systems;
    private TrailRenderer[] _trails;
    private readonly HashSet<Player> _struck = new HashSet<Player>();

    private GameObject owner;
    private DragonBoss ownerBoss;  // 부식 배율용
    private Vector3 origin;
    private Vector3 target;
    private float flightTime;
    private float impactRadius;
    private int damage;
    private ZoneSettings zone;
    private float timer;
    private bool flying;
    private bool lingering;
    private bool visualOnly;
    private bool loopHooked;

    private void Awake()
    {
        Transform root = flightFx != null ? flightFx : transform;
        _systems = root.GetComponentsInChildren<ParticleSystem>(true);
        _trails = root.GetComponentsInChildren<TrailRenderer>(true);
        foreach (var ps in _systems)
        {
            var main = ps.main;
            main.playOnAwake = false;
        }
        SetFlightFx(false);
    }

    #region Init
    /// <summary>탄을 쏜다 (호스트/싱글 원본). 착탄 시 zone.Key가 있으면 장판을 남긴다.</summary>
    public void Init(GameObject owner, Vector3 origin, Vector3 target, float flightTime, float impactRadius, int damage,
                     ZoneSettings zone)
    {
#if PHOTON_FUSION
        if (NetworkMonsterDirector.ShouldBroadcastEffects)
            NetworkMonsterDirector.Instance.BroadcastDragonFrostBall(Main.Pool.GetAddress(gameObject), origin, target, flightTime);
#endif
        Begin(owner, origin, target, flightTime, impactRadius, damage, zone, visual: false);
    }

    /// <summary>멀티 클라 전용: 비행·착탄 연출만 하는 복제본.</summary>
    public void InitVisual(Vector3 origin, Vector3 target, float flightTime)
        => Begin(null, origin, target, flightTime, 0f, 0, default, visual: true);

    private void Begin(GameObject owner, Vector3 origin, Vector3 target, float flightTime, float impactRadius, int damage,
                       ZoneSettings zone, bool visual)
    {
        this.owner = owner;
        ownerBoss = owner != null ? owner.GetComponent<DragonBoss>() : null;
        this.origin = origin;
        this.target = target;
        this.flightTime = Mathf.Max(flightTime, 0.05f);
        this.impactRadius = impactRadius;
        this.damage = damage;
        this.zone = zone;
        visualOnly = visual;
        timer = 0f;
        flying = true;
        lingering = false;

        Vector3 dir = target - origin;
        transform.SetPositionAndRotation(origin, dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) : Quaternion.identity);
        foreach (var trail in _trails) trail.Clear();
        SetFlightFx(true);
    }
    #endregion

    #region IPoolable
    public void OnSpawn()
    {
        flying = false;
        lingering = false;
    }

    public void OnDespawn()
    {
        flying = false;
        lingering = false;
        SetFlightFx(false);
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
        if (!isActiveAndEnabled) return;

        if (lingering)
        {
            timer -= deltaTime;
            if (timer > 0f) return;
            lingering = false;
            Extensions.Despawn(gameObject);
            return;
        }

        if (!flying) return;

        timer += deltaTime;
        float t = Mathf.Clamp01(timer / flightTime);
        transform.position = Vector3.Lerp(origin, target, t);
        if (t < 1f) return;

        Impact();
    }

    private void Impact()
    {
        flying = false;
        lingering = true;
        timer = lingerTime;
        StopEmitting();

        // 착탄 이펙트는 클라도 각자 띄운다 (복제본도 착탄 시점이 같으므로 따로 보내지 않는다)
        if (!string.IsNullOrEmpty(impactVfxKey))
            SpawnImpactVfxAsync(target).Forget();

        if (visualOnly) return;

        DamageArea();

        if (!string.IsNullOrEmpty(zone.Key))
            SpawnZoneAsync(owner, target, zone).Forget();
    }

    private void DamageArea()
    {
        if (damage <= 0 || impactRadius <= 0f) return;

        _struck.Clear();
        foreach (var col in Physics.OverlapSphere(target, impactRadius + 1f, ~0, QueryTriggerInteraction.Collide))
        {
            var player = col.GetComponentInParent<Player>();
            if (player == null || !player.IsAlive || !_struck.Add(player)) continue;

            Vector3 d = player.transform.position - target;
            d.y = 0f;
            if (d.sqrMagnitude > impactRadius * impactRadius) continue;

            int dmgAmount = ownerBoss != null ? ownerBoss.ModifyOutgoingDamage(player, damage) : damage;
            var ctx = new DamageContext(owner, target, dmgAmount, string.Empty);
            if (player.TryGetComponent<IDamageable>(out var dmg) && dmg.CanDamage(ctx))
                dmg.ApplyDamage(ctx);
        }
    }

    private async UniTaskVoid SpawnImpactVfxAsync(Vector3 position)
    {
        var vfx = await Extensions.SpawnAsync<DragonVfx>(impactVfxKey);
        if (vfx == null) return;
        vfx.PlayVisual(position, impactVfxScale);
    }

    private static async UniTaskVoid SpawnZoneAsync(GameObject owner, Vector3 center, ZoneSettings zone)
    {
        var hazard = await Extensions.SpawnAsync<DragonHazardZone>(zone.Key);
        if (hazard == null) return;
        hazard.Init(owner, center, zone.Radius, zone.Duration, zone.SlowMultiplier, zone.DotPerSecond);
    }

    private void SetFlightFx(bool on)
    {
        if (flightFx != null) flightFx.gameObject.SetActive(on);
        if (!on) return;
        foreach (var ps in _systems)
        {
            ps.Clear(true);
            ps.Play(true);
        }
        foreach (var trail in _trails) trail.emitting = true;
    }

    private void StopEmitting()
    {
        foreach (var ps in _systems) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        foreach (var trail in _trails) trail.emitting = false;
    }
}
