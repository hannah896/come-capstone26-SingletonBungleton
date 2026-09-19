using System;
using System.Collections;
using System.Reflection;
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
