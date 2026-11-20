using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 플레이어 주변 바이옴의 SpawnRuleSet을 사용해 런타임 몬스터·동물 자연 스폰을 관리합니다.
///
/// 몬스터는 MonsterCatalog / Monster.SpawnAsync / NetworkMonsterDirector를,
/// 동물은 AnimalCatalog / Animal.SpawnAsync / NetworkAnimalDirector를 사용하며
/// 스폰 슬롯 예산은 몬스터와 동물을 별도로 집계합니다.
/// </summary>
public sealed class DynamicSpawnDirector : MonoBehaviour
{
    private DynamicSpawnSettings _settings;
    private WorldLogicData _logicData;
    private WorldGraphData _graphData;
    private WorldChunkDirector _chunkDirector;

    private readonly List<Player> _players = new();
    private readonly HashSet<Vector2Int> _activeChunkCoords = new();
    private readonly List<SpawnedMobRecord> _spawnedMobs = new();
    private readonly Dictionary<Player, Dictionary<NatureSpawnRule, RuleRuntimeState>> _ruleStates = new();

    private CancellationTokenSource _spawnCts;
    private float _targetRefreshCooldown;
    private int _pendingMonsterSlotCost;
    private int _pendingAnimalSlotCost;
    private bool _isInitialized;

    public bool IsInitialized => _isInitialized;
    public int SpawnedMonsterCount => _spawnedMobs.Count;

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
            _spawnedMobs.Clear();

        _players.Clear();
        _activeChunkCoords.Clear();
        _ruleStates.Clear();
        _pendingMonsterSlotCost = 0;
        _pendingAnimalSlotCost = 0;

