using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 월드 배치 오브젝트, 최초 배치 몬스터, 월드 시계 주기 규칙을 호스트에서 실행합니다.
/// 배치 오브젝트가 호스트의 활성 청크 밖에 있어도 논리 배치 데이터로 규칙을 검사합니다.
/// </summary>
public sealed class WorldMonsterSpawnDirector : MonoBehaviour
{
    private WorldLogicData _logic;
    private DynamicSpawnSettings _settings;
    private WorldClock _clock;
    private CancellationTokenSource _cancellation;
    private readonly List<Player> _players = new();
    private readonly Dictionary<int, InitialState> _initial = new();
    private readonly Dictionary<int, SourceState> _sources = new();
    private readonly Dictionary<string, PeriodicState> _periodic = new();
    private readonly Dictionary<Monster, ActiveMonster> _active = new();
    private readonly List<MemberState> _dormantMembers = new();
    private readonly HashSet<int> _activeInitialIds = new();
    private readonly HashSet<int> _pendingInitialIds = new();
    private readonly HashSet<string> _invalidMonsterKeys = new();
    private float _checkCooldown;
    private int _pendingCount;
    private int _epoch;
    private bool _initialized;

    public void Initialize(WorldLogicData logic, WorldDisposeData placements, WorldSettings worldSettings,
        MonsterSpawnSaveData saved, CancellationToken token)
    {
        Shutdown();
        if (logic == null || placements == null || worldSettings?.DynamicSpawnSettings == null || Main.Loop == null)
        {
            Debug.LogError("[WorldMonsterSpawnDirector] 월드 배치나 설정이 준비되지 않았습니다.", this);
            return;
        }

        _logic = logic;
        _settings = worldSettings.DynamicSpawnSettings;
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        float now = CurrentTime;

        if (placements.InitialMonsterPlacements != null)
            foreach (WorldInitialMonsterPlacement placement in placements.InitialMonsterPlacements)
                if (placement != null && placement.id != 0)
                    _initial[placement.id] = new InitialState { Placement = placement };

        foreach (ChunkData chunk in logic.GetAllChunks())
            foreach (DisposeData placement in chunk.DisposeDatas)
                if (placement?.monsterSpawnRule != null && placement.instanceId != 0)
                    _sources[placement.instanceId] = new SourceState
                    {
                        Placement = placement,
                        Chunk = chunk,
                        NextTime = now + (placement.monsterSpawnRule.spawnImmediately
                            ? 0f : Mathf.Max(0.1f, placement.monsterSpawnRule.intervalSeconds))
                    };

        if (worldSettings.PeriodicSpawnRules != null)
            foreach (PeriodicSpawnRule rule in worldSettings.PeriodicSpawnRules)
            {
                if (rule == null) continue;
                if (string.IsNullOrWhiteSpace(rule.ruleId))
                {
                    Debug.LogWarning($"[WorldMonsterSpawnDirector] 주기 규칙 '{rule.name}'에 ruleId가 없어 건너뜁니다.", rule);
                    continue;
                }
                if (_periodic.ContainsKey(rule.ruleId))
                {
                    Debug.LogWarning($"[WorldMonsterSpawnDirector] 중복 ruleId '{rule.ruleId}'를 건너뜁니다.", rule);
                    continue;
                }
                _periodic.Add(rule.ruleId, new PeriodicState
                {
                    Rule = rule,
                    NextTime = now + Mathf.Max(0f, rule.firstDelaySeconds)
                });
            }

        try { Restore(saved); }
        catch
        {
            Shutdown();
            throw;
        }
        _checkCooldown = 0f;
        _clock = WorldClock.Instance;
        _initialized = true;
        if (_clock != null)
        {
            _clock.OnDayPassed += HandleDayPassed;
            if (Monster.IsSimulatedPeer && _clock.CurrentMoonPhase == MoonPhase.Full)
                QueueFullMoon(_clock.CurrentDay);
        }
        Main.Loop.OnGameUpdate += OnGameUpdate;
    }

