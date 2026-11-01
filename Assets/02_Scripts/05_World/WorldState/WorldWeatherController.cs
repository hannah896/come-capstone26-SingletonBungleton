using System;
using UnityEngine;

/// <summary>호스트가 모든 지역의 수분을 진행하고 클라이언트는 확정된 강수를 표시합니다.</summary>
[DisallowMultipleComponent]
public sealed class WorldWeatherController : MonoBehaviour
{
    public static WorldWeatherController Instance { get; private set; }
    public bool IsInitialized { get; private set; }
    public bool IsReady => IsInitialized && (!IsNetworkClient || _receivedSnapshot);
    public int PendingNetworkRequest { get; private set; }
    public System.Collections.Generic.IReadOnlyList<RegionWeatherState> Regions => _regions;

    private WorldLogicData _logic;
    private WorldClock _clock;
    private WorldWeatherSettings _settings;
    private WorldWeatherEffects _effects;
    private LoopManager _loop;
    private RegionWeatherState[] _regions = Array.Empty<RegionWeatherState>();
    private int _seed;
    private string _generationId;
    private bool _receivedSnapshot;
    private bool _needsBroadcast;
    private double _lastSnapshotSeconds = -1d;
    private float _requestTimer;

    private static bool IsNetworkClient => Main.Network != null && Main.Network.IsInRoom && !Main.Network.IsHost;
    private double ClockSeconds => (double)_clock.DaysPassed * WorldClock.SecondsPerDay + _clock.ElapsedSecondsToday;

    private void Awake() => Instance = this;

    public void Initialize(WorldLogicData logic, WorldGraphData graph, int seed, WorldClock clock,
        WorldWeatherSettings settings, WorldWeatherSaveData saved = null)
    {
        Shutdown();
        if (logic?.TerritoryWorld == null || graph?.Nodes == null || clock == null)
            throw new ArgumentException("날씨 초기화에는 월드 로직, 그래프와 시계가 필요합니다.");

        clock.EnsureInitialized();
        _logic = logic;
        _clock = clock;
        _settings = settings;
        _seed = seed;
        _generationId = Guid.NewGuid().ToString("N");
        _regions = new RegionWeatherState[graph.Nodes.Count];
        double start = ClockSeconds;
        for (int i = 0; i < _regions.Length; i++)
            _regions[i] = new RegionWeatherState(i, graph.Nodes[i]?.BiomeData?.Weather,
                unchecked((uint)seed), start, WorldClock.SecondsPerDay);

        if (saved != null) Restore(saved, preserveGeneration: false);
        IsInitialized = true;
        Instance = this;
        PendingNetworkRequest = WorldWeatherNetwork.NextRequest();
        _requestTimer = 0f;
        _needsBroadcast = true;
        if (isActiveAndEnabled) Bind();
    }

    private void OnEnable()
    {
        if (IsInitialized) Bind();
    }

    private void Bind()
    {
        if (_loop == null)
        {
            _loop = Main.Loop;
            if (_loop != null) _loop.OnUpdate += OnLoopUpdate;
        }
        if (_settings != null)
        {
            if (_effects == null && !TryGetComponent(out _effects))
                _effects = gameObject.AddComponent<WorldWeatherEffects>();
            _effects.Initialize(this, _settings);
        }
    }

    private void OnDisable()
    {
        if (_loop != null) _loop.OnUpdate -= OnLoopUpdate;
        _loop = null;
        if (_effects != null) _effects.Shutdown();
    }

    private void OnDestroy()
    {
        Shutdown();
        if (Instance == this) Instance = null;
    }

    public void Shutdown()
    {
        OnDisable();
        IsInitialized = false;
        _receivedSnapshot = false;
        _needsBroadcast = false;
        _lastSnapshotSeconds = -1d;
        _logic = null;
        _clock = null;
        _regions = Array.Empty<RegionWeatherState>();
    }

