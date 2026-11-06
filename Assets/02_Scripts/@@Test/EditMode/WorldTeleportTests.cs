using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class WorldTeleportTests
{
    private readonly List<UnityEngine.Object> _objects = new();
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);
    private static object New(string name, params object[] args) => Activator.CreateInstance(Type(name), args);
    private static object Get(object o, string name) => o.GetType().GetField(name)?.GetValue(o) ??
        o.GetType().GetProperty(name)?.GetValue(o);
    private static void Set(object o, string name, object value)
    {
        var field = o.GetType().GetField(name);
        if (field != null) field.SetValue(o, value);
        else o.GetType().GetProperty(name).SetValue(o, value);
    }
    private static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name).Invoke(o, args);
    private static object EnumValue(string type, string value) => Enum.Parse(Type(type), value);
    private object Asset(string type)
    {
        var asset = ScriptableObject.CreateInstance(Type(type));
        _objects.Add(asset);
        return asset;
    }

    [TearDown] public void Cleanup()
    {
        foreach (var obj in _objects) if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
        _objects.Clear();
    }

    [UnityTest] public IEnumerator Placement_IsDeterministic_AndPreservesExistingIds()
    {
        object first = null, second = null;
        yield return Generate(73, c => first = c);
        yield return Generate(73, c => second = c);
        Assert.That(Snapshot(first), Is.EqualTo(Snapshot(second)));
        var disposes = (IList)Get(Get(first, "DisposeData"), "DisposeDatas");
        Assert.That(Get(disposes[0], "instanceId"), Is.EqualTo(123456));
        Assert.That(Get(disposes[0], "tilePosition"), Is.EqualTo(new Vector2Int(20, 20)));
    }

    [UnityTest] public IEnumerator Placement_CreatesOneHighlandsPair_AndIncludesShallowerBranchEnds()
    {
        object context = null;
        yield return Generate(91, c => context = c);
        var entries = Entries(context);
        Assert.That(entries.Count(e => (bool)Get(e, "highlands")), Is.EqualTo(2));
        var shorts = entries.Where(e => !(bool)Get(e, "highlands")).ToList();
        Assert.That(shorts.Any(e => (int)Get(e, "regionIndex") == 1), Is.True, "Depth 2인 분기 끝도 포함해야 합니다.");
        Assert.That(shorts.Any(e => (int)Get(e, "regionIndex") == 2), Is.True, "Depth 3인 분기 끝도 포함해야 합니다.");
        Assert.That(shorts.All(e => (int)Get(e, "regionIndex") != 0 && (int)Get(e, "regionIndex") != 4 &&
            (int)Get(e, "regionIndex") != 5), Is.True);
        foreach (var group in shorts.GroupBy(e => (int)Get(e, "regionIndex"))) Assert.That(group.Count(), Is.LessThanOrEqualTo(2));
        foreach (var entry in entries)
        {
            object target = entries.Single(e => (int)Get(e, "id") == (int)Get(entry, "targetId"));
            Assert.That(Get(target, "targetId"), Is.EqualTo(Get(entry, "id")));
            Assert.That(Get(entry, "cost").ToString(), (bool)Get(entry, "highlands") ? Is.EqualTo("None") : Is.Not.EqualTo("None"));
        }
    }

    [UnityTest] public IEnumerator Placement_UsesDistalCoasts_AndSeparateArrivalPads()
    {
        object context = null;
        yield return Generate(110, c => context = c);
        var entries = Entries(context);
        var pair = entries.Where(e => !(bool)Get(e, "highlands") && (int)Get(e, "regionIndex") == 1).First();
        var target = entries.Single(e => (int)Get(e, "id") == (int)Get(pair, "targetId"));
        Assert.That(Get(target, "regionIndex"), Is.EqualTo(2), "다른 섬을 가로지르는 A-C 대신 가까운 A-B를 연결해야 합니다.");
        // A의 입구는 (32,32), B의 입구는 (39,5)입니다. 가장 먼 점까지 54칸 중 65% 이상 진입해야 합니다.
        var a = (Vector2Int)Get(pair, "tile");
        var b = (Vector2Int)Get(target, "tile");
        Assert.That(64 - a.x - a.y, Is.GreaterThanOrEqualTo(36));
        Assert.That(b.x + b.y - 44, Is.GreaterThanOrEqualTo(36));
        foreach (var entry in entries)
            Assert.That(((Vector2Int)Get(entry, "tile") - (Vector2Int)Get(entry, "arrivalTile")).sqrMagnitude, Is.EqualTo(9));
    }

    [UnityTest] public IEnumerator Placement_DoesNotConnectTouchingRegions()
    {
        object context = null;
        yield return Generate(32, c => context = c, touching: true);
        var entries = Entries(context);
        foreach (var entry in entries.Where(e => !(bool)Get(e, "highlands") && (int)Get(e, "regionIndex") == 1))
        {
            var target = entries.Single(e => (int)Get(e, "id") == (int)Get(entry, "targetId"));
            Assert.That(Get(target, "regionIndex"), Is.Not.EqualTo(2));
        }
    }

    [UnityTest] public IEnumerator Highlands_UsesReachableTierThreeKeyProvider_InsteadOfTierFiveNeighbor()
    {
        object context = null;
        yield return Generate(211, c => context = c, configure: c =>
        {
            var nodes = (IList)Get(Get(c, "GraphData"), "Nodes");
            Set(Get(nodes[4], "RegionData"), "LockIDs", new List<string> { "LOCK_TIER4" });
            Set(Get(nodes[0], "RegionData"), "GivenKeyIDs", new List<string> { "KEY_TIER1" });
            Set(Get(nodes[1], "RegionData"), "LockIDs", new List<string> { "LOCK_TIER5" });
            Set(Get(nodes[1], "RegionData"), "GivenKeyIDs", new List<string> { "KEY_TIER4" });
            Set(Get(nodes[2], "RegionData"), "LockIDs", new List<string> { "LOCK_TIER3" });
            Set(Get(nodes[2], "RegionData"), "GivenKeyIDs", new List<string> { "KEY_TIER4" });
            Set(Get(nodes[3], "RegionData"), "LockIDs", new List<string> { "LOCK_TIER3" });
            Set(Get(nodes[3], "RegionData"), "GivenKeyIDs", new List<string> { "KEY_TIER5" });
            ((IList)Get(Get(c, "GraphData"), "NodeConnections")).Add(New("NodeConnection", nodes[4], nodes[1]));
        });
        var gate = Entries(context).Single(e => (bool)Get(e, "highlands") && (int)Get(e, "regionIndex") != 4);
        Assert.That(Get(gate, "regionIndex"), Is.EqualTo(2), "직접 연결되지 않아도 적절한 티어/요구 키를 제공하는 지역을 선택해야 합니다.");
    }

    [UnityTest] public IEnumerator Highlands_DoesNotPutEntranceBehindHighlands()
    {
        object context = null;
        yield return Generate(212, c => context = c, configure: c =>
        {
            var graph = Get(c, "GraphData");
            var nodes = (IList)Get(graph, "Nodes");
            Set(Get(nodes[4], "RegionData"), "LockIDs", new List<string> { "LOCK_TIER4" });
            foreach (int index in new[] { 1, 2 })
            {
                Set(Get(nodes[index], "RegionData"), "LockIDs", new List<string> { "LOCK_TIER3" });
                Set(Get(nodes[index], "RegionData"), "GivenKeyIDs", new List<string> { "KEY_TIER4" });
            }
            var edges = (IList)Get(graph, "NodeConnections");
            for (int i = edges.Count - 1; i >= 0; i--)
                if (ReferenceEquals(Get(edges[i], "ChildNode"), nodes[1])) edges.RemoveAt(i);
            edges.Add(New("NodeConnection", nodes[4], nodes[1]));
        });
        var gate = Entries(context).Single(e => (bool)Get(e, "highlands") && (int)Get(e, "regionIndex") != 4);
        Assert.That(Get(gate, "regionIndex"), Is.EqualTo(2), "진입기가 Highlands를 먼저 통과해야 갈 수 있는 곳에 있으면 안 됩니다.");
    }

    [UnityTest] public IEnumerator Shortcuts_DoNotConnectVeryDistantBranches()
    {
        object context = null;
        var regions = new[] { new RectInt(44, 46, 36, 34), new RectInt(5, 5, 28, 28),
            new RectInt(230, 5, 28, 28), new RectInt(450, 5, 28, 28), new RectInt(87, 77, 30, 30), new RectInt() };
        yield return Generate(213, c => context = c, rectangles: regions);
        Assert.That(Entries(context).Count(e => !(bool)Get(e, "highlands")), Is.Zero);
        Assert.That(Entries(context).Count(e => (bool)Get(e, "highlands")), Is.EqualTo(2));
    }

    [UnityTest] public IEnumerator Shortcuts_RespectConfiguredMinimumAndMaximumDistance()
    {
        object context = null;
        yield return Generate(214, c => context = c, configure: c =>
        {
            var settings = Get(Get(c, "Settings"), "TeleportSettings");
            Set(settings, "shortcutMinSeaDistance", 28f);
            Set(settings, "shortcutMaxDistance", 48f);
        });
        var entries = Entries(context);
        var shortcuts = entries.Where(e => !(bool)Get(e, "highlands")).ToList();
        Assert.That(shortcuts, Is.Not.Empty);
        foreach (var entry in shortcuts)
        {
            var target = entries.Single(e => (int)Get(e, "id") == (int)Get(entry, "targetId"));
            float distance = Vector2Int.Distance((Vector2Int)Get(entry, "tile"), (Vector2Int)Get(target, "tile"));
            Assert.That(distance, Is.InRange(28f, 48f));
        }
    }

    [Test] public void Cost_ChargesExactlyOneStat_IgnoresArmorAndInvincibility_AndPreservesOneHp()
    {
        object data = Asset("PlayerStatData");
        Set(data, "MaxHP", 100f); Set(data, "CurHP", 11f); Set(data, "Defense", 999f);
        Set(data, "MaxHunger", 100f); Set(data, "CurHunger", 25f);
        Set(data, "MaxEgo", 100f); Set(data, "CurEgo", 30f);
        object status = New("PlayerStatus", data);
        Set(status, "Invincible", true);
        Assert.That(Call(status, "TryPayTeleport", EnumValue("TeleportCost", "Hp"), 10f), Is.True);
        Assert.That(Get(status, "CurrentHp"), Is.EqualTo(1f));
        Assert.That(Get(status, "CurrentEgo"), Is.EqualTo(30f));
        Assert.That(Get(status, "CurrentHunger"), Is.EqualTo(25f));
        Assert.That(Call(status, "TryPayTeleport", EnumValue("TeleportCost", "Hp"), 1f), Is.False);
        Assert.That(Call(status, "TryPayTeleport", EnumValue("TeleportCost", "Ego"), 31f), Is.False);
        Assert.That(Call(status, "TryPayTeleport", EnumValue("TeleportCost", "Ego"), 10f), Is.True);
        Assert.That(Get(status, "CurrentEgo"), Is.EqualTo(20f));
        Assert.That(Call(status, "TryPayTeleport", EnumValue("TeleportCost", "Hunger"), 25f), Is.True);
        Assert.That(Get(status, "CurrentHunger"), Is.EqualTo(0f));
        Assert.That(Call(status, "TryPayTeleport", EnumValue("TeleportCost", "Hp"), float.NaN), Is.False);
        Assert.That(Call(status, "TryPayTeleport", EnumValue("TeleportCost", "None"), 0f), Is.True);
    }

    [UnityTest] public IEnumerator Save_RoundTripRestoresPositionsConnectionsAndCosts_AndAcceptsLegacy()
    {
        object context = null;
        yield return Generate(200, c => context = c);
        object logic = Get(context, "LogicData");
        Set(Entries(context).First(e => !(bool)Get(e, "highlands")), "amount", 17f);
        var save = New("WorldSaveData");
        Set(save, "teleportVersion", 1);
        Set(save, "teleports", Get(logic, "Teleports"));
        var restored = JsonUtility.FromJson(JsonUtility.ToJson(save), save.GetType());
        string before = Snapshot(context);
        ((IList)Get(logic, "Teleports")).Clear();
        Type("WorldSaveAdapter").GetMethod("ApplyChunkChanges").Invoke(null, new[] { restored, logic });
        Assert.That(Snapshot(context), Is.EqualTo(before));
        var legacy = JsonUtility.FromJson("{\"seed\":200,\"branch\":0,\"loop\":0,\"size\":0," +
            "\"totalSeconds\":0,\"destroyedObjects\":[],\"structures\":[],\"droppedItems\":[]}", Type("WorldSaveData"));
        Type("WorldSaveAdapter").GetMethod("ApplyChunkChanges").Invoke(null, new[] { legacy, logic });
        Assert.That(Snapshot(context), Is.EqualTo(before));
        var entries = (IList)Get(restored, "teleports");
        Set(entries[0], "targetId", (int)Get(entries[0], "id"));
        Assert.Throws<TargetInvocationException>(() => Type("WorldSaveAdapter").GetMethod("Validate").Invoke(null, new[] { restored }));
    }

    [UnityTest] public IEnumerator Save_ReplacesGeneratedTeleportIds_AndRejectsOrdinaryPlacementCollisions()
    {
        object context = null;
        yield return Generate(301, c => context = c);
        object logic = Get(context, "LogicData");
        var generated = Entries(context);
        var generatedIds = generated.Select(e => (int)Get(e, "id")).ToArray();
        var disposes = (IList)Get(Get(context, "DisposeData"), "DisposeDatas");
        object ordinary = disposes[0];
        int ordinaryId = (int)Get(ordinary, "instanceId");
        foreach (object dispose in disposes)
        {
            var tile = (Vector2Int)Get(dispose, "tilePosition");
            var coord = Call(logic, "GetChunkCoord", tile.x, tile.y);
            var chunk = Call(logic, "GetOrCreateChunk", coord);
            ((IList)Get(chunk, "DisposeDatas")).Add(dispose);
        }
        // 복원 전에 생성 ID 색인을 만들어, 교체 후에도 옛 청크 색인이 남지 않는지 확인합니다.
        Assert.That(Call(logic, "FindChunkByInstanceId", generatedIds[0]), Is.Not.Null);

        var usedIds = new HashSet<int>(generatedIds) { ordinaryId };
        var replacements = new Dictionary<int, int>();
        int nextId = 700001;
        foreach (int id in generatedIds)
        {
            while (!usedIds.Add(nextId)) nextId++;
            replacements.Add(id, nextId++);
        }
        var savedTeleports = (IList)Activator.CreateInstance(Get(logic, "Teleports").GetType());
        foreach (object entry in generated)
        {
            object copy = Call(entry, "Copy");
            Set(copy, "id", replacements[(int)Get(entry, "id")]);
            Set(copy, "targetId", replacements[(int)Get(entry, "targetId")]);
            savedTeleports.Add(copy);
        }
        object save = New("WorldSaveData");
        Set(save, "teleportVersion", 1);
        Set(save, "teleports", savedTeleports);
        var apply = Type("WorldSaveAdapter").GetMethod("ApplyChunkChanges");
        apply.Invoke(null, new[] { save, logic });

        List<object> ChunkPlacements() => ((IEnumerable)Call(logic, "GetAllChunks")).Cast<object>()
            .SelectMany(c => ((IEnumerable)Get(c, "DisposeDatas")).Cast<object>()).ToList();
        var restoredIds = replacements.Values.ToArray();
        CollectionAssert.AreEquivalent(restoredIds, Entries(context).Select(e => (int)Get(e, "id")));
        CollectionAssert.AreEquivalent(restoredIds.Concat(new[] { ordinaryId }),
            ChunkPlacements().Select(p => (int)Get(p, "instanceId")));
        Assert.That(ChunkPlacements().Contains(ordinary), Is.True);
        foreach (int id in generatedIds) Assert.That(Call(logic, "FindChunkByInstanceId", id), Is.Null);
        foreach (int id in restoredIds) Assert.That(Call(logic, "FindChunkByInstanceId", id), Is.Not.Null);
        Assert.That(Call(logic, "FindChunkByInstanceId", ordinaryId), Is.Not.Null);

        // 연결 자체는 유효하게 유지한 채 일반 배치의 ID를 사용하면, 교체 전에 거부해야 합니다.
        var collision = JsonUtility.FromJson(JsonUtility.ToJson(save), save.GetType());
        var collidingEntries = (IList)Get(collision, "teleports");
        int replacedId = (int)Get(collidingEntries[0], "id");
        Set(collidingEntries[0], "id", ordinaryId);
        foreach (object entry in collidingEntries)
            if ((int)Get(entry, "targetId") == replacedId) Set(entry, "targetId", ordinaryId);
        string before = Snapshot(context);
        var error = Assert.Throws<TargetInvocationException>(() => apply.Invoke(null, new[] { collision, logic }));
        Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
        Assert.That(Snapshot(context), Is.EqualTo(before));
        CollectionAssert.AreEquivalent(restoredIds.Concat(new[] { ordinaryId }),
            ChunkPlacements().Select(p => (int)Get(p, "instanceId")));
        Assert.That(ChunkPlacements().Contains(ordinary), Is.True);
    }

    [Test] public void Landing_RequiresLoadedTerrain_AndRejectsBlockingStructures()
    {
        var actor = new GameObject("InactiveTeleportActor"); _objects.Add(actor); actor.SetActive(false);
        var player = actor.AddComponent(Type("Player"));
        var cc = actor.AddComponent<CharacterController>(); cc.center = Vector3.up; cc.height = 2f; cc.radius = 0.4f;
        var method = Type("WorldTeleporter").GetMethod("TryLanding", BindingFlags.Static | BindingFlags.NonPublic);
        object[] args = { player, new Vector3(8f, 0.15f, 8f), null };
        Assert.That(method.Invoke(null, args), Is.False);
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(16f, 10f, 16f) };
        _objects.Add(data);
        var terrain = Terrain.CreateTerrainGameObject(data); _objects.Add(terrain);
        terrain.layer = LayerMask.NameToLayer("Ground");
        Physics.SyncTransforms();
        Assert.That(method.Invoke(null, args), Is.True);
        var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube); _objects.Add(blocker);
        blocker.transform.position = new Vector3(8f, 1f, 8f); blocker.transform.localScale = new Vector3(2f, 2f, 2f);
        Physics.SyncTransforms();
        Assert.That(method.Invoke(null, args), Is.False);
    }

    [UnityTest] public IEnumerator Minimap_DrawsYellowAndPurpleDiamonds_AtFixedSizeAcrossZoom()
    {
        object context = null;
        yield return Generate(12, c => context = c);
        var go = new GameObject("TeleportMarkerValidation", typeof(RectTransform)); _objects.Add(go);
        var nav = go.AddComponent(Type("UI_MapViewport"));
        var graphic = go.AddComponent(Type("UI_TeleportMarkerGraphic"));
        var field = Type("UI_MapViewport").GetField("_contentSize", BindingFlags.Instance | BindingFlags.NonPublic);
        field.SetValue(nav, new Vector2(400f, 400f));
        var map = New("WorldMapData", null, null, new Vector2Int(128, 128), Get(context, "LogicData"));
        Call(graphic, "Configure", map, nav, false);
        var populate = graphic.GetType().GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
            null, new[] { typeof(VertexHelper) }, null);
        using var vh = new VertexHelper();
        populate.Invoke(graphic, new object[] { vh });
        Assert.That(vh.currentVertCount, Is.EqualTo(Entries(context).Count * 8));
        UIVertex top = default, right = default, inside = default;
        vh.PopulateUIVertex(ref top, 0); vh.PopulateUIVertex(ref right, 1); vh.PopulateUIVertex(ref inside, 4);
        float radius = right.position.x - top.position.x;
        Assert.That(radius, Is.EqualTo(9f).Within(0.01f));
        Assert.That(inside.color, Is.EqualTo(new Color32(255, 220, 30, 255)));
        vh.PopulateUIVertex(ref inside, 20);
        Assert.That(inside.color, Is.EqualTo(new Color32(185, 70, 255, 255)));
        field.SetValue(nav, new Vector2(1600f, 1600f));
        populate.Invoke(graphic, new object[] { vh });
        vh.PopulateUIVertex(ref top, 0); vh.PopulateUIVertex(ref right, 1);
        Assert.That(right.position.x - top.position.x, Is.EqualTo(radius).Within(0.01f));
    }

    [UnityTest] public IEnumerator Minimap_RendersMapAndClippedMarkers_WithoutCanvasErrors()
    {
        var root = new GameObject("MapCanvasValidation", typeof(RectTransform), typeof(Canvas));
        _objects.Add(root);
        root.layer = 5;
        var cameraObject = new GameObject("MapValidationCamera", typeof(Camera));
        _objects.Add(cameraObject);
        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.cullingMask = 1 << 5;
        var renderTarget = new RenderTexture(256, 256, 24);
        _objects.Add(renderTarget);
        camera.targetTexture = renderTarget;
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;

        var viewport = new GameObject("MapViewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.layer = 5;
        viewport.transform.SetParent(root.transform, false);
        ((RectTransform)viewport.transform).sizeDelta = new Vector2(128f, 128f);
        var imageObject = new GameObject("MapImage", typeof(RectTransform), typeof(Image));
        imageObject.layer = 5;
        imageObject.transform.SetParent(viewport.transform, false);
        var mapImage = imageObject.GetComponent<Image>();
        mapImage.color = new Color(0.05f, 0.25f, 0.4f, 1f);
        var imageRect = (RectTransform)imageObject.transform;
        imageRect.anchorMin = Vector2.zero; imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = imageRect.offsetMax = Vector2.zero;

        var navigation = viewport.AddComponent(Type("UI_MapViewport"));
        Type("UI_MapViewport").GetField("_contentSize", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(navigation, new Vector2(256f, 256f));
        object logic = New("WorldLogicData", new Vector2Int(256, 256), 16);
        var entries = (IList)Get(logic, "Teleports");
        foreach (var tile in new[] { new Vector2Int(128, 128), new Vector2Int(230, 128) })
        {
            object entry = New("WorldTeleportPlacement");
            Set(entry, "tile", tile);
            Set(entry, "highlands", tile.x == 128);
            entries.Add(entry);
        }
        var data = New("WorldMapData", null, null, new Vector2Int(256, 256), logic);
        // 런타임과 동일하게 Graphic 타입만 지정해도 필수 CanvasRenderer가 자동 생성되어야 합니다.
        var markers = new GameObject("TeleportMarkers", typeof(RectTransform), Type("UI_TeleportMarkerGraphic"));
        markers.layer = 5;
        markers.transform.SetParent(viewport.transform, false);
        var graphic = markers.GetComponent<MaskableGraphic>();
        Assert.That(graphic.canvasRenderer, Is.Not.Null);
        var markerRect = (RectTransform)markers.transform;
        markerRect.anchorMin = Vector2.zero; markerRect.anchorMax = Vector2.one;
        markerRect.offsetMin = markerRect.offsetMax = Vector2.zero;
        Call(graphic, "Configure", data, navigation, false);
        yield return null;

        Canvas.ForceUpdateCanvases();
        viewport.GetComponent<RectMask2D>().PerformClipping();
        Assert.That(graphic.canvasRenderer.hasRectClipping, Is.True);
        Assert.That(graphic.canvasRenderer.GetMesh().vertexCount, Is.EqualTo(16));
        Assert.That(mapImage.canvasRenderer.GetMesh().vertexCount, Is.EqualTo(4));
        // 맵 축소/확대처럼 활성 상태가 바뀌는 경로에서도 마스킹 오류가 없어야 합니다.
        viewport.SetActive(false);
        viewport.SetActive(true);
        Canvas.ForceUpdateCanvases();
        LogAssert.NoUnexpectedReceived();

        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        var pixels = new Texture2D(256, 256, TextureFormat.RGB24, false);
        _objects.Add(pixels);
        try
        {
            RenderTexture.active = renderTarget;
            pixels.ReadPixels(new Rect(0f, 0f, 256f, 256f), 0, 0);
            pixels.Apply();
        }
        finally { RenderTexture.active = previous; }
        Color center = pixels.GetPixel(128, 128);
        Assert.That(center.r, Is.GreaterThan(0.7f));
        Assert.That(center.g, Is.GreaterThan(0.6f));
        Assert.That(center.b, Is.LessThan(0.3f));
        Color map = pixels.GetPixel(80, 80);
        Assert.That(map.b, Is.GreaterThan(map.r + 0.1f), "지도 배경도 함께 렌더링되어야 합니다.");
        Color outside = pixels.GetPixel(230, 128);
        Assert.That(Mathf.Max(outside.r, outside.g, outside.b), Is.LessThan(0.02f),
            "표시 영역 바깥의 보라색 마커는 마스크에 가려져야 합니다.");
        string capturePath = Environment.GetEnvironmentVariable("TELEPORT_MAP_RENDER_PATH");
        if (!string.IsNullOrEmpty(capturePath)) System.IO.File.WriteAllBytes(capturePath, pixels.EncodeToPNG());
    }

    private static List<object> Entries(object context) => ((IEnumerable)Get(Get(context, "LogicData"), "Teleports")).Cast<object>().ToList();
    private static string Snapshot(object context) => string.Join("|", Entries(context).Select(JsonUtility.ToJson));

    private IEnumerator Generate(int seed, Action<object> result, bool touching = false,
        Action<object> configure = null, RectInt[] rectangles = null)
    {
        object settings = Asset("WorldSettings");
        Set(settings, "WorldSeed", seed);
        var rects = rectangles ?? new[] { new RectInt(44, 46, 36, 34), new RectInt(5, 5, 28, 28),
            new RectInt(touching ? 33 : 39, 5, 28, 28), new RectInt(73, 5, 28, 28), new RectInt(87, 77, 30, 30), new RectInt() };
        int width = Math.Max(128, rects.Max(r => r.xMax) + 8), height = Math.Max(128, rects.Max(r => r.yMax) + 8);
        object logic = New("WorldLogicData", new Vector2Int(width, height), 16);
        Set(logic, "SpawnTile", new Vector2Int(62, 62));
        int[,] territory = (int[,])Get(logic, "TerritoryWorld");
        for (int x = 0; x < width; x++) for (int y = 0; y < height; y++) territory[x, y] = -1;
        object graph = New("WorldGraphData");
        var nodes = (IList)Get(graph, "Nodes");
        for (int index = 0; index < rects.Length; index++)
        {
            object biome = Asset("BiomeData");
            Set(biome, "BiomeType", EnumValue("BiomeType", index == 4 ? "Highlands" : "Plains"));
            object region = Asset("RegionData"); Set(region, "BiomeData", biome);
            object node = New("Node"); Set(node, "RegionData", region); Set(node, "Index", index);
            Set(node, "Depth", index == 0 ? 1 : index == 2 ? 3 : 2);
            Set(node, "Position", rects[index].center);
            var tiles = (IList)Get(node, "OwnedTiles");
            foreach (Vector2Int tile in rects[index].allPositionsWithin)
            {
                tiles.Add(tile); territory[tile.x, tile.y] = index;
                ((float[,])Get(logic, "HeightWorld"))[tile.x, tile.y] = index == 4 ? 30f : 10f;
            }
            nodes.Add(node);
        }
        var edges = (IList)Get(graph, "NodeConnections");
        foreach (var pair in new[] { (0, 1), (0, 5), (5, 2), (0, 3), (0, 4) })
            edges.Add(New("NodeConnection", nodes[pair.Item1], nodes[pair.Item2]));
        object disposes = New("WorldDisposeData");
        object existing = New("DisposeData"); Set(existing, "instanceId", 123456);
        Set(existing, "tilePosition", new Vector2Int(20, 20));
        ((IList)Get(disposes, "DisposeDatas")).Add(existing);
        object context = New("WorldGenContext");
        Set(context, "Settings", settings); Set(context, "GraphData", graph); Set(context, "LogicData", logic); Set(context, "DisposeData", disposes);
        configure?.Invoke(context);
        object stage = New("TeleportDisposer"); Call(stage, "Initialize", settings);
        object task = Call(stage, "ExecuteAsync", context, CancellationToken.None);
        int frames = 0;
        while (Get(task, "Status").ToString() == "Pending")
        {
            Assert.That(++frames, Is.LessThan(10000));
            yield return null;
        }
        Call(Call(task, "GetAwaiter"), "GetResult");
        result(context);
    }
}
