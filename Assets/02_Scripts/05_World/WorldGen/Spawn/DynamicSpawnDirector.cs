using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 플레이어 주변 바이옴의 SpawnRuleSet을 사용해 런타임 몬스터 스폰을 관리합니다.
///
/// 실제 몬스터 생성/풀링은 Monster.SpawnAsync에, 멀티플레이 복제는
/// NetworkMonsterDirector의 공개 API에 위임합니다. 이 디렉터는 자신이 생성한
/// 몬스터만 추적하며 씬에 배치된 MonsterSpawner의 몬스터는 관리하지 않습니다.
/// </summary>
public sealed class DynamicSpawnDirector : MonoBehaviour
{
    private DynamicSpawnSettings _settings;
    private WorldLogicData _logicData;
    private WorldGraphData _graphData;
    private WorldChunkDirector _chunkDirector;

    private readonly List<Player> _players = new();
    private readonly HashSet<Vector2Int> _activeChunkCoords = new();
    private readonly List<SpawnedMonsterRecord> _spawnedMonsters = new();
    private readonly Dictionary<Player, Dictionary<MonsterSpawnRule, RuleRuntimeState>> _ruleStates = new();

    private CancellationTokenSource _spawnCts;
    private float _targetRefreshCooldown;
    private int _pendingSlotCost;
    private bool _isInitialized;

    public bool IsInitialized => _isInitialized;
    public int SpawnedMonsterCount => _spawnedMonsters.Count;

    /// <summary>
    /// 월드 생성 및 초기 청크 로드가 끝난 뒤 호출합니다.
    /// 재호출하면 이전 런타임 상태와 이 디렉터가 생성한 몬스터를 먼저 정리합니다.
    /// </summary>
    public void Initialize(
        WorldLogicData logicData,
        WorldGraphData graphData,
        WorldChunkDirector chunkDirector,
        DynamicSpawnSettings settings,
        CancellationToken cancellationToken = default)
    {
        Shutdown();

        if (logicData == null || graphData == null || chunkDirector == null || settings == null)
        {
            Debug.LogError("[DynamicSpawnDirector] 초기화에 필요한 월드 데이터 또는 설정이 없습니다.", this);
            return;
        }

        if (Main.Loop == null)
        {
            Debug.LogError("[DynamicSpawnDirector] Main.Loop가 준비되지 않았습니다.", this);
            return;
        }

        _settings = settings;
        _logicData = logicData;
        _graphData = graphData;
        _chunkDirector = chunkDirector;
        _spawnCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            this.GetCancellationTokenOnDestroy());

        _targetRefreshCooldown = 0f;
        _isInitialized = true;

