using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 월드 시계의 시간대에 맞춰 하늘과 조명을 표시한다. 시간 계산은 WorldClock이 담당한다.
/// </summary>
[DisallowMultipleComponent]
public class WorldSkyBoxController : MonoBehaviour
{
    private WorldSkyBoxSettings _settings;
    private Light _directionalLight;
    private WorldClock _worldClock;
    private bool _isSubscribed;
    private bool _hasOriginalSettings;
    private Material _originalSkybox;
    private Material _appliedSkybox;
    private float _originalAmbientIntensity;
    private Light _boundLight;
    private Color _originalLightColor;
    private float _originalLightIntensity;
    private WorldWeatherEffects _weather;
    private LoopManager _loop;
    private Material _runtimeSkybox;
    private Material _selectedSource;
    private TimePhase _selectedPhase;
    private PrecipitationKind _selectedWeather;

    /// <summary>파티클과 같은 로컬 날씨 전환 비율을 사용합니다.</summary>
    public void BindWeather(WorldWeatherEffects weather)
    {
        _weather = weather;
        // 같은 LateUpdate에서 파티클의 날씨 비율이 먼저 갱신되도록 구독 순서를 맞춥니다.
        if (_loop != null)
        {
            _loop.OnLateUpdate -= OnLoopLateUpdate;
            _loop.OnLateUpdate += OnLoopLateUpdate;
        }
        if (_worldClock != null && isActiveAndEnabled) ApplyPhase(_worldClock.CurrentTimePhase);
    }

    /// <summary>초기화 시 현재 시간대를 적용하고 이후 시간대 변경을 구독한다.</summary>
    public void Initialize(WorldClock worldClock, WorldSkyBoxSettings settings, Light directionalLight)
    {
        // 재초기화 전에 이전 시계 구독과 환경 설정을 정리한다.
        Unbind();
        if (worldClock == null || settings == null)
        {
            Debug.LogWarning("[WorldSkyBoxController] 시계와 하늘 설정이 필요합니다.", this);
            return;
        }

        _worldClock = worldClock;
        _settings = settings;
        _directionalLight = directionalLight;
        if (isActiveAndEnabled) SubscribeAndRefresh();
    }

    public void Unbind()
    {
        Unsubscribe();
        RestoreEnvironment();
        ReleaseRuntimeSkybox();
        _weather = null;
        _worldClock = null;
        _settings = null;
        _directionalLight = null;
    }