    public MonsterSpawnSaveData Capture()
    {
        var data = new MonsterSpawnSaveData();
        foreach (ActiveMonster active in _active.Values)
        {
            if (active.Dead || active.Monster == null || !active.Monster.isActiveAndEnabled) continue;
            if (active.InitialId != 0 && _initial.TryGetValue(active.InitialId, out InitialState state))
                SaveLiveState(state, active.Monster);
            else if (active.InitialId == 0 && active.Monster.Status != null && !active.Monster.Status.IsDead)
                data.members.Add(ToMemberSaveData(active));
        }

        foreach (KeyValuePair<int, InitialState> pair in _initial)
        {
            InitialState state = pair.Value;
            if (!state.Dead && !state.HasLiveState) continue;
            data.initialMonsters.Add(new InitialMonsterSaveData
            {
                id = pair.Key, dead = state.Dead, hasLiveState = state.HasLiveState,
                position = state.Position, hp = state.Hp
            });
        }
        foreach (KeyValuePair<int, SourceState> pair in _sources)
            data.sources.Add(new SpawnSourceSaveData { id = pair.Key, nextSpawnTime = pair.Value.NextTime });
        foreach (KeyValuePair<string, PeriodicState> pair in _periodic)
            data.periodic.Add(new PeriodicSpawnSaveData
            {
                ruleId = pair.Key,
                nextSpawnTime = pair.Value.NextTime,
                lastFullMoonDay = pair.Value.LastFullMoonDay,
                pendingFullMoonDay = pair.Value.PendingFullMoonDay
            });
        foreach (MemberState member in _dormantMembers)
            data.members.Add(member.ToSaveData());
        data.initialMonsters.Sort((a, b) => a.id.CompareTo(b.id));
        data.sources.Sort((a, b) => a.id.CompareTo(b.id));
        data.periodic.Sort((a, b) => string.CompareOrdinal(a.ruleId, b.ruleId));
        return data;
    }

    private void Restore(MonsterSpawnSaveData saved)
    {
        if (saved == null) return;
        if (saved.initialMonsters != null)
            foreach (InitialMonsterSaveData entry in saved.initialMonsters)
                if (entry != null)
                {
                    if (!_initial.TryGetValue(entry.id, out InitialState state))
                        throw new InvalidOperationException($"저장한 최초 몬스터 배치 ID를 찾을 수 없습니다: {entry.id}");
                    state.Dead = entry.dead;
                    state.HasLiveState = !entry.dead && entry.hasLiveState;
                    state.Position = entry.position;
                    state.Hp = entry.hp;
                }
        if (saved.sources != null)
            foreach (SpawnSourceSaveData entry in saved.sources)
                if (entry != null)
                {
                    if (!_sources.TryGetValue(entry.id, out SourceState state))
                        throw new InvalidOperationException($"저장한 스폰 오브젝트 ID를 찾을 수 없습니다: {entry.id}");
                    state.NextTime = entry.nextSpawnTime;
                }
        if (saved.periodic != null)
            foreach (PeriodicSpawnSaveData entry in saved.periodic)
                if (entry != null)
                {
                    if (!_periodic.TryGetValue(entry.ruleId, out PeriodicState state))
                        throw new InvalidOperationException($"저장한 주기 스폰 규칙을 찾을 수 없습니다: {entry.ruleId}");
                    state.NextTime = entry.nextSpawnTime;
                    state.LastFullMoonDay = entry.lastFullMoonDay;
                    state.PendingFullMoonDay = entry.pendingFullMoonDay;
                }
        if (saved.members != null)
            foreach (SpawnedMonsterSaveData entry in saved.members)
                if (entry != null)
                {
                    if ((entry.sourceId != 0 && !_sources.ContainsKey(entry.sourceId)) ||
                        (entry.sourceId == 0 && !_periodic.ContainsKey(entry.periodicRuleId)))
                        throw new InvalidOperationException("저장한 몬스터의 스폰 출처를 찾을 수 없습니다.");
                    _dormantMembers.Add(new MemberState
                    {
                        SourceId = entry.sourceId,
                        PeriodicId = entry.periodicRuleId,
                        Position = entry.position,
                        Hp = entry.hp
                    });
                }
    }

    public void Shutdown(bool despawnOwnedMonsters = true)
    {
        _epoch++;
        if (Main.Loop != null) Main.Loop.OnGameUpdate -= OnGameUpdate;
        if (_clock != null) _clock.OnDayPassed -= HandleDayPassed;
        _clock = null;
        _initialized = false;
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
        foreach (ActiveMonster active in new List<ActiveMonster>(_active.Values))
        {
            if (despawnOwnedMonsters) DespawnOwned(active);
            else Forget(active, saveLive: false);
        }
        _active.Clear();
        _activeInitialIds.Clear();
        _pendingInitialIds.Clear();
        _initial.Clear();
        _sources.Clear();
        _periodic.Clear();
        _dormantMembers.Clear();
        _players.Clear();
        _invalidMonsterKeys.Clear();
        _pendingCount = 0;
        _logic = null;
        _settings = null;
    }

