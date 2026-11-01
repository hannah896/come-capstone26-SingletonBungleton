using UnityEngine;

/// <summary>플레이어가 있는 지역의 비와 눈을 로컬 파티클로 표시합니다.</summary>
[DisallowMultipleComponent]
public class WorldWeatherEffects : MonoBehaviour
{
    private sealed class Effect
    {
        public GameObject Root;
        public PrecipitationKind Kind;
        public ParticleSystem[] Systems;
        public float[] FullRates;
        public ParticleSystem.Particle[][] Buffers;
        public float Intensity;
        public int LastWetRegion = -1;
    }

    private WorldWeatherController _weather;
    private WorldWeatherSettings _settings;
    private GameObject _container;
    private Effect _rain;
    private Effect _snow;
    private LoopManager _loop;
    private Player _localPlayer;
    private Transform _target;
    private Transform _previousTarget;
    private Vector3 _previousAnchor;
    private bool _hasAnchor;
    private float _playerSearchRemaining;
    private AudioSource _rainAudio;
    private JSAM.AudioManager _audioManager;
    private float _audioSearchTimer;

    public PrecipitationKind CurrentPrecipitation { get; private set; }
    public float RainBlend { get; private set; }
    public float SnowBlend { get; private set; }

    public float RainIntensity => _rain != null ? _rain.Intensity : 0f;
    public float SnowIntensity => _snow != null ? _snow.Intensity : 0f;

    public void Initialize(WorldWeatherController weather, WorldWeatherSettings settings)
    {
        Shutdown();
        if (weather == null || settings == null) return;
        _weather = weather;
        _settings = settings;

        // 비활성 부모 아래서 준비해 맑은 날 초기화 중 prewarm 입자가 나타나지 않게 합니다.
        _container = new GameObject("LocalWeatherEffects");
        _container.SetActive(false);
        _container.transform.SetParent(transform, false);
        _rain = CreateEffect(settings.RainPrefab, PrecipitationKind.Rain);
        _snow = CreateEffect(settings.SnowPrefab, PrecipitationKind.Snow);
        if (settings.RainLoop != null)
        {
            _rainAudio = _container.AddComponent<AudioSource>();
            _rainAudio.playOnAwake = false;
            _rainAudio.loop = true;
            _rainAudio.spatialBlend = 0f;
            _rainAudio.volume = 0f;
            _rainAudio.clip = settings.RainLoop;
        }
        _container.SetActive(true);
        if (isActiveAndEnabled) Subscribe();
    }

    public void Shutdown()
    {
        Unsubscribe();
        StopAndClear(_rain);
        StopAndClear(_snow);
        ResetPresentation();
        if (_container != null)
        {
            _container.SetActive(false);
            if (Application.isPlaying) Destroy(_container);
            else DestroyImmediate(_container);
        }
        _container = null;
        _rainAudio = null;
        _audioManager = null;
        _audioSearchTimer = 0f;
        _rain = _snow = null;
        _weather = null;
        _settings = null;
        _localPlayer = null;
        _target = _previousTarget = null;
        _hasAnchor = false;
        _playerSearchRemaining = 0f;
    }

    /// <summary>지정하지 않으면 로컬 플레이어, 다음으로 Main Camera를 사용합니다.</summary>
    public void SetTarget(Transform target)
    {
        _target = target;
        _hasAnchor = false;
    }

    private void OnEnable()
    {
        if (_weather != null && _container != null) Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        StopAndClear(_rain);
        StopAndClear(_snow);
        ResetPresentation();
        _hasAnchor = false;
    }

    private void OnDestroy() => Shutdown();

    private void Subscribe()
    {
        if (_loop != null) return;
        _loop = Main.Loop;
        if (_loop != null) _loop.OnLateUpdate += OnLoopLateUpdate;
    }

    private void Unsubscribe()
    {
        if (_loop != null) _loop.OnLateUpdate -= OnLoopLateUpdate;
        _loop = null;
    }

