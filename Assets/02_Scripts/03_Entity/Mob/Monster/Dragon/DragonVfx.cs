using UnityEngine;

/// <summary>
/// 파티클 이펙트를 풀링으로 재생하는 범용 래퍼 (착탄·폭발·추락 충돌 등 한 번 터지고 끝나는 연출).
/// 자식에 넣어 둔 파티클 시스템(PixPlays 등)을 처음부터 재생하고, 일정 시간 뒤 풀로 돌아간다.
/// 외부 이펙트 프리팹의 스크립트는 Instantiate/Destroy를 쓰므로 부르지 않고 파티클만 직접 재생한다.
///
/// 멀티: 호스트 원본이 Play에서 클라에 복제본을 띄운다.
/// </summary>
public class DragonVfx : MonoBehaviour, IPoolable
{
    [Tooltip("재생 후 풀로 돌아가기까지 시간(초)")]
    [SerializeField] private float lifeTime = 2f;

    private ParticleSystem[] _systems;
    private float timer;
    private bool active;
    private bool loopHooked;

    private void Awake()
    {
        _systems = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in _systems)
        {
            var main = ps.main;
            main.playOnAwake = false;
        }
        StopAll();
    }

    /// <summary>position에서 scale 배율로 재생한다 (호스트/싱글 원본).</summary>
    public void Play(Vector3 position, float scale = 1f)
    {
#if PHOTON_FUSION
        if (NetworkMonsterDirector.ShouldBroadcastEffects)
            NetworkMonsterDirector.Instance.BroadcastDragonVfx(Main.Pool.GetAddress(gameObject), position, scale);
#endif
        PlayVisual(position, scale);
    }

    /// <summary>판정 없는 연출 재생 (멀티 클라 복제본도 이걸 쓴다).</summary>
    public void PlayVisual(Vector3 position, float scale = 1f)
    {
        transform.SetPositionAndRotation(position, Quaternion.identity);
        transform.localScale = Vector3.one * Mathf.Max(scale, 0.01f);
        timer = lifeTime;
        active = true;

        // 충격 흔들림: 클수록(추락 3 · 폭발 6) 세고 멀리까지. 각 피어가 자기 카메라 기준으로 흔든다
        PlayerFirstPersonCameraController.ShakeAt(position, Mathf.Min(0.4f * scale, 2.5f), 8f * scale, 0.6f);

        foreach (var ps in _systems)
        {
            if (ps == null) continue;
            ps.Clear(true);
            ps.Play(true);
        }
    }

    #region IPoolable
    public void OnSpawn() => active = false;

    public void OnDespawn()
    {
        active = false;
        StopAll();
    }
    #endregion

    private void StopAll()
    {
        if (_systems == null) return;
        foreach (var ps in _systems)
        {
            if (ps == null) continue;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void OnEnable()
    {
        if (loopHooked) return;
        Main.Loop.OnUpdate += HandleUpdate;
        loopHooked = true;
    }

    // 구독 해제는 OnDisable이 아니라 파괴 시점에만 수행한다.
    private void OnDestroy()
    {
        if (!loopHooked) return;
        if (Main.Loop != null)
            Main.Loop.OnUpdate -= HandleUpdate;
        loopHooked = false;
    }

    private void HandleUpdate(float deltaTime)
    {
        if (!active || !isActiveAndEnabled) return;
        timer -= deltaTime;
        if (timer > 0f) return;
        active = false;
        Extensions.Despawn(gameObject);
    }
}
