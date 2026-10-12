using UnityEngine;

/// <summary>
/// 월드 환경음 + 시간 이벤트 소리. WorldClock과 같은 오브젝트에 자동으로 붙는다(게임 씬에서만 동작).
///
/// 환경음은 두 겹을 동시에 루프로 깐다 (각각 2D, JSAM 루프 사운드):
///   - 시간대: 아침·오후 = 새소리, 황혼·새벽 = 귀뚜라미
///   - 장소:   바다 가까이 = 파도 / 설원·고원·바위·사막 = 바람 / 늪 = 늪 소리 / 그 외 없음
/// 장소는 카메라(=듣는 사람) 위치의 바이옴으로 2초마다 다시 판정한다. 바뀔 때만 이전 루프를 끄고 새 루프를 켠다.
///
/// 이벤트: 아침이 되면 닭 울음, 보름달 날 황혼이 되면 천둥(만월 보스가 오는 밤 알림).
/// 모든 피어가 자기 시계로 각자 재생한다(시계는 동기화돼 있으므로 같은 순간에 들린다).
/// </summary>
public class WorldAmbienceAudio : MonoBehaviour
{
    // 바다(해안선)에서 이 타일 수 안이면 파도 소리
    private const int CoastDistanceTiles = 8;
    // 장소 재판정 간격(초)
    private const float RecheckInterval = 2f;

    private WorldClock clock;
    private AudioLibrarySounds? timeLayer;
    private AudioLibrarySounds? placeLayer;
    private float recheckTimer;
    private bool loopHooked;
    private bool clockHooked;

    private void OnEnable()
    {
        if (!loopHooked && Main.Loop != null)
        {
            Main.Loop.OnUpdate += OnLoopUpdate;
            loopHooked = true;
        }
    }

    // 사운드 정지는 이벤트 해제와 달리 비활성화 때도 해야 한다(씬을 떠나면 환경음이 남지 않게)
    private void OnDisable()
    {
        SetLayer(ref timeLayer, null);
        SetLayer(ref placeLayer, null);
    }

    // 구독 해제는 파괴 시점에
    private void OnDestroy()
    {
        if (loopHooked && Main.Instance != null && Main.Loop != null)
            Main.Loop.OnUpdate -= OnLoopUpdate;
        if (clockHooked && clock != null)
            clock.OnPhaseChanged -= OnPhaseChanged;
        loopHooked = false;
        clockHooked = false;
    }

    private void OnLoopUpdate(float deltaTime)
    {
        if (!isActiveAndEnabled) return;

        if (!clockHooked)
        {
            clock = GetComponent<WorldClock>();
            if (clock == null) return;
            clock.OnPhaseChanged += OnPhaseChanged;
            clockHooked = true;
        }

        recheckTimer -= deltaTime;
        if (recheckTimer > 0f) return;
        recheckTimer = RecheckInterval;

        SetLayer(ref timeLayer, PickTimeLayer(clock.CurrentTimePhase));
        SetLayer(ref placeLayer, PickPlaceLayer());
    }

    private void OnPhaseChanged(TimePhase phase)
    {
        if (phase == TimePhase.Morning)
            Extensions.PlaySFX(AudioLibrarySounds.Rooster);
        else if (phase == TimePhase.Dusk && clock != null && clock.CurrentMoonPhase == MoonPhase.Full)
            Extensions.PlaySFX(AudioLibrarySounds.Thunder);

        recheckTimer = 0f; // 시간대 환경음을 바로 바꾼다
    }

    private static AudioLibrarySounds PickTimeLayer(TimePhase phase) => phase switch
    {
        TimePhase.Morning or TimePhase.Afternoon => AudioLibrarySounds.AmbBirds,
        _ => AudioLibrarySounds.AmbCrickets,
    };

    // 카메라 위치의 지형으로 장소 환경음을 고른다. 월드가 아직 준비되지 않았으면 없음.
    private static AudioLibrarySounds? PickPlaceLayer()
    {
        Camera cam = Camera.main;
        WorldGenManager gen = WorldGenManager.Instance;
        WorldLogicData logic = gen != null ? gen.CurrentLogicData : null;
        if (cam == null || logic == null || gen.GraphDirector == null) return null;

        int x = Mathf.FloorToInt(cam.transform.position.x);
        int z = Mathf.FloorToInt(cam.transform.position.z);
        if (x < 0 || z < 0 || x >= logic.TerrainSize.x || z >= logic.TerrainSize.y) return AudioLibrarySounds.AmbWaves;

        if (logic.DistanceToOceanWorld != null && logic.DistanceToOceanWorld[x, z] <= CoastDistanceTiles)
            return AudioLibrarySounds.AmbWaves;

        int region = logic.GetRegionAt(x, z);
        var graph = gen.GraphDirector.GetWorldGraphData();
        if (region < 0 || graph?.Nodes == null || region >= graph.Nodes.Count) return null;

        BiomeData biome = graph.Nodes[region]?.BiomeData;
        if (biome == null) return null;

        return biome.BiomeType switch
        {
            BiomeType.Snow or BiomeType.Highlands or BiomeType.Rocky or BiomeType.Desert => AudioLibrarySounds.AmbWind,
            BiomeType.Swamp => AudioLibrarySounds.AmbSwamp,
            _ => null,
        };
    }

    // 바뀔 때만 이전 루프를 끄고 새 루프를 켠다
    private static void SetLayer(ref AudioLibrarySounds? current, AudioLibrarySounds? next)
    {
        if (current == next) return;
        if (current.HasValue) Extensions.StopSFXOn(current.Value, null);
        current = next;
        if (current.HasValue) Extensions.PlaySFX(current.Value);
    }
}