        RefreshRuntimeTargets();
        Main.Loop.OnGameUpdate += OnGameUpdate;
    }

    /// <summary>업데이트 구독과 비동기 작업을 끝내고 선택적으로 생성 몬스터를 정리합니다.</summary>
    public void Shutdown(bool despawnOwnedMonsters = true)
    {
        if (Main.Loop != null)
            Main.Loop.OnGameUpdate -= OnGameUpdate;

        _spawnCts?.Cancel();
        _spawnCts?.Dispose();
        _spawnCts = null;

        if (despawnOwnedMonsters)
            DespawnAllOwnedMonsters();
        else
            _spawnedMonsters.Clear();

        _players.Clear();
        _activeChunkCoords.Clear();
        _ruleStates.Clear();
        _pendingSlotCost = 0;

        _logicData = null;
        _graphData = null;
        _chunkDirector = null;
        _settings = null;
        _isInitialized = false;
    }

    private void OnGameUpdate(float deltaTime)
    {
        if (!_isInitialized || !Monster.IsSimulatedPeer)
            return;

        _targetRefreshCooldown -= deltaTime;
        if (_targetRefreshCooldown <= 0f)
        {
            _targetRefreshCooldown = Mathf.Max(0.1f, _settings.TargetRefreshInterval);
            RefreshRuntimeTargets();
        }

        UpdateSpawnedMonsters(deltaTime);
        UpdateSpawnRules(deltaTime);
    }

    private void RefreshRuntimeTargets()
    {
        _players.Clear();

        Player[] foundPlayers = FindObjectsByType<Player>(FindObjectsSortMode.None);
        for (int i = 0; i < foundPlayers.Length; i++)
        {
            Player player = foundPlayers[i];
            if (player != null && player.isActiveAndEnabled)
                _players.Add(player);
        }

        _activeChunkCoords.Clear();
        if (_chunkDirector != null)
        {
            foreach (ChunkData chunk in _chunkDirector.GetActiveChunks())
            {
                if (chunk != null)
                    _activeChunkCoords.Add(chunk.ChunkCoord);
            }
        }

        RemoveMissingPlayerStates();
    }

    private void RemoveMissingPlayerStates()
    {
        if (_ruleStates.Count == 0)
            return;

        List<Player> stalePlayers = null;
        foreach (Player player in _ruleStates.Keys)
        {
            if (player != null && _players.Contains(player))
                continue;

            stalePlayers ??= new List<Player>();
            stalePlayers.Add(player);
        }

        if (stalePlayers == null)
            return;

        for (int i = 0; i < stalePlayers.Count; i++)
            _ruleStates.Remove(stalePlayers[i]);
    }

    private void UpdateSpawnRules(float deltaTime)
    {
        for (int playerIndex = 0; playerIndex < _players.Count; playerIndex++)
        {
            Player player = _players[playerIndex];
            if (player == null || !player.isActiveAndEnabled)
                continue;

            if (!TryGetSpawnRuleSetAt(player.transform.position, out SpawnRuleSet ruleSet))
                continue;

            if (ruleSet.MonsterRules == null)
                continue;

            for (int ruleIndex = 0; ruleIndex < ruleSet.MonsterRules.Count; ruleIndex++)
            {
                MonsterSpawnRule rule = ruleSet.MonsterRules[ruleIndex];
                if (rule == null)
                    continue;

                RuleRuntimeState state = GetRuleState(player, rule);
                state.Cooldown -= deltaTime;

                if (state.Cooldown > 0f || state.IsSpawning)
                    continue;

                state.Cooldown = Mathf.Max(0.1f, rule.spawnInterval);

                if (!CanAttemptSpawn(rule))
                    continue;

                state.IsSpawning = true;
                SpawnGroupAsync(player, ruleSet, rule, state, _spawnCts.Token).Forget();
            }
        }
    }

    private bool CanAttemptSpawn(MonsterSpawnRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.monsterKey))
            return false;

        if (GetAvailableSlotBudget() < GetSlotCost(rule))
            return false;

        WorldClock clock = WorldClock.Instance;
        if (clock == null)
        {
            if (rule.allowedTimePhases != SpawnTimePhaseMask.All)
                return false;
        }
        else if ((rule.allowedTimePhases & clock.CurrentTimePhase.ToSpawnMask()) == 0)
        {
            return false;
        }

        return UnityEngine.Random.value <= Mathf.Clamp01(rule.spawnChance);
    }

    private RuleRuntimeState GetRuleState(Player player, MonsterSpawnRule rule)
    {
        if (!_ruleStates.TryGetValue(player, out Dictionary<MonsterSpawnRule, RuleRuntimeState> playerStates))
        {
            playerStates = new Dictionary<MonsterSpawnRule, RuleRuntimeState>();
            _ruleStates.Add(player, playerStates);
        }

        if (!playerStates.TryGetValue(rule, out RuleRuntimeState state))
        {
            state = new RuleRuntimeState();
            playerStates.Add(rule, state);
        }

        return state;
    }

    private async UniTaskVoid SpawnGroupAsync(
        Player sourcePlayer,
        SpawnRuleSet sourceRuleSet,
        MonsterSpawnRule rule,
        RuleRuntimeState state,
        CancellationToken cancellationToken)
    {
        int reservedCost = 0;

        try
        {
            if (sourcePlayer == null)
                return;

            MonsterCatalog catalog = MonsterCatalog.Instance;
            byte catalogId = catalog.GetCatalogId(rule.monsterKey);
            MonsterCatalog.Entry entry = catalog.GetEntry(catalogId);

            if (catalogId == 0 || entry == null || string.IsNullOrWhiteSpace(entry.addressableKey))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    $"[DynamicSpawnDirector] '{rule.monsterKey}'가 MonsterCatalog에 없습니다.",
                    this);
#endif
                return;
            }

            int slotCost = GetSlotCost(rule);
            int minCount = Mathf.Max(0, rule.minSpawnCount);
            int maxCount = Mathf.Max(minCount, rule.maxSpawnCount);
            int desiredCount = UnityEngine.Random.Range(minCount, maxCount + 1);
            int affordableCount = GetAvailableSlotBudget() / slotCost;
            desiredCount = Mathf.Min(desiredCount, affordableCount);

            if (desiredCount <= 0)
                return;

            reservedCost = desiredCount * slotCost;
            _pendingSlotCost += reservedCost;

            for (int i = 0; i < desiredCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (sourcePlayer == null || !sourcePlayer.isActiveAndEnabled)
                    break;

                if (!TryFindSpawnPosition(sourcePlayer, sourceRuleSet, rule, out Vector3 spawnPosition))
                    continue;

                Monster monster = await Monster.SpawnAsync(
                    entry.addressableKey,
                    entry.statData,
                    spawnPosition);

                if (monster == null)
                    continue;

                if (cancellationToken.IsCancellationRequested || !_isInitialized)
                {
                    Extensions.Despawn(monster.gameObject);
                    cancellationToken.ThrowIfCancellationRequested();
                    continue;
                }

                if (!TryRegisterNetworkMonster(monster, catalogId))
                {
                    Extensions.Despawn(monster.gameObject);
                    continue;
                }

                _spawnedMonsters.Add(new SpawnedMonsterRecord(monster, rule, slotCost));
            }
        }
        catch (System.OperationCanceledException)
        {
            // 월드 재생성 또는 오브젝트 파괴에 따른 정상 취소입니다.
        }
        finally
        {
            _pendingSlotCost = Mathf.Max(0, _pendingSlotCost - reservedCost);
            state.IsSpawning = false;
        }
    }

    private bool TryFindSpawnPosition(
        Player sourcePlayer,
        SpawnRuleSet sourceRuleSet,
        MonsterSpawnRule rule,
        out Vector3 spawnPosition)
    {
        spawnPosition = default;

        float minDistance = Mathf.Max(0f, rule.minDistanceFromPlayer);
        float maxDistance = Mathf.Max(minDistance, rule.maxDistanceFromPlayer);
        float minDistanceSqr = minDistance * minDistance;
        Vector3 playerPosition = sourcePlayer.transform.position;

        for (int attempt = 0; attempt < Mathf.Max(1, _settings.MaxPositionAttempts); attempt++)
        {
            Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
                continue;

            float radius = Mathf.Sqrt(UnityEngine.Random.Range(
                minDistance * minDistance,
                maxDistance * maxDistance));

            float worldX = playerPosition.x + direction.x * radius;
            float worldZ = playerPosition.z + direction.y * radius;
            int tileX = Mathf.FloorToInt(worldX);
            int tileZ = Mathf.FloorToInt(worldZ);

            int regionIndex = _logicData.GetRegionAt(tileX, tileZ);
            if (!TryGetBiomeData(regionIndex, out BiomeData candidateBiome))
                continue;

            if (candidateBiome.BiomeSpawnRule != sourceRuleSet)
                continue;

            Vector2Int chunkCoord = _logicData.GetChunkCoord(tileX, tileZ);
            if (!_activeChunkCoords.Contains(chunkCoord))
                continue;

            Vector3 candidate = new Vector3(
                worldX,
                _logicData.GetHeightAt(tileX, tileZ) + _settings.SpawnHeightOffset,
                worldZ);

            if (IsTooCloseToAnyPlayer(candidate, minDistanceSqr))
                continue;

            if (_settings.SpawnBlockingMask.value != 0 && _settings.CollisionCheckRadius > 0f)
            {
                Vector3 checkCenter = candidate + Vector3.up * _settings.CollisionCheckRadius;
                if (Physics.CheckSphere(
                    checkCenter,
                    _settings.CollisionCheckRadius,
                    _settings.SpawnBlockingMask,
                    QueryTriggerInteraction.Ignore))
                {
                    continue;
                }
            }

            spawnPosition = candidate;
            return true;
        }

        return false;
    }

    private bool TryGetSpawnRuleSetAt(Vector3 worldPosition, out SpawnRuleSet ruleSet)
    {
        ruleSet = null;

        int tileX = Mathf.FloorToInt(worldPosition.x);
        int tileZ = Mathf.FloorToInt(worldPosition.z);
        int regionIndex = _logicData.GetRegionAt(tileX, tileZ);

        if (!TryGetBiomeData(regionIndex, out BiomeData biomeData))
            return false;

        ruleSet = biomeData.BiomeSpawnRule;
        return ruleSet != null;
    }

    private bool TryGetBiomeData(int regionIndex, out BiomeData biomeData)
    {
        biomeData = null;

        if (regionIndex < 0 || _graphData?.Nodes == null || regionIndex >= _graphData.Nodes.Count)
            return false;

        Node node = _graphData.Nodes[regionIndex];
        biomeData = node?.BiomeData;
        return biomeData != null;
    }

    private bool IsTooCloseToAnyPlayer(Vector3 position, float minDistanceSqr)
    {
        for (int i = 0; i < _players.Count; i++)
        {
            Player player = _players[i];
            if (player == null || !player.isActiveAndEnabled)
                continue;

            Vector3 delta = player.transform.position - position;
            delta.y = 0f;
            if (delta.sqrMagnitude < minDistanceSqr)
                return true;
        }

        return false;
    }
    /// <summary>
    ///  업데이트 루프에서 스폰된 몬스터를 순회하며 플레이어와의 거리와 체류 시간을 확인하고 
    ///  이후 필요에 따라 DespawnOwnedMonster를 호출합니다.
    /// </summary>
    /// <param name="deltaTime"></param>
    private void UpdateSpawnedMonsters(float deltaTime)
    {
        float despawnDistance = Mathf.Max(0f, _settings.DespawnDistanceFromPlayers);
        float despawnDistanceSqr = despawnDistance * despawnDistance;

        for (int i = _spawnedMonsters.Count - 1; i >= 0; i--)
        {
            SpawnedMonsterRecord record = _spawnedMonsters[i];
            Monster monster = record.Monster;

            if (monster == null || !monster.isActiveAndEnabled)
            {
                _spawnedMonsters.RemoveAt(i);
                continue;
            }

            if (IsWithinAnyPlayerDistance(monster.transform.position, despawnDistanceSqr))
            {
                record.OutOfRangeTime = 0f;
                continue;
            }

            record.OutOfRangeTime += deltaTime;
            if (record.OutOfRangeTime < Mathf.Max(0f, record.Rule.stayDuration))
                continue;

            DespawnOwnedMonster(record);
            _spawnedMonsters.RemoveAt(i);
        }
    }

    private bool IsWithinAnyPlayerDistance(Vector3 position, float distanceSqr)
    {
        for (int i = 0; i < _players.Count; i++)
        {
            Player player = _players[i];
            if (player == null || !player.isActiveAndEnabled)
                continue;

            Vector3 delta = player.transform.position - position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= distanceSqr)
                return true;
        }

        return false;
    }

    private int GetAvailableSlotBudget()
    {
        int usedSlots = _pendingSlotCost;
        for (int i = 0; i < _spawnedMonsters.Count; i++)
        {
            SpawnedMonsterRecord record = _spawnedMonsters[i];
            if (record.Monster != null && record.Monster.isActiveAndEnabled)
                usedSlots += record.SlotCost;
        }

        return Mathf.Max(0, _settings.MaxSpawnSlots - usedSlots);
    }

    private static int GetSlotCost(MonsterSpawnRule rule)
    {
        // 0 비용은 무제한 스폰으로 이어질 수 있으므로 런타임에서는 최소 1로 취급합니다.
        return Mathf.Max(1, rule.slotCost);
    }

    private static bool TryRegisterNetworkMonster(Monster monster, byte catalogId)
    {
#if PHOTON_FUSION
        bool isMultiplayer = Main.Network != null && Main.Network.IsInRoom;
        if (!isMultiplayer)
            return true;

        NetworkMonsterDirector director = NetworkMonsterDirector.Instance;
        return director != null && director.RegisterMonster(monster, catalogId) >= 0;
#else
        return true;
#endif
    }

    private static void UnregisterNetworkMonster(Monster monster)
    {
#if PHOTON_FUSION
        NetworkMonsterDirector.Instance?.UnregisterMonster(monster);
#endif
    }

    private void DespawnOwnedMonster(SpawnedMonsterRecord record)
    {
        Monster monster = record.Monster;
        if (monster == null)
            return;

        UnregisterNetworkMonster(monster);
        Extensions.Despawn(monster.gameObject);
    }

    private void DespawnAllOwnedMonsters()
    {
        for (int i = _spawnedMonsters.Count - 1; i >= 0; i--)
            DespawnOwnedMonster(_spawnedMonsters[i]);

        _spawnedMonsters.Clear();
    }

    private void OnDestroy()
    {
        // 씬 종료 중에는 풀 매니저의 파괴 순서를 보장할 수 없으므로 구독과 추적만 정리합니다.
        Shutdown(despawnOwnedMonsters: false);
    }

    private sealed class RuleRuntimeState
    {
        public float Cooldown;
        public bool IsSpawning;
    }

    private sealed class SpawnedMonsterRecord
    {
        public readonly Monster Monster;
        public readonly MonsterSpawnRule Rule;
        public readonly int SlotCost;
        public float OutOfRangeTime;

        public SpawnedMonsterRecord(Monster monster, MonsterSpawnRule rule, int slotCost)
        {
            Monster = monster;
            Rule = rule;
            SlotCost = slotCost;
        }
    }
}