    private void OnLoopUpdate(float deltaTime)
    {
        if (!IsInitialized || (Main.Save != null && (Main.Save.IsRestoring || Main.Save.IsCapturing))) return;
        if (IsNetworkClient)
        {
            if (!_receivedSnapshot)
            {
                _requestTimer -= deltaTime;
                if (_requestTimer <= 0f)
                {
                    _requestTimer = 2f;
                    WorldWeatherNetwork.Request(_seed, PendingNetworkRequest);
                }
                return;
            }

            // 다음 기간의 추첨은 호스트만 합니다. 복제된 기간 안에서만 수분을 보간합니다.
            double now = ClockSeconds;
            foreach (RegionWeatherState region in _regions)
            {
                double end = region.PhaseStartSeconds + region.PhaseDurationSeconds;
                double target = region.Kind == PrecipitationKind.None ? now : Math.Min(now, end - 0.00001d);
                region.AdvanceTo(Math.Max(region.LastUpdateSeconds, target));
            }
        }
        else
        {
            AdvanceToClock();
            if (_needsBroadcast)
            {
                WorldWeatherSaveData snapshot = Capture();
                _needsBroadcast = false;
                WorldWeatherNetwork.Broadcast(snapshot);
            }
        }
    }

    private bool AdvanceToClock()
    {
        bool changed = false;
        double now = ClockSeconds;
        foreach (RegionWeatherState region in _regions)
            changed |= region.AdvanceTo(Math.Max(region.LastUpdateSeconds, now));
        _needsBroadcast |= changed;
        return changed;
    }

    /// <summary>파티클의 생성 위치도 검사할 수 있도록 부작용 없이 지역 상태를 조회합니다.</summary>
    public bool TryGetWeather(Vector3 position, out RegionWeatherState state)
    {
        state = null;
        if (!IsReady || _logic?.TerritoryWorld == null ||
            !(position.x >= 0f && position.x < _logic.TerrainSize.x &&
              position.z >= 0f && position.z < _logic.TerrainSize.y)) return false;
        int regionIndex = _logic.GetRegionAt(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.z));
        if (regionIndex < 0 || regionIndex >= _regions.Length) return false;
        state = _regions[regionIndex];
        return true;
    }

    public WorldWeatherSaveData Capture()
    {
        if (!IsInitialized) throw new InvalidOperationException("지역 날씨가 아직 준비되지 않았습니다.");
        if (IsNetworkClient) throw new InvalidOperationException("날씨 저장 상태는 호스트가 생성합니다.");
        AdvanceToClock();
        var data = new WorldWeatherSaveData
        {
            seed = _seed,
            regionCount = _regions.Length,
            generationId = _generationId,
            clockSeconds = ClockSeconds,
        };
        foreach (RegionWeatherState region in _regions)
        {
            data.regions.Add(region.Capture());
            data.clockSeconds = Math.Max(data.clockSeconds, region.LastUpdateSeconds);
        }
        return data;
    }

    private void Restore(WorldWeatherSaveData saved, bool preserveGeneration)
    {
        WorldWeatherSaveData.Validate(saved);
        if (saved.seed != _seed || saved.regionCount != _regions.Length)
            throw new ArgumentException("날씨 저장 데이터와 생성한 월드가 일치하지 않습니다.");
        foreach (RegionWeatherSaveData region in saved.regions)
            _regions[region.regionIndex].ValidateSnapshot(region);
        foreach (RegionWeatherSaveData region in saved.regions)
            _regions[region.regionIndex].Restore(region);
        if (preserveGeneration) _generationId = saved.generationId;
    }

    /// <summary>첫 응답의 요청 번호와 월드 세대를 검사해 이전 월드 패킷을 제외합니다.</summary>
    public void ReceiveNetworkSnapshot(WorldWeatherSaveData snapshot, int request)
    {
        if (!IsInitialized || !IsNetworkClient || snapshot == null || snapshot.seed != _seed) return;
        if (!_receivedSnapshot && request != PendingNetworkRequest) return;
        if (_receivedSnapshot && snapshot.generationId != _generationId) return;
        if (_receivedSnapshot && snapshot.clockSeconds < _lastSnapshotSeconds) return;
        Restore(snapshot, preserveGeneration: true);
        _lastSnapshotSeconds = snapshot.clockSeconds;
        _receivedSnapshot = true;
    }
}