    private void OnGameUpdate(float deltaTime)
    {
        if (!_initialized || !Monster.IsSimulatedPeer ||
            (Main.Save != null && (Main.Save.IsRestoring || Main.Save.IsCapturing))) return;
        _checkCooldown -= deltaTime;
        if (_checkCooldown > 0f) return;
        _checkCooldown = Mathf.Max(0.1f, _settings.TargetRefreshInterval);

        RefreshPlayers();
        ReapInactive();
        float now = CurrentTime;
        UpdateDormantMembers();
        UpdateInitial();
        UpdateSources(now);
        UpdatePeriodic(now);
    }

    private void RefreshPlayers()
    {
        _players.Clear();
        foreach (Player player in FindObjectsByType<Player>(FindObjectsSortMode.None))
            if (player != null && player.isActiveAndEnabled && player.IsAlive)
                _players.Add(player);
    }

    private void ReapInactive()
    {
        foreach (ActiveMonster active in new List<ActiveMonster>(_active.Values))
        {
            Monster monster = active.Monster;
            if (monster == null || !monster.isActiveAndEnabled)
            {
                Forget(active, saveLive: !active.Dead);
                continue;
            }
            if (active.Dead) continue;
            if (active.InitialId == 0)
            {
                if (!AnyPlayerNear(monster.transform.position, _settings.AdditionalMonsterDespawnDistance))
                    DespawnOwned(active, saveLive: true);
                continue;
            }
            float distance = Mathf.Max(_settings.InitialMonsterActivationDistance,
                _settings.InitialMonsterDespawnDistance);
            if (!AnyPlayerNear(monster.transform.position, distance))
            {
                DespawnOwned(active, saveLive: true);
            }
        }
    }

    private void UpdateDormantMembers()
    {
        for (int i = _dormantMembers.Count - 1; i >= 0; i--)
        {
            if (AvailableCount <= 0) return;
            MemberState member = _dormantMembers[i];
            if (member.Pending || !ValidPosition(member.Position) ||
                !AnyPlayerNear(member.Position, _settings.InitialMonsterActivationDistance)) continue;
            member.Pending = true;
            _pendingCount++;
            SpawnMemberAsync(member, _epoch, _cancellation.Token).Forget();
        }
    }

    private void UpdateInitial()
    {
        foreach (KeyValuePair<int, InitialState> pair in _initial)
        {
            if (AvailableCount <= 0) return;
            InitialState state = pair.Value;
            if (state.Dead || _activeInitialIds.Contains(pair.Key) || _pendingInitialIds.Contains(pair.Key)) continue;
            Vector3 position = state.HasLiveState ? state.Position : TilePosition(state.Placement.tile);
            if (!AnyPlayerNear(position, _settings.InitialMonsterActivationDistance)) continue;
            if (!ValidPosition(position)) continue;
            _pendingInitialIds.Add(pair.Key);
            _pendingCount++;
            SpawnInitialAsync(state, position, _epoch, _cancellation.Token).Forget();
        }
    }

    private void UpdateSources(float now)
    {
        foreach (KeyValuePair<int, SourceState> pair in _sources)
        {
            SourceState state = pair.Value;
            SourceSpawnRule rule = state.Placement.monsterSpawnRule;
            if (state.Spawning || now < state.NextTime || rule == null) continue;
            state.NextTime = now + Mathf.Max(0.1f, rule.intervalSeconds);
            if (state.Chunk.IsObjectDestroyed(pair.Key) || !TimeAllowed(rule.allowedTimePhases) ||
                !ValidMonsterKey(rule.monsterKey) || rule.spawnChance <= 0f ||
                (rule.spawnChance < 1f && UnityEngine.Random.value >= rule.spawnChance)) continue;
            Vector3 center = TilePosition(state.Placement.tilePosition);
            float activationDistance = rule.activationDistance > 0f
                ? rule.activationDistance : _settings.AdditionalMonsterDespawnDistance;
            if (!AnyPlayerNear(center, activationDistance)) continue;
            int available = Mathf.Min(AvailableCount, Mathf.Max(0, rule.maxAlive - CountActiveForSource(pair.Key)));
            int count = Mathf.Min(available, RandomCount(rule.minSpawnCount, rule.maxSpawnCount));
            if (count <= 0) continue;
            state.Spawning = true;
            _pendingCount += count;
            SpawnGroupAsync(rule.monsterKey, count, center, Mathf.Min(rule.placementRadius, rule.spawnRadius),
                rule.spawnRadius, pair.Key, null, _ => state.Spawning = false, _epoch, _cancellation.Token).Forget();
        }
    }

