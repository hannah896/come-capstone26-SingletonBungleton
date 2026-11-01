using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 보스 아레나 분위기. 이 피어의 플레이어가 아레나 안에 있는 동안 하늘을 어두운 달밤(Resources/ArenaSkybox)으로 바꾸고,
/// 주변광/태양광을 낮추고 은은한 보랏빛 안개를 깔아 스산하게 만든다. 아레나를 벗어나거나 아레나가 사라지면 원래대로 돌린다.
///
/// - 다른 피어에는 영향이 없다 (각자 자기 플레이어가 안에 있을 때만 바꾼다).
/// - 낮밤 시스템(WorldSkyBoxController)이 시간대가 바뀔 때 하늘을 덮어쓰면, 아레나 안에서는 다시 아레나 하늘로 되돌린다.
/// - MoonGate가 아레나를 만들 때 이 컴포넌트를 붙인다.
/// </summary>
public class ArenaAtmosphere : MonoBehaviour
{
    private const string SkyboxResource = "ArenaSkybox";

    // 분위기 수치 (조정은 여기서)
    private const float AmbientIntensity = 0.2f;
    private const float SunIntensity = 0.12f;
    private static readonly Color SunColor = new(0.45f, 0.4f, 0.8f, 1f);
    private static readonly Color FogColor = new(0.06f, 0.045f, 0.1f, 1f);
    private const float FogDensity = 0.008f;

    private Material skybox;
    private bool active;
    private float pollTimer;

    // 아레나에 들어가기 전의 환경값
    private Material savedSkybox;
    private float savedAmbient;
    private bool savedFog;
    private FogMode savedFogMode;
    private Color savedFogColor;
    private float savedFogDensity;
    private Light savedSun;
    private Color savedSunColor;
    private float savedSunIntensity;

    private void Awake()
    {
        skybox = Resources.Load<Material>(SkyboxResource);
        if (skybox == null)
            Debug.LogWarning($"[ArenaAtmosphere] Resources/{SkyboxResource} 재질이 없어 하늘은 바꾸지 못합니다.");
    }

    private void Update()
    {
        pollTimer -= Time.unscaledDeltaTime;
        if (pollTimer <= 0f)
        {
            pollTimer = 0.25f;
            bool shouldBeActive = IsLocalPlayerInArena();
            if (shouldBeActive && !active) Apply();
            else if (!shouldBeActive && active) Restore();
        }

        // 시간대가 바뀌어 낮밤 시스템이 하늘/조명을 덮어쓴 경우 다시 아레나 분위기로 돌린다
        if (active && skybox != null && RenderSettings.skybox != skybox)
            Apply(refreshSavedValues: false);
    }

    private void OnDestroy()
    {
        if (active) Restore();
    }

    private static bool IsLocalPlayerInArena()
    {
        foreach (Player player in FindObjectsByType<Player>(FindObjectsSortMode.None))
            if (player != null && player.IsLocalPlayer && player.InBossArena) return true;

        return false;
    }

    private void Apply(bool refreshSavedValues = true)
    {
        if (refreshSavedValues)
        {
            savedSkybox = RenderSettings.skybox;
            savedAmbient = RenderSettings.ambientIntensity;
            savedFog = RenderSettings.fog;
            savedFogMode = RenderSettings.fogMode;
            savedFogColor = RenderSettings.fogColor;
            savedFogDensity = RenderSettings.fogDensity;

            savedSun = RenderSettings.sun;
            if (savedSun != null)
            {
                savedSunColor = savedSun.color;
                savedSunIntensity = savedSun.intensity;
            }
        }

        if (skybox != null) RenderSettings.skybox = skybox;
        RenderSettings.ambientIntensity = AmbientIntensity;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = FogColor;
        RenderSettings.fogDensity = FogDensity;

        Light sun = RenderSettings.sun;
        if (sun != null)
        {
            sun.color = SunColor;
            sun.intensity = SunIntensity;
        }

        DynamicGI.UpdateEnvironment();
        active = true;

        // 처음 들어갈 때만 보스전 BGM을 튼다 (낮밤 시스템이 하늘을 덮어써서 다시 적용할 때는 곡을 처음부터 다시 틀지 않는다)
        if (refreshSavedValues) PlayArenaMusic();
    }

    private static void PlayArenaMusic()
    {
        if (Main.Instance == null || Main.JSAM == null) return;
        Extensions.PlayBGM(AudioLibraryMusic.BossArenaBGM);
    }

    // 게임 월드에는 평소 BGM이 없으므로(로비에서만 재생) 아레나를 벗어나면 보스전 곡을 멈춘다
    private static void StopArenaMusic()
    {
        if (Main.Instance == null || Main.JSAM == null) return;
        Main.JSAM.StopBGM();
    }

    private void Restore()
    {
        active = false;

        RenderSettings.skybox = savedSkybox;
        RenderSettings.ambientIntensity = savedAmbient;
        RenderSettings.fog = savedFog;
        RenderSettings.fogMode = savedFogMode;
        RenderSettings.fogColor = savedFogColor;
        RenderSettings.fogDensity = savedFogDensity;

        if (savedSun != null)
        {
            savedSun.color = savedSunColor;
            savedSun.intensity = savedSunIntensity;
        }

        DynamicGI.UpdateEnvironment();
        StopArenaMusic();
    }
}