    private Effect CreateEffect(GameObject prefab, PrecipitationKind kind)
    {
        if (prefab == null) return null;
        GameObject instance = Instantiate(prefab, _container.transform, false);
        instance.name = "Runtime_" + kind;
        // 날씨 컨트롤러가 위치와 강도를 소유하므로 프리팹의 수동 제어기는 실행하지 않습니다.
        foreach (RainEffect control in instance.GetComponentsInChildren<RainEffect>(true)) control.enabled = false;
        foreach (LODGroup lod in instance.GetComponentsInChildren<LODGroup>(true)) lod.enabled = false;

        ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        var effect = new Effect
        {
            Root = instance, Kind = kind, Systems = systems,
            FullRates = new float[systems.Length], Buffers = new ParticleSystem.Particle[systems.Length][]
        };
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem ps = systems[i];
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.prewarm = false;
            main.loop = true;
            main.stopAction = ParticleSystemStopAction.None;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            if (kind == PrecipitationKind.Snow) ConfigureSnow(ps);

            ParticleSystem.EmissionModule emission = ps.emission;
            effect.FullRates[i] = kind == PrecipitationKind.Snow
                ? Mathf.Max(0f, _settings.SnowEmissionRate) : emission.rateOverTimeMultiplier;
            emission.rateOverTimeMultiplier = 0f;
            emission.enabled = false;
            effect.Buffers[i] = new ParticleSystem.Particle[ps.main.maxParticles];
        }
        return effect;
    }

    private static void ConfigureSnow(ParticleSystem ps)
    {
        // 외부 눈 에셋과 사용자 프리팹은 보존하고 런타임 복제본만 강수용으로 맞춥니다.
        ParticleSystem.MainModule main = ps.main;
        main.simulationSpeed = 1f;
        main.startSpeed = 0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 12f);
        main.maxParticles = Mathf.Max(main.maxParticles, 1600);
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(24f, 0.5f, 24f);
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = 0.15f;
        velocity.y = -1.5f;
        velocity.z = 0.05f;
        ParticleSystem.CollisionModule collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.collidesWith = LayerMask.GetMask("Ground", "Structure");
        collision.enableDynamicColliders = false;
        collision.lifetimeLoss = 1f;
        collision.sendCollisionMessages = false;
        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null) renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    private Transform ResolveTarget(float deltaTime)
    {
        if (_target != null) return _target;
        if (_localPlayer != null && _localPlayer.isActiveAndEnabled && _localPlayer.IsLocalPlayer)
            return _localPlayer.transform;

        _playerSearchRemaining -= Mathf.Max(0f, deltaTime);
        if (_playerSearchRemaining <= 0f)
        {
            _playerSearchRemaining = 1f;
            _localPlayer = null;
            foreach (Player player in FindObjectsByType<Player>(FindObjectsSortMode.None))
            {
                if (!player.IsLocalPlayer) continue;
                _localPlayer = player;
                return player.transform;
            }
        }
        Camera mainCamera = Camera.main;
        return mainCamera != null ? mainCamera.transform : null;
    }

    private void OnLoopLateUpdate(float deltaTime)
    {
        if (_weather == null || !_weather.IsReady)
        {
            StopAndClear(_rain);
            StopAndClear(_snow);
            ResetPresentation();
            return;
        }

        Transform target = ResolveTarget(deltaTime);
        if (target == null)
        {
            StopAndClear(_rain);
            StopAndClear(_snow);
            ResetPresentation();
            return;
        }

        Camera mainCamera = Camera.main;
        Vector3 anchor = target.position;
        if (_target == null && mainCamera != null)
            anchor = new Vector3(mainCamera.transform.position.x, target.position.y, mainCamera.transform.position.z);
        bool teleported = !_hasAnchor || target != _previousTarget || (anchor - _previousAnchor).sqrMagnitude > 144f;
        _rain?.Root.transform.SetPositionAndRotation(anchor, Quaternion.identity);
        _snow?.Root.transform.SetPositionAndRotation(anchor, Quaternion.identity);
        if (teleported)
        {
            ClearParticles(_rain);
            ClearParticles(_snow);
        }
        _previousAnchor = anchor;
        _previousTarget = target;
        _hasAnchor = true;

        bool wet = _weather.TryGetWeather(target.position, out RegionWeatherState state) && state.IsPrecipitating;
        if (wet && state.Kind == PrecipitationKind.Rain && _rain != null) _rain.LastWetRegion = state.RegionIndex;
        if (wet && state.Kind == PrecipitationKind.Snow && _snow != null) _snow.LastWetRegion = state.RegionIndex;
        float step = _settings.TransitionSeconds <= 0f ? 1f : Mathf.Max(0f, deltaTime) / _settings.TransitionSeconds;
        CurrentPrecipitation = wet ? state.Kind : PrecipitationKind.None;
        RainBlend = Mathf.MoveTowards(RainBlend, CurrentPrecipitation == PrecipitationKind.Rain ? 1f : 0f, step);
        SnowBlend = Mathf.MoveTowards(SnowBlend, CurrentPrecipitation == PrecipitationKind.Snow ? 1f : 0f, step);
        SetIntensity(_rain, RainBlend * Mathf.Clamp01(_settings.RainIntensity), 1f);
        SetIntensity(_snow, SnowBlend * Mathf.Clamp01(_settings.SnowIntensity), 1f);
        UpdateRainAudio(deltaTime);
        ClipOutsideWeather(_rain);
        ClipOutsideWeather(_snow);
    }

    private void ResetPresentation()
    {
        CurrentPrecipitation = PrecipitationKind.None;
        RainBlend = SnowBlend = 0f;
        if (_rainAudio != null)
        {
            _rainAudio.volume = 0f;
            _rainAudio.Stop();
        }
    }

    private void UpdateRainAudio(float deltaTime)
    {
        if (_rainAudio == null) return;
        // 별도 로컬 루프도 기존 JSAM의 마스터/SFX 볼륨과 음소거를 따릅니다.
        if (_audioManager == null)
        {
            _audioSearchTimer -= Mathf.Max(0f, deltaTime);
            if (_audioSearchTimer <= 0f)
            {
                _audioSearchTimer = 1f;
                _audioManager = FindFirstObjectByType<JSAM.AudioManager>();
            }
        }
        float volume = 0f;
        if (_audioManager != null && !JSAM.AudioManager.MasterMuted && !JSAM.AudioManager.SoundMuted)
            volume = JSAM.AudioManager.MasterVolume * JSAM.AudioManager.SoundVolume;
        _rainAudio.volume = Mathf.Clamp01(_settings.RainVolume) * RainBlend * volume;
        if (_rainAudio.volume > 0f)
        {
            if (!_rainAudio.isPlaying) _rainAudio.Play();
        }
        else if (_rainAudio.isPlaying) _rainAudio.Stop();
    }

    private static void SetIntensity(Effect effect, float target, float step)
    {
        if (effect == null) return;
        effect.Intensity = Mathf.MoveTowards(effect.Intensity, target, step);
        for (int i = 0; i < effect.Systems.Length; i++)
        {
            ParticleSystem ps = effect.Systems[i];
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTimeMultiplier = effect.FullRates[i] * effect.Intensity;
            emission.enabled = effect.Intensity > 0f;
            if (effect.Intensity > 0f && !ps.isEmitting) ps.Play(false);
            else if (effect.Intensity <= 0f && ps.isPlaying) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void ClipOutsideWeather(Effect effect)
    {
        if (effect == null) return;
        for (int i = 0; i < effect.Systems.Length; i++)
        {
            ParticleSystem ps = effect.Systems[i];
            if (ps.particleCount == 0) continue;
            if (effect.Buffers[i].Length < ps.main.maxParticles)
                effect.Buffers[i] = new ParticleSystem.Particle[ps.main.maxParticles];
            ParticleSystem.Particle[] particles = effect.Buffers[i];
            int count = ps.GetParticles(particles);
            bool changed = false;
            for (int index = 0; index < count; index++)
            {
                // 생성 볼륨이 지역 경계에 걸쳐도 맑은 이웃 지역과 사막에 강수가 새지 않게 합니다.
                if (_weather.TryGetWeather(particles[index].position, out RegionWeatherState state) &&
                    state.Kind == effect.Kind && (state.IsPrecipitating || state.RegionIndex == effect.LastWetRegion)) continue;
                particles[index].remainingLifetime = 0f;
                changed = true;
            }
            if (changed) ps.SetParticles(particles, count);
        }
    }

    private static void ClearParticles(Effect effect)
    {
        if (effect == null) return;
        foreach (ParticleSystem ps in effect.Systems) if (ps != null) ps.Clear(false);
    }

    private static void StopAndClear(Effect effect)
    {
        if (effect == null) return;
        effect.Intensity = 0f;
        effect.LastWetRegion = -1;
        foreach (ParticleSystem ps in effect.Systems)
        {
            if (ps == null) continue;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            emission.rateOverTimeMultiplier = 0f;
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
