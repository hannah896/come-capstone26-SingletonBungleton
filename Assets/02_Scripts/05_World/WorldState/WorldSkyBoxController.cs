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
        ApplyPhase(_worldClock.CurrentTimePhase);
    }

    private void Unsubscribe()
    {
        if (_isSubscribed && _worldClock != null)
            _worldClock.OnPhaseChanged -= ApplyPhase;
        _isSubscribed = false;
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

        // 외부 에셋의 Material 자체를 변경하지 않고 씬에서 사용하는 참조만 교체한다.
        RenderSettings.skybox = settings.Skybox;
        RenderSettings.ambientIntensity = settings.AmbientIntensity;
        _appliedSkybox = settings.Skybox;
        if (_boundLight != null)
        {
            _boundLight.color = settings.LightColor;
            _boundLight.intensity = settings.LightIntensity;
        }

        // 프레임마다 호출하지 않고 시간대를 적용할 때만 환경광 프로브를 갱신한다.
        DynamicGI.UpdateEnvironment();
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
}