    private void OnEnable()
    {
        if (_worldClock != null) SubscribeAndRefresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
        RestoreEnvironment();
        ReleaseRuntimeSkybox();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void SubscribeAndRefresh()
    {
        if (!_isSubscribed)
        {
            _worldClock.OnPhaseChanged += ApplyPhase;
            _isSubscribed = true;
        }
        if (_loop == null && Application.isPlaying)
        {
            _loop = Main.Loop;
            if (_loop != null) _loop.OnLateUpdate += OnLoopLateUpdate;
        }
        ApplyPhase(_worldClock.CurrentTimePhase);
    }

    private void Unsubscribe()
    {
        if (_loop != null) _loop.OnLateUpdate -= OnLoopLateUpdate;
        _loop = null;
        if (_isSubscribed && _worldClock != null)
            _worldClock.OnPhaseChanged -= ApplyPhase;
        _isSubscribed = false;
    }

    private void OnLoopLateUpdate(float deltaTime)
    {
        if (_worldClock != null) ApplyPhase(_worldClock.CurrentTimePhase);
    }

    private void ApplyPhase(TimePhase phase)
    {
        // Additive 씬 전환 중 다른 씬의 전역 렌더 설정을 덮어쓰지 않는다.
        if (!isActiveAndEnabled || gameObject.scene != SceneManager.GetActiveScene()) return;

        WorldSkyBoxSettings.PhaseSettings settings = _settings.GetPhaseSettings(phase);
        if (settings == null || settings.Skybox == null)
        {
            Debug.LogWarning($"[WorldSkyBoxController] {phase} 스카이박스가 연결되지 않았습니다.", this);
            return;
        }

        CaptureEnvironment();

        float rain = _weather != null ? _weather.RainBlend : 0f;
        float snow = _weather != null ? _weather.SnowBlend : 0f;
        PrecipitationKind kind = rain <= 0f && snow <= 0f ? PrecipitationKind.None :
            rain >= snow ? PrecipitationKind.Rain : PrecipitationKind.Snow;
        Material source = kind == PrecipitationKind.Rain ? settings.RainSkybox :
            kind == PrecipitationKind.Snow ? settings.SnowSkybox : settings.Skybox;
        if (source == null) source = settings.Skybox;
        if (_appliedSkybox == null || source != _selectedSource || phase != _selectedPhase || kind != _selectedWeather)
        {
            Material previous = _runtimeSkybox;
            _runtimeSkybox = null;
            _appliedSkybox = source;
            if (source != settings.Skybox)
            {
                // 외부 원본 대신 런타임 복제본에서 시간대별 강수 하늘 밝기를 보정합니다.
                _runtimeSkybox = new Material(source) { name = "Runtime_WeatherSkybox" };
                if (_runtimeSkybox.HasProperty("_Exposure"))
                    _runtimeSkybox.SetFloat("_Exposure", source.GetFloat("_Exposure") * settings.WeatherSkyExposure);
                _appliedSkybox = _runtimeSkybox;
            }
            RenderSettings.skybox = _appliedSkybox;
            _selectedSource = source;
            _selectedPhase = phase;
            _selectedWeather = kind;
            if (previous != null) DestroyMaterial(previous);
            DynamicGI.UpdateEnvironment();
        }
        RenderSettings.ambientIntensity = settings.AmbientIntensity * Mathf.Max(0f,
            1f - rain * (1f - _settings.RainAmbientMultiplier) - snow * (1f - _settings.SnowAmbientMultiplier));
        if (_boundLight != null)
        {
            _boundLight.color = settings.LightColor;
            _boundLight.intensity = settings.LightIntensity * Mathf.Max(0f,
                1f - rain * (1f - _settings.RainLightMultiplier) - snow * (1f - _settings.SnowLightMultiplier));
        }

    }

    private void CaptureEnvironment()
    {
        if (_hasOriginalSettings) return;

        _originalSkybox = RenderSettings.skybox;
        _originalAmbientIntensity = RenderSettings.ambientIntensity;
        _boundLight = _directionalLight;
        if (_boundLight != null)
        {
            _originalLightColor = _boundLight.color;
            _originalLightIntensity = _boundLight.intensity;
        }
        _hasOriginalSettings = true;
    }

    private void RestoreEnvironment()
    {
        if (!_hasOriginalSettings) return;

        // 이미 다른 씬/컨트롤러가 하늘을 교체했다면 그 설정을 보존한다.
        if (gameObject.scene == SceneManager.GetActiveScene() && RenderSettings.skybox == _appliedSkybox)
        {
            RenderSettings.skybox = _originalSkybox;
            RenderSettings.ambientIntensity = _originalAmbientIntensity;
            DynamicGI.UpdateEnvironment();
        }
        if (_boundLight != null)
        {
            _boundLight.color = _originalLightColor;
            _boundLight.intensity = _originalLightIntensity;
        }
        _hasOriginalSettings = false;
        _boundLight = null;
        _appliedSkybox = null;
    }

    private void ReleaseRuntimeSkybox()
    {
        if (_runtimeSkybox != null) DestroyMaterial(_runtimeSkybox);
        _runtimeSkybox = null;
        _selectedSource = null;
    }

    private static void DestroyMaterial(Material material)
    {
        if (Application.isPlaying) Destroy(material);
        else DestroyImmediate(material);
    }
}