    private void UpdatePeriodic(float now)
    {
        foreach (KeyValuePair<string, PeriodicState> pair in _periodic)
        {
            PeriodicState state = pair.Value;
            PeriodicSpawnRule rule = state.Rule;
            if (state.Spawning) continue;
            if (rule.scheduleMode == PeriodicMonsterScheduleMode.FullMoon)
            {
                UpdateFullMoonPeriodic(pair.Key, state);
                continue;
            }
            if (now < state.NextTime) continue;
            // 시간 건너뛰기로 여러 회차가 지났어도 한 번만 검사합니다.
            state.NextTime = now + Mathf.Max(0.1f, rule.intervalSeconds);
            if (!TimeAllowed(rule.allowedTimePhases) || !ValidMonsterKey(rule.monsterKey)) continue;
            int available = Mathf.Min(AvailableCount, Mathf.Max(0, rule.maxAlive - CountActiveForPeriodic(pair.Key)));
            int count = Mathf.Min(available, RandomCount(rule.minSpawnCount, rule.maxSpawnCount));
            if (count <= 0) continue;

            Vector3 center;
            float minRadius;
            float maxRadius;
            if (rule.positionMode == PeriodicMonsterPositionMode.PlayerRing)
            {
                if (_players.Count == 0) continue;
                center = _players[UnityEngine.Random.Range(0, _players.Count)].transform.position;
                minRadius = Mathf.Max(0f, rule.minDistanceFromPlayer);
                maxRadius = Mathf.Max(minRadius, rule.maxDistanceFromPlayer);
            }
            else
            {
                center = rule.fixedWorldPoint;
                minRadius = 0f;
                maxRadius = rule.spawnRadius;
                if (!AnyPlayerNear(center, Mathf.Max(_settings.InitialMonsterActivationDistance, maxRadius))) continue;
            }

            state.Spawning = true;
            _pendingCount += count;
            SpawnGroupAsync(rule.monsterKey, count, center, minRadius, maxRadius, 0,
                pair.Key, _ => state.Spawning = false, _epoch, _cancellation.Token).Forget();
        }
    }

    private void UpdateFullMoonPeriodic(string ruleId, PeriodicState state)
    {
        int fullMoonDay = state.PendingFullMoonDay;
        if (fullMoonDay <= state.LastFullMoonDay) return;

        PeriodicSpawnRule rule = state.Rule;
        if (!TimeAllowed(rule.allowedTimePhases) || !ValidMonsterKey(rule.monsterKey)) return;

        // 이전 만월의 보스가 살아 있거나 비활성 상태로 저장돼 있다면 이번 만월은 소비한다.
        int existing = CountActiveForPeriodic(ruleId);
        if (existing > 0)
        {
            state.LastFullMoonDay = fullMoonDay;
            return;
        }

        // 보스 한 마리는 일반 추가 몬스터 예산이 가득 차도 생성한다.
        int available = Mathf.Max(0, rule.maxAlive);
        int count = Mathf.Min(available, RandomCount(rule.minSpawnCount, rule.maxSpawnCount));
        if (count <= 0 || _players.Count == 0) return;

        Vector3 center;
        float minRadius;
        float maxRadius;
        if (rule.positionMode == PeriodicMonsterPositionMode.PlayerRing)
        {
            center = _players[UnityEngine.Random.Range(0, _players.Count)].transform.position;
            minRadius = Mathf.Max(0f, rule.minDistanceFromPlayer);
            maxRadius = Mathf.Max(minRadius, rule.maxDistanceFromPlayer);
        }
        else
        {
            center = rule.fixedWorldPoint;
            minRadius = 0f;
            maxRadius = rule.spawnRadius;
            if (!AnyPlayerNear(center, Mathf.Max(_settings.InitialMonsterActivationDistance, maxRadius))) return;
        }

        state.Spawning = true;
        _pendingCount += count;
        SpawnGroupAsync(rule.monsterKey, count, center, minRadius, maxRadius, 0, ruleId,
            spawnedCount =>
            {
                state.Spawning = false;
                // 위치 탐색이나 비동기 생성이 실패하면 다음 갱신에서 다시 시도한다.
                if (spawnedCount > 0) state.LastFullMoonDay = Mathf.Max(state.LastFullMoonDay, fullMoonDay);
            }, _epoch, _cancellation.Token,
            rule.positionMode == PeriodicMonsterPositionMode.PlayerRing ? minRadius : 0f).Forget();
    }

