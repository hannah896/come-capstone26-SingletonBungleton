using UnityEngine;

/// <summary>로컬 카메라나 지정한 대상 주변에 비를 내리고 강도를 조절합니다.</summary>
[DisallowMultipleComponent]
public class RainEffect : MonoBehaviour
{
    [SerializeField] private ParticleSystem _particles;
    [Tooltip("지정하면 이 대상을 따라갑니다. 비워 두면 Main Camera를 사용합니다.")]
    [SerializeField] private Transform _followTarget;
    [SerializeField] private bool _followMainCamera = true;
    [Range(0f, 1f)]
    [SerializeField] private float _intensity = 1f;
    [Tooltip("이 거리 이상 이동하면 이전 위치의 빗방울을 지웁니다.")]
    [Min(0.1f)]
    [SerializeField] private float _teleportDistance = 12f;

    private LoopManager _loop;
    private Transform _lastTarget;
    private float _fullEmissionRate;
    private float _appliedIntensity = -1f;
    private bool _hasEmissionRate;
    private bool _hasFollowPosition;

    public float Intensity => _intensity;

    private void OnEnable()
    {
        if (!Application.isPlaying) return;

        InitializeParticles();
        _loop = Main.Loop;
        if (_loop != null) _loop.OnLateUpdate += OnLoopLateUpdate;
        FollowTarget(true);
        ApplyIntensity();
    }

    private void OnDisable()
    {
        // 캐시한 루프에서 해제해 종료 중 Main을 새로 만들지 않습니다.
        if (_loop != null) _loop.OnLateUpdate -= OnLoopLateUpdate;
        _loop = null;
        _lastTarget = null;
        _hasFollowPosition = false;
        if (Application.isPlaying && _particles != null)
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    /// <summary>0이면 새 빗방울 생성을 멈추고, 1이면 프리팹의 최대 강도로 내립니다.</summary>
    public void SetIntensity(float intensity)
    {
        _intensity = float.IsNaN(intensity) ? 0f : Mathf.Clamp01(intensity);
        ApplyIntensity();
    }

    /// <summary>플레이어 등 추적 대상을 지정합니다. null이면 Main Camera 설정을 따릅니다.</summary>
    public void SetTarget(Transform target)
    {
        _followTarget = target;
        FollowTarget(true);
    }

    private void InitializeParticles()
    {
        if (_particles == null) _particles = GetComponentInChildren<ParticleSystem>(true);
        if (_particles == null || _hasEmissionRate) return;

        _fullEmissionRate = _particles.emission.rateOverTimeMultiplier;
        _hasEmissionRate = true;
    }

    private void ApplyIntensity()
    {
        InitializeParticles();
        if (_particles == null) return;

        _appliedIntensity = _intensity;
        ParticleSystem.EmissionModule emission = _particles.emission;
        emission.rateOverTimeMultiplier = _fullEmissionRate * _intensity;
        emission.enabled = _intensity > 0f;

        if (_intensity <= 0f)
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        else if (isActiveAndEnabled && !_particles.isPlaying)
            _particles.Play(true);
    }

    private void OnLoopLateUpdate(float deltaTime)
    {
        FollowTarget(false);
        if (_appliedIntensity != _intensity) ApplyIntensity();
    }

    private void FollowTarget(bool forceClear)
    {
        Transform target = _followTarget;
        if (target == null && _followMainCamera)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null) target = mainCamera.transform;
        }
        if (target == null || target == transform || target.IsChildOf(transform)) return;

        bool teleported = !_hasFollowPosition || target != _lastTarget ||
            (target.position - transform.position).sqrMagnitude > _teleportDistance * _teleportDistance;

        // 카메라 회전으로 비의 범위가 기울지 않도록 위치만 추적합니다.
        transform.SetPositionAndRotation(target.position, Quaternion.identity);
        if ((forceClear || teleported) && _particles != null) _particles.Clear(true);
        _lastTarget = target;
        _hasFollowPosition = true;
    }
}
