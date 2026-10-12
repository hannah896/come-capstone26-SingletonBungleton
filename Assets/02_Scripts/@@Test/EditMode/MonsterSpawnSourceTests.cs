using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class MonsterSpawnSourceTests
{
    private readonly List<UnityEngine.Object> _created = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void GetDefinition_CopiesPlacementSettings_WithoutCloningSharedSpawnRule()
    {
        Component source = CreateSource();
        ScriptableObject rule = CreateAsset("SourceSpawnRule");
        Set(source, "spawnRule", rule);
        object settings = Get(source, "placementSettings");
        Set(settings, "placementRadius", 4f);
        Set(settings, "maxHeightDifference", 1.5f);
        Set(settings, "minDistanceFromWorldStart", 45f);

        object first = Call(source, "GetDefinition");
        object second = Call(source, "GetDefinition");
        object snapshot = GetProperty(first, "PlacementSettings");

        Assert.That(GetProperty(first, "SpawnRule"), Is.SameAs(rule));
        Assert.That(snapshot, Is.Not.SameAs(settings));
        Assert.That(snapshot, Is.Not.SameAs(GetProperty(second, "PlacementSettings")));
        Set(settings, "placementRadius", 100f);
        Set(settings, "maxHeightDifference", 100f);
        Set(settings, "minDistanceFromWorldStart", 100f);
        Assert.That(Get(snapshot, "placementRadius"), Is.EqualTo(4f));
        Assert.That(Get(snapshot, "maxHeightDifference"), Is.EqualTo(1.5f));
        Assert.That(Get(snapshot, "minDistanceFromWorldStart"), Is.EqualTo(45f));
    }

    [Test]
    public void GetDefinition_ClampsInvalidPlacement_AndSuppliesDefaultsForMissingSettings()
    {
        Component source = CreateSource();
        Assert.That(Call(source, "GetDefinition"), Is.Null, "규칙이 없는 프리팹은 몬스터 생성 소스로 등록하지 않습니다.");
        Set(source, "spawnRule", CreateAsset("SourceSpawnRule"));
        object settings = Get(source, "placementSettings");
        Set(settings, "placementRadius", -3f);
        Set(settings, "maxHeightDifference", -2f);
        Set(settings, "minDistanceFromWorldStart", -30f);
        object snapshot = GetProperty(Call(source, "GetDefinition"), "PlacementSettings");
        Assert.That(Get(snapshot, "placementRadius"), Is.EqualTo(0f));
        Assert.That(Get(snapshot, "maxHeightDifference"), Is.EqualTo(0f));
        Assert.That(Get(snapshot, "minDistanceFromWorldStart"), Is.EqualTo(0f));
        Assert.That(Get(settings, "placementRadius"), Is.EqualTo(-3f), "런타임 정규화가 프리팹 에셋 설정을 수정하면 안 됩니다.");

        Set(source, "placementSettings", null);
        snapshot = GetProperty(Call(source, "GetDefinition"), "PlacementSettings");
        Assert.That(Get(snapshot, "placementRadius"), Is.EqualTo(3f));
        Assert.That(Get(snapshot, "maxHeightDifference"), Is.EqualTo(2f));
        Assert.That(Get(snapshot, "minDistanceFromWorldStart"), Is.EqualTo(30f));
    }

    [TestCase("TreeantMinionSpawner", "Source_TreantMinion")]
    [TestCase("TreeantTreeSpawner", "Source_TreantTree")]
    public void ExistingTreantPrefabs_HaveRootMetadataAndARegisteredMonsterRule(string prefabName, string ruleName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/03_Prefabs/Objects/Spawner/{prefabName}.prefab");
        UnityEngine.Object rule = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
            $"Assets/05_Datas/WorldData_KGB/SpawnRuleSets/Source/{ruleName}.asset");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(rule, Is.Not.Null);
        Component source = prefab.GetComponent(FindType("MonsterSpawnSource"));
        Assert.That(source, Is.Not.Null, "청크 인스턴스를 만들기 전 프리팹 루트에서 설정을 읽어야 합니다.");
        Assert.That(Get(source, "spawnRule"), Is.SameAs(rule));
        string monsterKey = (string)Get(rule, "monsterKey");
        Assert.That(string.IsNullOrWhiteSpace(monsterKey), Is.False);
        UnityEngine.Object catalog = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Resources/ConfigData/MonsterCatalog.asset");
        Assert.That(catalog, Is.Not.Null);
        Assert.That((byte)Call(catalog, "GetCatalogId", monsterKey), Is.GreaterThan((byte)0));
        object placement = Get(source, "placementSettings");
        object snapshot = GetProperty(Call(source, "GetDefinition"), "PlacementSettings");
        Assert.That(snapshot, Is.Not.SameAs(placement));
        foreach (string field in new[] { "placementRadius", "maxHeightDifference", "minDistanceFromWorldStart" })
        {
            Assert.That((float)Get(placement, field), Is.GreaterThanOrEqualTo(0f));
            Assert.That(Get(snapshot, field), Is.EqualTo(Get(placement, field)));
        }
    }

    [Test]
    public void PlacementRulesAndSpawnRules_KeepTheirSeparateSerializationContracts()
    {
        Assert.That(FindType("CountDisposeRule").GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Select(field => field.Name), Is.EquivalentTo(new[] { "ruleName", "prefabKey", "minCount", "maxCount" }));
        Assert.That(FindType("DisposeData").GetField("monsterSpawnRule"), Is.Null);
        Type sourceRule = FindType("SourceSpawnRule");
        foreach (string field in new[] { "placementRadius", "maxHeightDifference", "minDistanceFromWorldStart" })
            Assert.That(sourceRule.GetField(field), Is.Null, $"{field}는 프리팹의 배치 설정에 속합니다.");
    }

    [UnityTest]
    public IEnumerator SpawnerDisposer_UsesPrefabMetadata_WithoutInstantiatingPrefab_AndKeepsStableSourceIds()
    {
        object definition = CreateDefinition(2f, 1f, 0f);
        object first = CreatePlacementContext(new Vector2Int(12, 12));
        int instanceCount = Resources.FindObjectsOfTypeAll(FindType("MonsterSpawnSource")).Length;
        yield return RunPlacement(first, definition);

        object placements = Get(first, "DisposeData");
        IList objects = (IList)Get(placements, "ObjectDisposes");
        IList sources = (IList)Get(placements, "MonsterSpawnSources");
        Assert.That(objects.Count, Is.EqualTo(1));
        Assert.That(sources.Count, Is.EqualTo(1));
        Assert.That(Get(sources[0], "Placement"), Is.SameAs(objects[0]));
        Assert.That(Get(sources[0], "Definition"), Is.SameAs(definition));
        Assert.That(Resources.FindObjectsOfTypeAll(FindType("MonsterSpawnSource")).Length, Is.EqualTo(instanceCount),
            "논리 배치는 프리팹 인스턴스나 OnEnable 등록에 의존하면 안 됩니다.");
        bool[,] occupied = (bool[,])GetProperty(Get(first, "LogicData"), "OccupiedWorld");
        for (int x = 10; x <= 14; x++)
            for (int z = 10; z <= 14; z++) Assert.That(occupied[x, z], Is.True);
        Assert.That(occupied[9, 12], Is.False);

        object second = CreatePlacementContext(new Vector2Int(12, 12));
        yield return RunPlacement(second, definition);
        object secondPlacement = ((IList)Get(Get(second, "DisposeData"), "ObjectDisposes"))[0];
        Assert.That(Get(objects[0], "instanceId"), Is.Not.EqualTo(0));
        Assert.That(Get(secondPlacement, "instanceId"), Is.EqualTo(Get(objects[0], "instanceId")),
            "같은 시드와 배치 조건은 이전 저장본의 sourceId를 그대로 재생성해야 합니다.");
    }

    [UnityTest]
    public IEnumerator SpawnerDisposer_RejectsFootprintOverlapHeightDifferenceAndStartDistance()
    {
        for (int condition = 0; condition < 3; condition++)
        {
            object context = CreatePlacementContext(new Vector2Int(12, 12));
            object logic = Get(context, "LogicData");
            if (condition == 0) ((bool[,])GetProperty(logic, "OccupiedWorld"))[13, 12] = true;
            if (condition == 1) ((float[,])GetProperty(logic, "HeightWorld"))[13, 12] = 2f;
            if (condition == 2) SetProperty(logic, "SpawnTile", new Vector2Int(10, 12));
            yield return RunPlacement(context, CreateDefinition(2f, 1f, condition == 2 ? 3f : 0f));
            Assert.That(((IList)Get(Get(context, "DisposeData"), "ObjectDisposes")).Count, Is.EqualTo(0));
            Assert.That(((IList)Get(Get(context, "DisposeData"), "MonsterSpawnSources")).Count, Is.EqualTo(0),
                "배치가 거절되면 런타임 생성 소스도 등록하면 안 됩니다.");
        }
    }

    [Test]
    public void SourceLedger_RegistersWithoutVisualInstances_AndRestoresExistingSaveIdsAndDeadlines()
    {
        object logic = Activator.CreateInstance(FindType("WorldLogicData"), new Vector2Int(32, 32), 8);
        object placements = Create("WorldDisposeData");
        object placement = Create("DisposeData");
        Set(placement, "instanceId", 31415);
        Set(placement, "tilePosition", new Vector2Int(12, 12));
        object definition = CreateDefinition(3f, 2f, 30f);
        object rule = GetProperty(definition, "SpawnRule");
        Set(rule, "spawnImmediately", false);
        Set(rule, "intervalSeconds", 30f);
        object entry = Create("WorldMonsterSpawnSourcePlacement");
        Set(entry, "Placement", placement);
        Set(entry, "Definition", definition);
        ((IList)Get(placements, "MonsterSpawnSources")).Add(entry);
        object chunk = Call(logic, "GetOrCreateChunk", new Vector2Int(1, 1));
        ((IList)GetProperty(chunk, "DisposeDatas")).Add(placement);

        object director = FormatterServices.GetUninitializedObject(FindType("WorldMonsterSpawnDirector"));
        foreach (string collection in new[] { "_initial", "_sources", "_periodic", "_active", "_dormantMembers", "_players" })
        {
            FieldInfo field = PrivateField(director, collection);
            field.SetValue(director, Activator.CreateInstance(field.FieldType));
        }
        PrivateField(director, "_logic").SetValue(director, logic);
        Call(director, "RegisterSources", placements, 120f);
        IDictionary sources = (IDictionary)PrivateField(director, "_sources").GetValue(director);
        Assert.That(sources.Contains(31415), Is.True);
        Assert.That(Get(sources[31415], "Chunk"), Is.SameAs(chunk));
        Assert.That(Get(sources[31415], "NextTime"), Is.EqualTo(150f));

        object captured = Call(director, "Capture");
        object restored = JsonUtility.FromJson(JsonUtility.ToJson(captured), captured.GetType());
        FindType("WorldSaveAdapter").GetMethod("ValidateMonsterSpawns", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new[] { restored });
        object savedSource = ((IList)Get(restored, "sources"))[0];
        Assert.That(Get(savedSource, "id"), Is.EqualTo(31415));
        Assert.That(Get(savedSource, "nextSpawnTime"), Is.EqualTo(150f));
        Set(sources[31415], "NextTime", 999f);
        Call(director, "Restore", restored);
        Assert.That(Get(sources[31415], "NextTime"), Is.EqualTo(150f));

        Call(placements, "Clear");
        Assert.That(((IList)Get(placements, "MonsterSpawnSources")).Count, Is.EqualTo(0));
        Assert.That(sources.Contains(31415), Is.True, "런타임 소스 상태는 배치 목록의 Clear로 수정되지 않습니다.");
    }

    private Component CreateSource()
    {
        var root = new GameObject("MonsterSpawnSource_Test");
        _created.Add(root);
        return root.AddComponent(FindType("MonsterSpawnSource"));
    }

    private ScriptableObject CreateAsset(string name)
    {
        var asset = ScriptableObject.CreateInstance(FindType(name));
        _created.Add(asset);
        return asset;
    }

    private object CreateDefinition(float radius, float height, float startDistance)
    {
        object settings = Create("SpawnerPlacementSettings");
        Set(settings, "placementRadius", radius);
        Set(settings, "maxHeightDifference", height);
        Set(settings, "minDistanceFromWorldStart", startDistance);
        return Activator.CreateInstance(FindType("MonsterSpawnSourceDefinition"), CreateAsset("SourceSpawnRule"), settings);
    }

    private object CreatePlacementContext(Vector2Int tile)
    {
        object context = Create("WorldGenContext");
        ScriptableObject settings = CreateAsset("WorldSettings");
        Set(settings, "WorldSeed", 42);
        object rule = Create("CountDisposeRule");
        Set(rule, "prefabKey", "Source_Test_CachedMetadata");
        Set(rule, "minCount", 1);
        Set(rule, "maxCount", 1);
        ScriptableObject rules = CreateAsset("DisposeRuleSet");
        IList spawnerRules = (IList)Activator.CreateInstance(rules.GetType().GetField("SpawnerRules").FieldType);
        spawnerRules.Add(rule);
        Set(rules, "SpawnerRules", spawnerRules);
        ScriptableObject biome = CreateAsset("BiomeData");
        Set(biome, "BiomeDisposeRule", rules);
        ScriptableObject region = CreateAsset("RegionData");
        Set(region, "BiomeData", biome);
        object node = Create("Node");
        Set(node, "RegionData", region);
        ((IList)Get(node, "OwnedTiles")).Add(tile);
        object graph = Create("WorldGraphData");
        ((IList)GetProperty(graph, "Nodes")).Add(node);
        Set(context, "Settings", settings);
        Set(context, "GraphData", graph);
        Set(context, "LogicData", Activator.CreateInstance(FindType("WorldLogicData"), new Vector2Int(32, 32), 8));
        Set(context, "DisposeData", Create("WorldDisposeData"));
        return context;
    }

    private static IEnumerator RunPlacement(object context, object definition)
    {
        object disposer = Create("SpawnerDisposer");
        Call(disposer, "Initialize", Get(context, "Settings"));
        ((IDictionary)PrivateField(disposer, "_sourceDefinitions").GetValue(disposer))
            .Add("Source_Test_CachedMetadata", definition);
        object task = Call(disposer, "ExecuteAsync", context, CancellationToken.None);
        object awaiter = Call(task, "GetAwaiter");
        int frames = 0;
        while (!(bool)GetProperty(awaiter, "IsCompleted"))
        {
            Assert.That(++frames, Is.LessThan(1000), "캐시된 프리팹 설정의 배치가 완료되지 않았습니다.");
            yield return null;
        }
        Call(awaiter, "GetResult");
    }

    private static object Create(string name) => Activator.CreateInstance(FindType(name));
    private static FieldInfo PrivateField(object target, string name)
        => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static object Call(object target, string name, params object[] args)
        => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    private static object Get(object target, string name) => target.GetType().GetField(name).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name).SetValue(target, value);
    private static object GetProperty(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static void SetProperty(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
    private static Type FindType(string name)
        => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name))
            .First(type => type != null);
}