    private void HandleDayPassed(int daysPassed)
    {
        if (!_initialized || !Monster.IsSimulatedPeer ||
            (Main.Save != null && Main.Save.IsRestoring) ||
            daysPassed % WorldClock.MoonCycleDays != (int)MoonPhase.Full) return;
        QueueFullMoon(daysPassed + 1);
    }

    private void QueueFullMoon(int currentDay)
    {
        foreach (PeriodicState state in _periodic.Values)
            if (state.Rule.scheduleMode == PeriodicMonsterScheduleMode.FullMoon)
                state.PendingFullMoonDay = Mathf.Max(state.PendingFullMoonDay, currentDay);
    }

    private async UniTaskVoid SpawnInitialAsync(InitialState state, Vector3 position, int epoch, CancellationToken token)
    {
        try
        {
            Monster monster = await SpawnRegisteredAsync(state.Placement.monsterKey, position, token);
            if (monster == null) return;
            if (state.HasLiveState && state.Hp > 0f) monster.Status?.SetCurrentHp(state.Hp);
            Track(monster, state.Placement.id, 0, null);
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { UnityEngine.Debug.LogException(e, this); }
        finally
        {
            if (epoch == _epoch)
            {
                _pendingInitialIds.Remove(state.Placement.id);
                _pendingCount = Mathf.Max(0, _pendingCount - 1);
            }
        }
    }

    private async UniTaskVoid SpawnMemberAsync(MemberState member, int epoch, CancellationToken token)
    {
        try
        {
            string key = member.SourceId != 0
                ? _sources[member.SourceId].Placement.monsterSpawnRule.monsterKey
                : _periodic[member.PeriodicId].Rule.monsterKey;
            Monster monster = await SpawnRegisteredAsync(key, member.Position, token);
            if (monster == null) return;
            if (member.Hp > 0f) monster.Status?.SetCurrentHp(member.Hp);
            Track(monster, 0, member.SourceId, member.PeriodicId);
            _dormantMembers.Remove(member);
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { UnityEngine.Debug.LogException(e, this); }
        finally
        {
            if (epoch == _epoch)
            {
                member.Pending = false;
                _pendingCount = Mathf.Max(0, _pendingCount - 1);
            }
        }
    }

    private async UniTaskVoid SpawnGroupAsync(string key, int count, Vector3 center, float minRadius,
        float maxRadius, int sourceId, string periodicId, Action<int> completed, int epoch, CancellationToken token,
        float minDistanceFromPlayers = 0f)
    {
        int spawnedCount = 0;
        try
        {
            for (int i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested();
                if (!TrySamplePosition(center, minRadius, maxRadius, out Vector3 position,
                        minDistanceFromPlayers)) continue;
                Monster monster = await SpawnRegisteredAsync(key, position, token);
                if (monster == null) continue;
                Track(monster, 0, sourceId, periodicId);
                spawnedCount++;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { UnityEngine.Debug.LogException(e, this); }
        finally
        {
            if (epoch == _epoch)
            {
                _pendingCount = Mathf.Max(0, _pendingCount - count);
                completed?.Invoke(spawnedCount);
            }
        }
    }

    private async UniTask<Monster> SpawnRegisteredAsync(string key, Vector3 position, CancellationToken token)
    {
        if (!ValidMonsterKey(key)) return null;
        MonsterCatalog catalog = MonsterCatalog.Instance;
        byte id = catalog.GetCatalogId(key);
        MonsterCatalog.Entry entry = catalog.GetEntry(id);
        Monster monster = await Monster.SpawnAsync(entry.addressableKey, entry.statData, position);
        if (monster == null) return null;
        if (token.IsCancellationRequested || !_initialized)
        {
            Extensions.Despawn(monster.gameObject);
            token.ThrowIfCancellationRequested();
            return null;
        }
#if PHOTON_FUSION
        if (Main.Network != null && Main.Network.IsInRoom &&
            (NetworkMonsterDirector.Instance == null || NetworkMonsterDirector.Instance.RegisterMonster(monster, id) < 0))
        {
            Extensions.Despawn(monster.gameObject);
            return null;
        }
#endif
        return monster;
    }

    private void Track(Monster monster, int initialId, int sourceId, string periodicId)
    {
        var active = new ActiveMonster { Monster = monster, InitialId = initialId, SourceId = sourceId,
            PeriodicId = periodicId };
        _active.Add(monster, active);
        if (initialId != 0) _activeInitialIds.Add(initialId);
        monster.Died += HandleDied;
        monster.Despawned += HandleDespawned;
    }

    private void HandleDied(Monster monster)
    {
        if (!_active.TryGetValue(monster, out ActiveMonster active)) return;
        active.Dead = true;
        if (active.InitialId != 0 && _initial.TryGetValue(active.InitialId, out InitialState state))
        {
            state.Dead = true;
            state.HasLiveState = false;
        }
    }

    private void HandleDespawned(Monster monster)
    {
        if (_active.TryGetValue(monster, out ActiveMonster active)) Forget(active, saveLive: !active.Dead);
    }

    private void Forget(ActiveMonster active, bool saveLive)
    {
        Monster monster = active.Monster;
        if (saveLive && active.InitialId != 0 && monster != null &&
            _initial.TryGetValue(active.InitialId, out InitialState state) && !state.Dead)
            SaveLiveState(state, monster);
        else if (saveLive && active.InitialId == 0 && monster != null && monster.Status != null &&
            !monster.Status.IsDead)
            _dormantMembers.Add(new MemberState
            {
                SourceId = active.SourceId,
                PeriodicId = active.PeriodicId,
                Position = monster.transform.position,
                Hp = monster.Status.CurrentHp
            });
        if (!ReferenceEquals(monster, null))
        {
            monster.Died -= HandleDied;
            monster.Despawned -= HandleDespawned;
            _active.Remove(monster);
        }
        if (active.InitialId != 0) _activeInitialIds.Remove(active.InitialId);
    }

    private void DespawnOwned(ActiveMonster active, bool saveLive = false)
    {
        Monster monster = active.Monster;
        Forget(active, saveLive);
        if (monster == null) return;
#if PHOTON_FUSION
        NetworkMonsterDirector.Instance?.UnregisterMonster(monster);
#endif
        Extensions.Despawn(monster.gameObject);
    }

    private static void SaveLiveState(InitialState state, Monster monster)
    {
        if (monster.Status == null || monster.Status.IsDead) return;
        state.HasLiveState = true;
        state.Position = monster.transform.position;
        state.Hp = monster.Status.CurrentHp;
    }

    private static SpawnedMonsterSaveData ToMemberSaveData(ActiveMonster active)
        => new SpawnedMonsterSaveData
        {
            sourceId = active.SourceId,
            periodicRuleId = active.PeriodicId,
            position = active.Monster.transform.position,
            hp = active.Monster.Status.CurrentHp
        };

    private bool TrySamplePosition(Vector3 center, float minRadius, float maxRadius, out Vector3 position,
        float minDistanceFromPlayers = 0f)
    {
        position = default;
        minRadius = Mathf.Max(0f, minRadius);
        maxRadius = Mathf.Max(minRadius, maxRadius);
        for (int i = 0; i < Mathf.Max(1, _settings.MaxPositionAttempts); i++)
        {
            Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.right;
            float radius = Mathf.Sqrt(UnityEngine.Random.Range(minRadius * minRadius, maxRadius * maxRadius));
            Vector3 candidate = center + new Vector3(direction.x * radius, 0f, direction.y * radius);
            if (!ValidPosition(candidate)) continue;
            int x = Mathf.FloorToInt(candidate.x);
            int z = Mathf.FloorToInt(candidate.z);
            if (_logic.OccupiedWorld[x, z]) continue;
            candidate.y = _logic.GetHeightAt(x, z) + _settings.SpawnHeightOffset;
            if (minDistanceFromPlayers > 0f && AnyPlayerNear(candidate, minDistanceFromPlayers)) continue;
            if (_settings.SpawnBlockingMask.value != 0 && _settings.CollisionCheckRadius > 0f &&
                Physics.CheckSphere(candidate + Vector3.up * _settings.CollisionCheckRadius,
                    _settings.CollisionCheckRadius, _settings.SpawnBlockingMask, QueryTriggerInteraction.Ignore))
                continue;
            position = candidate;
            return true;
        }
        return false;
    }

    private bool ValidPosition(Vector3 position)
    {
        int x = Mathf.FloorToInt(position.x);
        int z = Mathf.FloorToInt(position.z);
        return _logic != null && x >= 0 && z >= 0 && x < _logic.TerrainSize.x && z < _logic.TerrainSize.y &&
            _logic.GetRegionAt(x, z) >= 0;
    }

    private Vector3 TilePosition(Vector2Int tile)
        => new Vector3(tile.x + 0.5f, _logic.GetHeightAt(tile.x, tile.y) + _settings.SpawnHeightOffset,
            tile.y + 0.5f);

    private bool AnyPlayerNear(Vector3 position, float distance)
    {
        float sqr = Mathf.Max(0f, distance) * Mathf.Max(0f, distance);
        foreach (Player player in _players)
        {
            Vector3 delta = player.transform.position - position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= sqr) return true;
        }
        return false;
    }

    private bool TimeAllowed(SpawnTimePhaseMask mask)
    {
        WorldClock clock = WorldClock.Instance;
        return clock == null ? mask == SpawnTimePhaseMask.All :
            (mask & clock.CurrentTimePhase.ToSpawnMask()) != 0;
    }

    private bool ValidMonsterKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        MonsterCatalog catalog = MonsterCatalog.Instance;
        byte id = catalog != null ? catalog.GetCatalogId(key) : (byte)0;
        if (id != 0 && catalog.GetEntry(id) != null) return true;
        if (_invalidMonsterKeys.Add(key))
            Debug.LogWarning($"[WorldMonsterSpawnDirector] MonsterCatalog에 '{key}'가 없습니다.", this);
        return false;
    }

    private int CountActiveForSource(int id)
    {
        int count = 0;
        foreach (ActiveMonster active in _active.Values)
            if (active.SourceId == id && !active.Dead && active.Monster != null && active.Monster.isActiveAndEnabled) count++;
        foreach (MemberState member in _dormantMembers)
            if (member.SourceId == id) count++;
        return count;
    }

    private int CountActiveForPeriodic(string id)
    {
        int count = 0;
        foreach (ActiveMonster active in _active.Values)
            if (active.PeriodicId == id && !active.Dead && active.Monster != null && active.Monster.isActiveAndEnabled) count++;
        foreach (MemberState member in _dormantMembers)
            if (member.PeriodicId == id) count++;
        return count;
    }

    private int AvailableCount
    {
        get
        {
            int count = _pendingCount;
            foreach (ActiveMonster active in _active.Values)
                if (!active.Dead && active.Monster != null && active.Monster.isActiveAndEnabled) count++;
            return Mathf.Max(0, _settings.MaxAdditionalMonsters - count);
        }
    }

    private static int RandomCount(int minimum, int maximum)
    {
        int min = Mathf.Max(0, minimum);
        int max = Mathf.Max(min, maximum);
        return UnityEngine.Random.Range(min, max + 1);
    }

    private static float CurrentTime => WorldClock.Instance != null ? WorldClock.Instance.TotalInGameSeconds : 0f;

    private void OnDestroy() => Shutdown(despawnOwnedMonsters: false);

    private sealed class InitialState
    {
        public WorldInitialMonsterPlacement Placement;
        public bool Dead;
        public bool HasLiveState;
        public Vector3 Position;
        public float Hp;
    }

    private sealed class SourceState
    {
        public DisposeData Placement;
        public ChunkData Chunk;
        public float NextTime;
        public bool Spawning;
    }

    private sealed class PeriodicState
    {
        public PeriodicSpawnRule Rule;
        public float NextTime;
        public int LastFullMoonDay;
        public int PendingFullMoonDay;
        public bool Spawning;
    }

    private sealed class ActiveMonster
    {
        public Monster Monster;
        public int InitialId;
        public int SourceId;
        public string PeriodicId;
        public bool Dead;
    }

    private sealed class MemberState
    {
        public int SourceId;
        public string PeriodicId;
        public Vector3 Position;
        public float Hp;
        public bool Pending;

        public SpawnedMonsterSaveData ToSaveData() => new SpawnedMonsterSaveData
        {
            sourceId = SourceId,
            periodicRuleId = PeriodicId,
            position = Position,
            hp = Hp
        };
    }
}
