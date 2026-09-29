using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class WorldSaveTests
{
    [Test]
    public void ApplyChunkChanges_RestoresUnloadedChunk_AndLeavesExpiredResourceAvailable()
    {
        object logic = Activator.CreateInstance(FindType("WorldLogicData"), new Vector2Int(128, 128), 16);
        object distant = Call(logic, "GetOrCreateChunk", new Vector2Int(6, 6));
        AddPlacement(distant, 123);
        AddPlacement(distant, 456);
        object data = Create("WorldSaveData");
        Set(data, "totalSeconds", 100f);
        AddDestroyed(data, 123, 150f);
        AddDestroyed(data, 456, 90f);

        FindType("WorldSaveAdapter").GetMethod("ApplyChunkChanges").Invoke(null, new[] { data, logic });

        Assert.That(Call(distant, "IsObjectDestroyed", 123), Is.True);
        Assert.That(Call(distant, "IsObjectDestroyed", 456), Is.False);
        Assert.That(((IDictionary)Get(distant, "DestroyedObjects"))[123], Is.EqualTo(150f));
    }

    [Test]
    public void ApplyChunkChanges_RejectsMissingPlacement_InsteadOfSilentlyLosingProgress()
    {
        object logic = Activator.CreateInstance(FindType("WorldLogicData"), new Vector2Int(16, 16), 16);
        object data = Create("WorldSaveData");
        AddDestroyed(data, 999, 150f);
        var error = Assert.Throws<TargetInvocationException>(() =>
            FindType("WorldSaveAdapter").GetMethod("ApplyChunkChanges").Invoke(null, new[] { data, logic }));
        Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void JsonRoundTrip_PreservesDestroyedIdsAndRespawnDeadline()
    {
        object data = Create("WorldSaveData");
        Set(data, "seed", -4857);
        Set(data, "totalSeconds", 2891.5f);
        AddDestroyed(data, -19383, 3333.25f);
        object restored = JsonUtility.FromJson(JsonUtility.ToJson(data), data.GetType());
        Assert.That(Get(restored, "seed"), Is.EqualTo(-4857));
        Assert.That(Get(restored, "totalSeconds"), Is.EqualTo(2891.5f));
        object entry = ((IList)Get(restored, "destroyedObjects"))[0];
        Assert.That(Get(entry, "instanceId"), Is.EqualTo(-19383));
        Assert.That(Get(entry, "respawnTime"), Is.EqualTo(3333.25f));
    }

    [Test]
    public void Validate_RejectsDuplicateDestroyedIds()
    {
        object data = Create("WorldSaveData");
        AddDestroyed(data, 1, 10f);
        AddDestroyed(data, 1, 20f);
        var error = Assert.Throws<TargetInvocationException>(() =>
            FindType("WorldSaveAdapter").GetMethod("Validate").Invoke(null, new[] { data }));
        Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void JsonRoundTrip_PreservesInitialDeathAndSpawnDeadlines()
    {
        object world = Create("WorldSaveData");
        object spawns = Get(world, "monsterSpawns");
        object initial = Create("InitialMonsterSaveData");
        Set(initial, "id", 701);
        Set(initial, "dead", true);
        ((IList)Get(spawns, "initialMonsters")).Add(initial);
        object source = Create("SpawnSourceSaveData");
        Set(source, "id", 821);
        Set(source, "nextSpawnTime", 1500f);
        ((IList)Get(spawns, "sources")).Add(source);
        object periodic = Create("PeriodicSpawnSaveData");
        Set(periodic, "ruleId", "night-wave");
        Set(periodic, "nextSpawnTime", 1800f);
        ((IList)Get(spawns, "periodic")).Add(periodic);
        object member = Create("SpawnedMonsterSaveData");
        Set(member, "sourceId", 821);
        Set(member, "position", new Vector3(10f, 2f, 20f));
        Set(member, "hp", 27f);
        ((IList)Get(spawns, "members")).Add(member);

        object restored = JsonUtility.FromJson(JsonUtility.ToJson(world), world.GetType());
        object restoredSpawns = Get(restored, "monsterSpawns");
        Assert.That(Get(((IList)Get(restoredSpawns, "initialMonsters"))[0], "dead"), Is.True);
        Assert.That(Get(((IList)Get(restoredSpawns, "sources"))[0], "nextSpawnTime"), Is.EqualTo(1500f));
        Assert.That(Get(((IList)Get(restoredSpawns, "periodic"))[0], "ruleId"), Is.EqualTo("night-wave"));
        Assert.That(Get(((IList)Get(restoredSpawns, "members"))[0], "hp"), Is.EqualTo(27f));
    }

    [Test]
    public void Validate_RejectsDuplicateInitialMonsterIds()
    {
        object world = Create("WorldSaveData");
        object spawns = Get(world, "monsterSpawns");
        foreach (int _ in new[] { 0, 1 })
        {
            object entry = Create("InitialMonsterSaveData");
            Set(entry, "id", 701);
            Set(entry, "dead", true);
            ((IList)Get(spawns, "initialMonsters")).Add(entry);
        }

        var error = Assert.Throws<TargetInvocationException>(() =>
            FindType("WorldSaveAdapter").GetMethod("Validate").Invoke(null, new[] { world }));
        Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void WorldMonsterPlacementId_IsStableAndResolvesCollisions()
    {
        MethodInfo create = FindType("WorldMonsterPlacementId").GetMethod("Create",
            BindingFlags.Public | BindingFlags.Static);
        var firstWorld = new HashSet<int>();
        var secondWorld = new HashSet<int>();
        object[] argsA = { 12345, "Mischief", new Vector2Int(8, 12), 0, firstWorld };
        object[] argsB = { 12345, "Mischief", new Vector2Int(8, 12), 0, secondWorld };

        int first = (int)create.Invoke(null, argsA);
        int replay = (int)create.Invoke(null, argsB);
        int collision = (int)create.Invoke(null, argsA);

        Assert.That(first, Is.Not.Zero);
        Assert.That(replay, Is.EqualTo(first));
        Assert.That(collision, Is.Not.EqualTo(first));
    }

    private static void AddPlacement(object chunk, int id)
    {
        object placement = Create("DisposeData");
        Set(placement, "instanceId", id);
        ((IList)chunk.GetType().GetProperty("DisposeDatas").GetValue(chunk)).Add(placement);
    }

    private static void AddDestroyed(object data, int id, float time)
    {
        object entry = Create("DestroyedObjectSaveData");
        Set(entry, "instanceId", id);
        Set(entry, "respawnTime", time);
        ((IList)Get(data, "destroyedObjects")).Add(entry);
    }

    private static object Create(string name) => Activator.CreateInstance(FindType(name));
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);
    private static object Get(object target, string field) => target.GetType().GetField(field).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field).SetValue(target, value);
    private static Type FindType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(name);
            if (type != null) return type;
        }
        throw new InvalidOperationException($"타입을 찾을 수 없습니다: {name}");
    }
}
