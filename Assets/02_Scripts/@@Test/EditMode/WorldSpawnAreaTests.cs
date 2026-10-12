using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class WorldSpawnAreaTests
{
    private readonly List<UnityEngine.Object> created = new();
    private object logic;
    private Type area;

    [SetUp]
    public void SetUp()
    {
        area = FindType("WorldSpawnArea");
        logic = Activator.CreateInstance(FindType("WorldLogicData"), new Vector2Int(40, 40), 8);
        SetProperty(logic, "SpawnTile", new Vector2Int(20, 20));
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
            if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    [TestCase(6f, 0f, true)]
    [TestCase(6.01f, 0f, false)]
    [TestCase(6.5f, 0.5f, true)]
    [TestCase(6.51f, 0.5f, false)]
    public void Contains_ProtectsBoundaryAndFootprintRegardlessOfHeight(float offset, float margin, bool expected)
    {
        Assert.That(Call("Contains", logic, new Vector3(20f + offset, 300f, 20f), margin), Is.EqualTo(expected));
    }

    [Test]
    public void GetCenter_UnassignedSpawnUsesMapCenter()
    {
        SetProperty(logic, "SpawnTile", new Vector2Int(-1, -1));
        Assert.That(Call("GetCenter", logic), Is.EqualTo(new Vector3(20f, 0f, 20f)));
    }

    [Test]
    public void Intersects_BoundsRejectsAnEdgeIntrusionWithRootOutsideRadius()
    {
        Bounds overlap = new Bounds(new Vector3(27f, 200f, 20f), new Vector3(3f, 1f, 1f));
        Bounds clear = new Bounds(new Vector3(28f, 200f, 20f), new Vector3(3f, 1f, 1f));
        Assert.That(Call("Intersects", logic, overlap), Is.True);
        Assert.That(Call("Intersects", logic, clear), Is.False);
    }

    [Test]
    public void Intersects_TransformedBoxColliderWorksBeforePhysicsSync()
    {
        var root = new GameObject("SpawnAreaCollider_Test");
        created.Add(root);
        var child = new GameObject("OffsetCollider");
        child.transform.SetParent(root.transform, false);
        child.transform.localPosition = new Vector3(-2f, 0f, 0f);
        BoxCollider collider = child.AddComponent<BoxCollider>();
        collider.size = new Vector3(2f, 1f, 2f);
        root.transform.position = new Vector3(28f, 0f, 20f);
        root.transform.localScale = new Vector3(1.5f, 1f, 1f);
        Assert.That(Call("Contains", logic, root.transform.position, 0f), Is.False);
        Assert.That(Call("Intersects", logic, root), Is.True);
    }

    [Test]
    public void IntersectsStructure_ConsidersRotationOfLongFootprint()
    {
        ScriptableObject item = CreateItem(new Vector2Int(4, 1));
        Vector3 position = new Vector3(27.5f, 0f, 20f);
        Assert.That(Call("IntersectsStructure", logic, item, position, Quaternion.identity, 1f, null), Is.True);
        Assert.That(Call("IntersectsStructure", logic, item, position, Quaternion.Euler(0f, 90f, 0f), 1f, null), Is.False);
    }

    [Test]
    public void TryRelocateStructure_PreservesHeightAndIsDeterministic()
    {
        ScriptableObject item = CreateItem(Vector2Int.one);
        object[] args = { logic, item, new Vector3(20f, 2f, 20f), Quaternion.identity, Vector3.one, Vector3.zero, 0f };
        Assert.That(Call("TryRelocateStructure", args), Is.True);
        Vector3 moved = (Vector3)args[5];
        Assert.That(moved.y, Is.EqualTo(2f));
        Assert.That(Call("IntersectsStructure", logic, item, moved, Quaternion.identity, 1f, null), Is.False);
        object[] second = { logic, item, new Vector3(20f, 2f, 20f), Quaternion.identity, Vector3.one, Vector3.zero, 0f };
        Assert.That(Call("TryRelocateStructure", second), Is.True);
        Assert.That(second[5], Is.EqualTo(moved));
    }

    [Test]
    public void TryRelocateStructure_NoSuitableGroundReportsFailure()
    {
        var territory = (int[,])GetProperty(logic, "TerritoryWorld");
        for (int x = 0; x < 40; x++)
            for (int z = 0; z < 40; z++) territory[x, z] = -1;
        ScriptableObject item = CreateItem(Vector2Int.one);
        Vector3 original = new Vector3(20f, 2f, 20f);
        object[] args = { logic, item, original, Quaternion.identity, Vector3.one, Vector3.zero, 0f };
        Assert.That(Call("TryRelocateStructure", args), Is.False);
        Assert.That(args[5], Is.EqualTo(original));
    }

    [Test]
    public void SpawnSingleObject_ExcludesVisualButRetainsPlacementIdAcrossChunkUnload()
    {
        object spawner = Activator.CreateInstance(FindType("WorldObjectSpawner"));
        spawner.GetType().GetMethod("Initialize").Invoke(spawner, new[] { logic, null });
        Vector2Int coord = new Vector2Int(2, 2);
        object chunk = logic.GetType().GetMethod("GetOrCreateChunk").Invoke(logic, new object[] { coord });
        object placement = Activator.CreateInstance(FindType("DisposeData"));
        placement.GetType().GetField("instanceId").SetValue(placement, 123);
        placement.GetType().GetField("prefabName").SetValue(placement, "SpawnArea_MustNotLoad_Test");
        placement.GetType().GetField("tilePosition").SetValue(placement, new Vector2Int(20, 20));
        var placements = (System.Collections.IList)GetProperty(chunk, "DisposeDatas");
        placements.Add(placement);
        object task = spawner.GetType().GetMethod("SpawnSingleObjectAsync").Invoke(spawner, new[] { chunk, placement });
        object awaiter = task.GetType().GetMethod("GetAwaiter").Invoke(task, null);
        Assert.That(awaiter.GetType().GetProperty("IsCompleted").GetValue(awaiter), Is.True,
            "제단 범위의 배치는 프리팹 로드 없이 즉시 종료해야 한다.");
        awaiter.GetType().GetMethod("GetResult").Invoke(awaiter, null);
        Assert.That(placements.Count, Is.EqualTo(1), "기존 저장본의 ID를 보존해야 한다.");
        Assert.That(spawner.GetType().GetMethod("IsSpawnExcluded").Invoke(spawner, new object[] { 123 }), Is.True);
        spawner.GetType().GetMethod("DespawnChunkObjects").Invoke(spawner, new object[] { coord });
        Assert.That(spawner.GetType().GetMethod("IsSpawnExcluded").Invoke(spawner, new object[] { 123 }), Is.True);
        spawner.GetType().GetMethod("Clear").Invoke(spawner, null);
        Assert.That(spawner.GetType().GetMethod("IsSpawnExcluded").Invoke(spawner, new object[] { 123 }), Is.False);
    }

    private ScriptableObject CreateItem(Vector2Int footprint)
    {
        var item = ScriptableObject.CreateInstance(FindType("ItemDataSO"));
        created.Add(item);
        item.GetType().GetField("placementFootprint").SetValue(item, footprint);
        item.GetType().GetField("placementCheckHeight").SetValue(item, 2f);
        return item;
    }

    private object Call(string name, params object[] args)
        => area.GetMethods(BindingFlags.Public | BindingFlags.Static).Single(method =>
            method.Name == name && method.GetParameters().Length == args.Length &&
            method.GetParameters().Select((parameter, index) => args[index] == null ||
                parameter.ParameterType.IsByRef || parameter.ParameterType.IsInstanceOfType(args[index])).All(matches => matches))
            .Invoke(null, args);

    private static Type FindType(string name)
        => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static void SetProperty(object target, string name, object value)
        => target.GetType().GetProperty(name).SetValue(target, value);
    private static object GetProperty(object target, string name)
        => target.GetType().GetProperty(name).GetValue(target);
}