        _logicData = null;
        _graphData = null;
        _chunkDirector = null;
        _settings = null;
        _isInitialized = false;
    }

    private void OnGameUpdate(float deltaTime)
    {
        if (!_isInitialized || !Monster.IsSimulatedPeer ||
            (Main.Save != null && (Main.Save.IsRestoring || Main.Save.IsCapturing)))
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

            if (ruleSet.NatureSpawnRules == null)
                continue;

            for (int ruleIndex = 0; ruleIndex < ruleSet.NatureSpawnRules.Count; ruleIndex++)
            {
                NatureSpawnRule rule = ruleSet.NatureSpawnRules[ruleIndex];
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

    private bool CanAttemptSpawn(NatureSpawnRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.monsterKey))
            return false;

        bool isAnimal = IsAnimalKey(rule.monsterKey);
        if (GetAvailableSlotBudget(isAnimal) < GetSlotCost(rule))
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

    private RuleRuntimeState GetRuleState(Player player, NatureSpawnRule rule)
    {
        if (!_ruleStates.TryGetValue(player, out Dictionary<NatureSpawnRule, RuleRuntimeState> playerStates))
        {
            playerStates = new Dictionary<NatureSpawnRule, RuleRuntimeState>();
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
        NatureSpawnRule rule,
        RuleRuntimeState state,
        CancellationToken cancellationToken)
    {
        int reservedCost = 0;
        bool isAnimal = false;

        try
        {
            if (sourcePlayer == null)
                return;

            if (!TryResolveCatalog(
                rule.monsterKey,
                out isAnimal,
                out byte catalogId,
                out MonsterCatalog.Entry monsterEntry,
                out AnimalCatalog.Entry animalEntry))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    $"[DynamicSpawnDirector] '{rule.monsterKey}'가 MonsterCatalog/AnimalCatalog에 없습니다.",
                    this);
#endif
                return;
            }

            int slotCost = GetSlotCost(rule);
            int minCount = Mathf.Max(0, rule.minSpawnCount);
            int maxCount = Mathf.Max(minCount, rule.maxSpawnCount);
            int desiredCount = UnityEngine.Random.Range(minCount, maxCount + 1);
            int affordableCount = GetAvailableSlotBudget(isAnimal) / slotCost;
            desiredCount = Mathf.Min(desiredCount, affordableCount);

            if (desiredCount <= 0)
                return;

            reservedCost = desiredCount * slotCost;
            if (isAnimal) _pendingAnimalSlotCost += reservedCost;
            else _pendingMonsterSlotCost += reservedCost;

            for (int i = 0; i < desiredCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (sourcePlayer == null || !sourcePlayer.isActiveAndEnabled)
                    break;

                if (!TryFindSpawnPosition(sourcePlayer, sourceRuleSet, isAnimal, out Vector3 spawnPosition))
                    continue;

                Mob spawnedMob;
                if (isAnimal)
                {
                    Animal animal = await Animal.SpawnAsync(
                        animalEntry.addressableKey,
                        animalEntry.statData,
                        spawnPosition);
                    if (animal == null) continue;

                    if (cancellationToken.IsCancellationRequested || !_isInitialized)
                    {
                        Extensions.Despawn(animal.gameObject);
                        cancellationToken.ThrowIfCancellationRequested();
                        continue;
                    }

                    if (!TryRegisterNetworkAnimal(animal, catalogId))
                    {
                        Extensions.Despawn(animal.gameObject);
                        continue;
                    }

                    spawnedMob = animal;
                }
                else
                {
                    Monster monster = await Monster.SpawnAsync(
                        monsterEntry.addressableKey,
                        monsterEntry.statData,
                        spawnPosition);
                    if (monster == null) continue;

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

                    spawnedMob = monster;
                }

                _spawnedMobs.Add(new SpawnedMobRecord(spawnedMob, rule, slotCost, isAnimal));
            }
        }
        catch (System.OperationCanceledException)
        {
            // 월드 재생성 또는 오브젝트 파괴에 따른 정상 취소입니다.
        }
        finally
        {
            if (isAnimal) _pendingAnimalSlotCost = Mathf.Max(0, _pendingAnimalSlotCost - reservedCost);
            else _pendingMonsterSlotCost = Mathf.Max(0, _pendingMonsterSlotCost - reservedCost);
            state.IsSpawning = false;
        }
    }

    private bool TryFindSpawnPosition(
        Player sourcePlayer,
        SpawnRuleSet sourceRuleSet,
        bool isAnimal,
        out Vector3 spawnPosition)
    {
        spawnPosition = default;

        float minDistance = Mathf.Max(0f, _settings.MinDistanceFromPlayer);
        float maxDistance = Mathf.Max(minDistance, _settings.MaxDistanceFromPlayer);
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

            // 활성 청크 검사는 이 피어가 청크를 스트리밍하는 로컬 플레이어에게만 적용한다.
            // 멀티에서 호스트는 원격 플레이어 주변 청크를 로드하지 않으므로, 검사하면 클라 주변엔 영영 스폰되지 않는다.
            // (몬스터는 transform, 동물은 논리 지형 폴백 이동/넉백으로 청크 미로드 구역에서도 동작한다)
            Vector2Int chunkCoord = _logicData.GetChunkCoord(tileX, tileZ);
            if (sourcePlayer.IsLocalPlayer && !_activeChunkCoords.Contains(chunkCoord))
                continue;

            Vector3 candidate = new Vector3(
                worldX,
                _logicData.SampleHeight(worldX, worldZ) + _settings.SpawnHeightOffset,
                worldZ);

            if (WorldSpawnArea.Contains(_logicData, candidate, _settings.CollisionCheckRadius))
                continue;

            if (IsTooCloseToAnyPlayer(candidate, minDistanceSqr))
                continue;

            // 동물은 이미 스폰된 다른 동물과 최소 간격을 둔다 (한곳에 몰려 나오지 않고 길 가다 몇 마리씩 만나도록)
            if (isAnimal && IsTooCloseToSpawnedAnimal(candidate, MinAnimalSpacing * MinAnimalSpacing))
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

    // 동물끼리 스폰 위치 최소 간격(m). 지역이 좁아 후보가 한 구역에 몰려도 뭉치지 않게 한다. (제안값)
    private const float MinAnimalSpacing = 30f;

    private bool IsTooCloseToSpawnedAnimal(Vector3 position, float minDistanceSqr)
    {
        for (int i = 0; i < _spawnedMobs.Count; i++)
        {
            SpawnedMobRecord record = _spawnedMobs[i];
            if (!record.IsAnimal || record.Mob == null)
                continue;

            Vector3 delta = record.Mob.transform.position - position;
            delta.y = 0f;
            if (delta.sqrMagnitude < minDistanceSqr)
                return true;
        }

        return false;
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
    ///  업데이트 루프에서 스폰된 몬스터·동물을 순회하며 플레이어와의 거리와 체류 시간을 확인하고 
    ///  이후 필요에 따라 DespawnOwnedMob을 호출합니다.
    /// </summary>
    /// <param name="deltaTime"></param>
    private void UpdateSpawnedMonsters(float deltaTime)
    {
        float despawnDistance = Mathf.Max(0f, _settings.DespawnDistanceFromPlayers);
        float despawnDistanceSqr = despawnDistance * despawnDistance;

        for (int i = _spawnedMobs.Count - 1; i >= 0; i--)
        {
            SpawnedMobRecord record = _spawnedMobs[i];
            Mob mob = record.Mob;

            if (mob == null || !mob.isActiveAndEnabled)
            {
                _spawnedMobs.RemoveAt(i);
                continue;
            }

            if (IsWithinAnyPlayerDistance(mob.transform.position, despawnDistanceSqr))
            {
                record.OutOfRangeTime = 0f;
                continue;
            }

            record.OutOfRangeTime += deltaTime;
            if (record.OutOfRangeTime < Mathf.Max(0f, record.Rule.stayDuration))
                continue;

            DespawnOwnedMob(record);
            _spawnedMobs.RemoveAt(i);
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

    private int GetAvailableSlotBudget(bool isAnimal)
    {
        int usedSlots = isAnimal ? _pendingAnimalSlotCost : _pendingMonsterSlotCost;
        for (int i = 0; i < _spawnedMobs.Count; i++)
        {
            SpawnedMobRecord record = _spawnedMobs[i];
            if (record.IsAnimal == isAnimal && record.Mob != null && record.Mob.isActiveAndEnabled)
                usedSlots += record.SlotCost;
        }

        int maxSlots = isAnimal ? _settings.MaxAnimalSpawnSlots : _settings.MaxSpawnSlots;
        return Mathf.Max(0, maxSlots - usedSlots);
    }

    private static bool IsAnimalKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;

        MonsterCatalog monsterCatalog = MonsterCatalog.Instance;
        if (monsterCatalog != null && monsterCatalog.GetCatalogId(key) != 0)
            return false;

        AnimalCatalog animalCatalog = AnimalCatalog.Instance;
        return animalCatalog != null && animalCatalog.GetCatalogId(key) != 0;
    }

    private static bool TryResolveCatalog(
        string key,
        out bool isAnimal,
        out byte catalogId,
        out MonsterCatalog.Entry monsterEntry,
        out AnimalCatalog.Entry animalEntry)
    {
        isAnimal = false;
        catalogId = 0;
        monsterEntry = null;
        animalEntry = null;

        if (string.IsNullOrWhiteSpace(key))
            return false;

        MonsterCatalog monsterCatalog = MonsterCatalog.Instance;
        if (monsterCatalog != null)
        {
            byte id = monsterCatalog.GetCatalogId(key);
            MonsterCatalog.Entry entry = monsterCatalog.GetEntry(id);
            if (id != 0 && entry != null && !string.IsNullOrWhiteSpace(entry.addressableKey))
            {
                catalogId = id;
                monsterEntry = entry;
                return true;
            }
        }

        AnimalCatalog animalCatalog = AnimalCatalog.Instance;
        if (animalCatalog != null)
        {
            byte id = animalCatalog.GetCatalogId(key);
            AnimalCatalog.Entry entry = animalCatalog.GetEntry(id);
            if (id != 0 && entry != null && !string.IsNullOrWhiteSpace(entry.addressableKey))
            {
                isAnimal = true;
                catalogId = id;
                animalEntry = entry;
                return true;
            }
        }

        return false;
    }

    private static int GetSlotCost(NatureSpawnRule rule)
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

    private static bool TryRegisterNetworkAnimal(Animal animal, byte catalogId)
    {
#if PHOTON_FUSION
        bool isMultiplayer = Main.Network != null && Main.Network.IsInRoom;
        if (!isMultiplayer)
            return true;

        NetworkAnimalDirector director = NetworkAnimalDirector.Instance;
        return director != null && director.RegisterAnimal(animal, catalogId) >= 0;
#else
        return true;
#endif
    }

    private static void UnregisterNetworkAnimal(Animal animal)
    {
#if PHOTON_FUSION
        NetworkAnimalDirector.Instance?.UnregisterAnimal(animal);
#endif
    }

    private void DespawnOwnedMob(SpawnedMobRecord record)
    {
        Mob mob = record.Mob;
        if (mob == null)
            return;

        if (mob is Animal animal)
            UnregisterNetworkAnimal(animal);
        else if (mob is Monster monster)
            UnregisterNetworkMonster(monster);

        Extensions.Despawn(mob.gameObject);
    }

    private void DespawnAllOwnedMonsters()
    {
        for (int i = _spawnedMobs.Count - 1; i >= 0; i--)
            DespawnOwnedMob(_spawnedMobs[i]);

        _spawnedMobs.Clear();
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

    private sealed class SpawnedMobRecord
    {
        public readonly Mob Mob;
        public readonly NatureSpawnRule Rule;
        public readonly int SlotCost;
        public readonly bool IsAnimal;
        public float OutOfRangeTime;

        public SpawnedMobRecord(Mob mob, NatureSpawnRule rule, int slotCost, bool isAnimal)
        {
            Mob = mob;
            Rule = rule;
            SlotCost = slotCost;
            IsAnimal = isAnimal;
        }
    }
}
