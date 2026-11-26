using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class WorldMapPresentationTests : InputTestFixture
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private GameObject _root;
    private Component _hud;
    private Component _map;
    private Component _navigation;
    private RectTransform _slot;
    private RectTransform _expanded;
    private Sprite _sprite;
    private Texture2D _texture;
    private Vector3 _backgroundScale;
    private object _actions;
    private object _handler;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _root = new GameObject("WorldMapPresentationTest", typeof(RectTransform), typeof(Canvas));
        _root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        _root.GetComponent<Canvas>().sortingOrder = 10;
        ((RectTransform)_root.transform).sizeDelta = new Vector2(1920f, 1080f);
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/03_Prefabs/UI/World/UI_Hud_WorldState.prefab");
        var instance = UnityEngine.Object.Instantiate(prefab, _root.transform, false);
        _hud = instance.GetComponent(FindType("UI_Hud_WorldState"));
        Invoke(_hud, "Initialize");
        _map = (Component)Property(_hud, "WorldMapView");
        _backgroundScale = _map.transform.Find("BG").localScale;
        _slot = (RectTransform)Field(_hud, "_mapSlot");
        _expanded = (RectTransform)Field(_hud, "_expandedMapRoot");
        _navigation = _map.GetComponentInChildren(FindType("UI_MapViewport"));

        // 실제 월드 생성/저장에 의존하지 않는 지도 데이터로 전환을 검증한다.
        _texture = new Texture2D(64, 64);
        _sprite = Sprite.Create(_texture, new Rect(0, 0, 64, 64), Vector2.one * 0.5f);
        object data = Activator.CreateInstance(FindType("WorldMapData"),
            _texture, _sprite, new Vector2Int(1000, 1000));
        Invoke(_map, "HandleDataChanged", data);
        Layout();
    }

    [TearDown]
    public override void TearDown()
    {
        if (_handler != null) Invoke(_handler, "Disconnect");
        if (_actions != null)
        {
            Invoke(_actions, "Disable");
            // 생성된 Dispose는 Destroy를 사용하므로 EditMode에서는 에셋을 즉시 정리한다.
            UnityEngine.Object.DestroyImmediate((UnityEngine.Object)Property(_actions, "asset"));
        }
        if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        if (_sprite != null) UnityEngine.Object.DestroyImmediate(_sprite);
        if (_texture != null) UnityEngine.Object.DestroyImmediate(_texture);
        _handler = _actions = null;
        base.TearDown();
    }

    [TestCase(1920f, 1080f)]
    [TestCase(2560f, 1080f)]
    [TestCase(1280f, 1024f)]
    public void Toggle_UsesPreviousPopupDimensionsAndPreservesHudLayout(float width, float height)
    {
        ((RectTransform)_root.transform).sizeDelta = new Vector2(width, height);
        Layout();
        var clock = (RectTransform)_hud.transform.Find("WorldStateContent/Top/UI_WorldClock");
        Vector3 clockPosition = clock.position;
        Vector3 slotPosition = _slot.position;
        Assert.That(_map.transform.parent, Is.EqualTo(_slot));
        Assert.That(_slot.rect.size, Is.EqualTo(new Vector2(150f, 85f)));
        Assert.That(_expanded.gameObject.activeSelf, Is.False);

        Invoke(_hud, "ToggleMap");
        Layout();
        Assert.That(Property(_hud, "IsMapExpanded"), Is.True);
        Assert.That(_map.transform.parent, Is.EqualTo(_expanded));
        Assert.That(((RectTransform)_map.transform).rect.width, Is.EqualTo(width).Within(0.1f));
        Assert.That(((RectTransform)_map.transform).rect.height, Is.EqualTo(height).Within(0.1f));
        var viewport = (RectTransform)_navigation.transform;
        Assert.That(viewport.rect.width, Is.EqualTo(width * 0.8f).Within(0.1f));
        Assert.That(viewport.rect.height, Is.EqualTo(height * 0.8f).Within(0.1f));
        Assert.That(_map.transform.Find("BG").localScale, Is.EqualTo(_backgroundScale));
        Assert.That(Vector3.Distance(clock.position, clockPosition), Is.LessThan(0.1f));
        Assert.That(Vector3.Distance(_slot.position, slotPosition), Is.LessThan(0.1f));
        Assert.That(_expanded.GetComponent<Image>().raycastTarget, Is.True);
        Assert.That(_expanded.GetComponent<Canvas>().sortingOrder, Is.InRange(11, 19));

        Invoke(_hud, "ToggleMap");
        Layout();
        Assert.That(_map.transform.parent, Is.EqualTo(_slot));
        Assert.That(_expanded.gameObject.activeSelf, Is.False);
        Assert.That(((RectTransform)_map.transform).rect.size, Is.EqualTo(_slot.rect.size));
    }

    [Test]
    public void Toggle_KeepsOneMapSpriteZoomFocusAndTarget()
    {
        var target = new GameObject("MapTarget").transform;
        target.SetParent(_root.transform, false);
        target.position = new Vector3(600f, 0f, 400f);
        Invoke(_hud, "SetTarget", target);
        _navigation.GetType().GetProperty("Zoom").SetValue(_navigation, 8f);
        _navigation.GetType().GetProperty("IsNavigating").SetValue(_navigation, true);
        _navigation.GetType().GetField("_center", Members).SetValue(_navigation, new Vector2(0.2f, 0.8f));
        Layout();
        Vector2 playerNormalized = new Vector2(0.6f, 0.4f);
        var image = (Component)Property(_navigation, "MapImage");

        for (int i = 0; i < 10; i++)
        {
            Invoke(_hud, "ToggleMap");
            Layout();
            Assert.That(Property(_hud, "WorldMapView"), Is.SameAs(_map));
            Assert.That(image.GetComponent<Image>().sprite, Is.SameAs(_sprite));
            Assert.That(Property(_navigation, "Zoom"), Is.EqualTo(6f));
            Assert.That(Property(_navigation, "IsNavigating"), Is.False);
            Assert.That(Vector2.Distance((Vector2)Field(_navigation, "_center"), playerNormalized), Is.LessThan(0.001f));
            Assert.That(Property(_map, "Target"), Is.SameAs(target));
            Assert.That(_hud.GetComponentsInChildren(FindType("UI_Panel_WorldMap"), true).Length, Is.EqualTo(1));
            var marker = (RectTransform)Field(_map, "_playerMarker");
            Assert.That(marker.sizeDelta.x, Is.EqualTo(i % 2 == 0 ? 60f : 18f));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CameraHeading_RotatesPlayerImageAndSightAreaTogether(bool expanded)
    {
        var target = CreateMapTarget("LocalTarget", new Vector3(500f, 0f, 500f));
        Invoke(_hud, "SetTarget", target);
        if (expanded) Invoke(_hud, "ToggleMap");
        Layout();

        var playerRoot = (RectTransform)Field(_map, "_playerMarker");
        Transform sightArea = playerRoot.Find("SightArea");
        Transform markerImage = playerRoot.Find("Marker");
        Assert.That(sightArea, Is.Not.Null);
        Assert.That(markerImage, Is.Not.Null);
        Vector2 markerPosition = playerRoot.anchoredPosition;

        Camera viewCamera = Camera.main;
        GameObject cameraObject = null;
        if (viewCamera == null)
        {
            cameraObject = new GameObject("MapHeadingCamera", typeof(Camera));
            cameraObject.transform.SetParent(_root.transform, false);
            cameraObject.tag = "MainCamera";
            viewCamera = cameraObject.GetComponent<Camera>();
        }
        Quaternion cameraRotation = viewCamera.transform.rotation;

        try
        {
            foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
            {
                viewCamera.transform.rotation = Quaternion.Euler(35f, yaw, 0f);
                Invoke(_map, "UpdatePlayerMarkerRotation");
                Quaternion expectedRotation = playerRoot.parent.rotation * Quaternion.Euler(0f, 0f, -yaw);
                Assert.That(Quaternion.Angle(sightArea.rotation, expectedRotation),
                    Is.LessThan(0.01f), "시야선은 카메라의 수평 방향을 따라야 한다.");
                Assert.That(Quaternion.Angle(markerImage.rotation, expectedRotation),
                    Is.LessThan(0.01f), "플레이어 마커가 시야선과 같은 방향을 가리켜야 한다.");
                Assert.That(playerRoot.anchoredPosition, Is.EqualTo(markerPosition));
            }

            Quaternion previousMarkerRotation = markerImage.rotation;
            viewCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Invoke(_map, "UpdatePlayerMarkerRotation");
            Assert.That(Quaternion.Angle(markerImage.rotation, previousMarkerRotation),
                Is.LessThan(0.01f), "수직 시선에서는 마지막 수평 방향을 유지한다.");
        }
        finally
        {
            viewCamera.transform.rotation = cameraRotation;
            if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void DisableAndDestroyHud_LeavesNoDetachedMap()
    {
        Invoke(_hud, "ToggleMap");
        _hud.gameObject.SetActive(false);
        Assert.That(_map.gameObject.activeInHierarchy, Is.False);
        Assert.That(_map.transform.IsChildOf(_hud.transform), Is.True);
        _hud.gameObject.SetActive(true);
        // 일반 MonoBehaviour의 활성화 콜백은 EditMode에서 직접 호출한다.
        Invoke(_hud, "OnEnable");
        Assert.That(Property(_hud, "IsMapExpanded"), Is.False);
        Assert.That(_map.transform.parent, Is.EqualTo(_slot));
        Invoke(_hud, "ToggleMap");
        UnityEngine.Object.DestroyImmediate(_hud.gameObject);
        Assert.That(_map == null, Is.True);
        Assert.That(_root.transform.childCount, Is.Zero);
    }

    [Test]
    public void MKey_TogglesExistingMapAndDisconnectStopsInput()
    {
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var manager = Activator.CreateInstance(FindType("InputManager"));
        _actions = Activator.CreateInstance(FindType("InputSystem_Actions"));
        manager.GetType().GetField("_input", Members).SetValue(manager, _actions);
        _handler = Activator.CreateInstance(FindType("UIMapInputHandler"), manager);
        Invoke(_handler, "Connect");
        Invoke(_handler, "Connect");
        Invoke(_actions, "Enable");
        Press(keyboard.mKey);
        Assert.That(Property(_hud, "IsMapExpanded"), Is.True);
        InputSystem.Update();
        Assert.That(Property(_hud, "IsMapExpanded"), Is.True, "키를 유지해도 재전환하지 않는다.");
        Release(keyboard.mKey);
        Press(keyboard.mKey);
        Assert.That(Property(_hud, "IsMapExpanded"), Is.False);
        Release(keyboard.mKey);
        Invoke(_handler, "Disconnect");
        Press(keyboard.mKey);
        Assert.That(Property(_hud, "IsMapExpanded"), Is.False);
        Release(keyboard.mKey);
        Invoke(_handler, "Connect");
        UnityEngine.Object.DestroyImmediate(_hud.gameObject);
        Assert.DoesNotThrow(() => Press(keyboard.mKey));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DefaultView_CentersPlayerRegardlessOfMapAndTargetArrivalOrder(bool targetFirst)
    {
        object data = Field(_map, "_mapData");
        Invoke(_map, "ClearView");
        var target = new GameObject("LateMapTarget").transform;
        target.SetParent(_root.transform, false);
        target.position = new Vector3(20f, 0f, 980f);

        if (targetFirst) Invoke(_hud, "SetTarget", target);
        Invoke(_map, "HandleDataChanged", data);
        if (!targetFirst) Invoke(_hud, "SetTarget", target);
        Layout();
        AssertDefaultView();

        // 저장된 위치가 HUD 생성 이후 복원되거나 이동해도 중심 추적을 유지한다.
        target.position = new Vector3(800f, 0f, 100f);
        Invoke(_map, "LateUpdate");
        AssertDefaultView();
        Invoke(_hud, "ToggleMap");
        Layout();
        AssertDefaultView();
    }

    [Test]
    public void MiniMapScroll_ZoomsAroundPlayerAndResumesFollowingAfterDrag()
    {
        var target = CreateMapTarget("MiniMapTarget", new Vector3(700f, 0f, 400f));
        Invoke(_hud, "SetTarget", target);
        Layout();

        var viewport = (RectTransform)_navigation.transform;
        var eventsObject = new GameObject("MiniMapEventSystem", typeof(EventSystem));
        eventsObject.transform.SetParent(_root.transform, false);
        var pointer = new PointerEventData(eventsObject.GetComponent<EventSystem>())
        {
            button = PointerEventData.InputButton.Left,
            pointerId = -1,
            position = RectTransformUtility.WorldToScreenPoint(null,
                viewport.TransformPoint(viewport.rect.center + viewport.rect.size * 0.25f)),
            scrollDelta = Vector2.up * 2f
        };
        float defaultZoom = (float)Property(_navigation, "Zoom");
        ((IScrollHandler)_navigation).OnScroll(pointer);
        float enlargedZoom = (float)Property(_navigation, "Zoom");
        Assert.That(enlargedZoom, Is.GreaterThan(defaultZoom));
        AssertPlayerCentered();

        target.position = new Vector3(995f, 0f, 5f);
        Invoke(_map, "LateUpdate");
        AssertPlayerCentered();
        Assert.That(Property(_navigation, "Zoom"), Is.EqualTo(enlargedZoom));

        ((IBeginDragHandler)_navigation).OnBeginDrag(pointer);
        pointer.delta = new Vector2(-12f, 8f);
        pointer.position += pointer.delta;
        ((IDragHandler)_navigation).OnDrag(pointer);
        ((IEndDragHandler)_navigation).OnEndDrag(pointer);
        Assert.That(Property(_navigation, "IsNavigating"), Is.True);
        pointer.Reset();
        pointer.scrollDelta = Vector2.down;
        ((IScrollHandler)_navigation).OnScroll(pointer);
        Assert.That((float)Property(_navigation, "Zoom"), Is.LessThan(enlargedZoom));
        AssertPlayerCentered();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DoubleClick_RestoresPlayerTrackingAndKeepsZoomAfterExploration(bool expanded)
    {
        var target = new GameObject("ExploredMapTarget").transform;
        target.SetParent(_root.transform, false);
        target.position = new Vector3(700f, 0f, 400f);
        Invoke(_hud, "SetTarget", target);
        Invoke(_hud, "SetMapExpanded", expanded);
        Layout();

        var viewport = (RectTransform)_navigation.transform;
        var eventsObject = new GameObject("MapEventSystem", typeof(EventSystem));
        eventsObject.transform.SetParent(_root.transform, false);
        var pointer = new PointerEventData(eventsObject.GetComponent<EventSystem>())
        {
            button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null,
                viewport.TransformPoint(viewport.rect.center + viewport.rect.size * 0.25f)),
            scrollDelta = Vector2.up * 2f,
            pointerId = -1
        };
        ((IScrollHandler)_navigation).OnScroll(pointer);
        float chosenZoom = (float)Property(_navigation, "Zoom");
        Assert.That(chosenZoom, Is.GreaterThan(6f));
        Assert.That(Property(_navigation, "IsNavigating"), Is.EqualTo(expanded));
        ((IBeginDragHandler)_navigation).OnBeginDrag(pointer);
        pointer.delta = new Vector2(12f, -8f);
        pointer.position += pointer.delta;
        ((IDragHandler)_navigation).OnDrag(pointer);
        Assert.That(Property(_navigation, "IsNavigating"), Is.True);

        target.position = new Vector3(5f, 0f, 995f);
        Invoke(_map, "LateUpdate");
        var marker = (RectTransform)Field(_map, "_playerMarker");
        Assert.That(marker.anchoredPosition.sqrMagnitude, Is.GreaterThan(1f),
            "직접 지도를 탐색하는 동안에는 플레이어에게 자동 복귀하지 않는다.");

        pointer.Reset();
        pointer.clickCount = 2;
        ((IPointerClickHandler)_navigation).OnPointerClick(pointer);
        Layout();
        Assert.That(Property(_navigation, "Zoom"), Is.EqualTo(chosenZoom));
        AssertPlayerCentered();
        Assert.That(pointer.used, Is.True);

        target.position = new Vector3(300f, 0f, 300f);
        Invoke(_map, "LateUpdate");
        AssertPlayerCentered();
        Assert.That(Property(_navigation, "Zoom"), Is.EqualTo(chosenZoom));
    }

    [Test]
    public void InGameReset_UsesRestoredPositionEvenWhenReusingTheSameTarget()
    {
        var target = new GameObject("RestoredMapTarget").transform;
        target.SetParent(_root.transform, false);
        target.position = new Vector3(500f, 0f, 500f);
        Invoke(_hud, "SetTarget", target);
        ((IBeginDragHandler)_navigation).OnBeginDrag(new PointerEventData(null));
        target.position = new Vector3(990f, 0f, 10f);

        // GameScene에서 위치 복원을 마친 뒤 호출하는 순서다.
        Invoke(_hud, "SetMapExpanded", false);
        Invoke(_hud, "SetTarget", target);
        Invoke(_map, "ResetToDefault");
        Layout();
        AssertDefaultView();
        Assert.That(Property(_hud, "IsMapExpanded"), Is.False);
    }

    [Test]
    public void RemoteMarkers_ReuseOnlyMarkerImageAndDoNotDuplicateTheLocalPlayer()
    {
        var local = CreateMapTarget("LocalTarget", new Vector3(500, 0, 500));
        var remote = CreateMapTarget("RemoteTarget", new Vector3(550, 0, 500));
        Invoke(_hud, "SetTarget", local);
        Invoke(_map, "AddRemoteMarker", local);
        Invoke(_map, "AddRemoteMarker", remote);
        Invoke(_map, "AddRemoteMarker", remote);
        Invoke(_map, "UpdateRemotePlayerMarkers");

        Assert.That(RemoteMarkers.Count, Is.EqualTo(1));
        var marker = (RectTransform)RemoteMarkers[remote];
        var template = (RectTransform)Field(_map, "_remoteMarkerTemplate");
        var images = marker.GetComponentsInChildren<Image>(true);
        Assert.That(images.Length, Is.EqualTo(1), "원격 마커에는 시야선 이미지를 복제하지 않는다.");
        Assert.That(images[0].sprite, Is.SameAs(template.GetComponent<Image>().sprite));
        Assert.That(images[0].color, Is.EqualTo(template.GetComponent<Image>().color));
        Assert.That(images[0].raycastTarget, Is.False);
        Assert.That(images[0].maskable, Is.True);
        Assert.That(marker.Find("SightArea"), Is.Null);
        Assert.That(marker.parent, Is.SameAs(_navigation.transform));
        Assert.That(marker.parent.GetComponent<RectMask2D>(), Is.Not.Null);
        var localMarker = (RectTransform)Field(_map, "_playerMarker");
        Assert.That(localMarker.Find("SightArea"), Is.Not.Null);
        Assert.That(marker.GetSiblingIndex(), Is.LessThan(localMarker.GetSiblingIndex()));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RemoteMarkers_FollowMovementAndMapNavigationWithoutScalingWithZoom(bool expanded)
    {
        var local = CreateMapTarget("LocalTarget", new Vector3(500, 0, 500));
        var remote = CreateMapTarget("RemoteTarget", new Vector3(530, 0, 540));
        Invoke(_hud, "SetTarget", local);
        Invoke(_map, "AddRemoteMarker", remote);
        Invoke(_hud, "SetMapExpanded", expanded);
        Layout();
        AssertRemotePosition(remote);
        var marker = (RectTransform)RemoteMarkers[remote];
        Vector2 markerSize = marker.sizeDelta;

        var eventsObject = new GameObject("RemoteMapEvents", typeof(EventSystem));
        eventsObject.transform.SetParent(_root.transform, false);
        var pointer = new PointerEventData(eventsObject.GetComponent<EventSystem>())
        {
            button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, _navigation.transform.position),
            scrollDelta = Vector2.up * 2f,
            pointerId = -1
        };
        ((IScrollHandler)_navigation).OnScroll(pointer);
        ((IBeginDragHandler)_navigation).OnBeginDrag(pointer);
        pointer.delta = new Vector2(15f, 8f);
        pointer.position += pointer.delta;
        ((IDragHandler)_navigation).OnDrag(pointer);
        remote.position = new Vector3(550, 0, 570);
        AssertRemotePosition(remote);
        Assert.That(marker.sizeDelta, Is.EqualTo(markerSize));

        pointer.clickCount = 2;
        ((IPointerClickHandler)_navigation).OnPointerClick(pointer);
        AssertRemotePosition(remote);
        Invoke(_hud, "ToggleMap");
        Layout();
        AssertRemotePosition(remote);
        Assert.That(RemoteMarkers[remote], Is.SameAs(marker));
        Assert.That(marker.sizeDelta, Is.EqualTo(((RectTransform)Field(_map, "_playerMarker")).sizeDelta));
    }

    [Test]
    public void RemoteMarkers_HideOutsideWorldAndCleanUpDepartedPlayersAndClearedMap()
    {
        var remote = CreateMapTarget("RemoteTarget", new Vector3(400, 0, 400));
        var departed = CreateMapTarget("DepartedTarget", new Vector3(600, 0, 600));
        Invoke(_map, "AddRemoteMarker", remote);
        Invoke(_map, "AddRemoteMarker", departed);
        var marker = (RectTransform)RemoteMarkers[remote];
        var departedMarker = (RectTransform)RemoteMarkers[departed];
        remote.position = new Vector3(-1, 0, 400);
        Invoke(_map, "UpdateRemotePlayerMarkers");
        Assert.That(marker.gameObject.activeSelf, Is.False);
        remote.position = new Vector3(400, 0, 400);
        UnityEngine.Object.DestroyImmediate(departed.gameObject);
        Invoke(_map, "UpdateRemotePlayerMarkers");
        Assert.That(marker.gameObject.activeSelf, Is.True);
        Assert.That(departedMarker == null, Is.True);
        Assert.That(RemoteMarkers.Count, Is.EqualTo(1));
        remote.gameObject.SetActive(false);
        Invoke(_map, "UpdateRemotePlayerMarkers");
        Assert.That(RemoteMarkers.Count, Is.Zero);
        Assert.That(marker == null, Is.True);

        remote.gameObject.SetActive(true);
        Invoke(_map, "AddRemoteMarker", remote);
        marker = (RectTransform)RemoteMarkers[remote];
        Invoke(_map, "ClearView");
        Assert.That(RemoteMarkers.Count, Is.Zero);
        Assert.That(marker == null, Is.True);
    }

    [Test]
    public void RemotePlayerDiscovery_ExcludesSinglePlayerAndRemovesUnregisteredTargets()
    {
        var local = CreateMapTarget("SinglePlayer", new Vector3(500, 0, 500));
        local.gameObject.AddComponent(FindType("Player"));
        var removed = CreateMapTarget("OldRemote", new Vector3(550, 0, 500));
        Invoke(_map, "AddRemoteMarker", removed);
        var marker = (RectTransform)RemoteMarkers[removed];
        Invoke(_map, "RefreshRemotePlayers");
        Assert.That(RemoteMarkers.Count, Is.Zero);
        Assert.That(marker == null, Is.True);
    }

    [Test]
    public void ResourceMarkers_UseFiveIconsAndFollowVisibilityAndDestroyedState()
    {
        var logic = Activator.CreateInstance(FindType("WorldLogicData"), new Vector2Int(100, 100), 10);
        logic.GetType().GetProperty("SpawnTile").SetValue(logic, new Vector2Int(10, 10));
        var chunk = Invoke(logic, "GetOrCreateChunk", new Vector2Int(5, 5));
        var placements = (System.Collections.IList)Property(chunk, "DisposeDatas");
        string[] names = { "Branch", "Bush", "Fir", "Grass", "Rock" };
        int[] worldX = { 54, 50, 53, 51, 52 }; // 배치 목록 순서와 화면의 좌우 순서를 다르게 둔다.
        var resources = new object[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            var dispose = Activator.CreateInstance(FindType("DisposeData"));
            dispose.GetType().GetField("instanceId").SetValue(dispose, i + 1);
            dispose.GetType().GetField("prefabName").SetValue(dispose, "ResourceNode_" + names[i]);
            dispose.GetType().GetField("tilePosition").SetValue(dispose, new Vector2Int(worldX[i], 50));
            placements.Add(dispose);
            resources[i] = dispose;
        }

        var target = CreateMapTarget("ResourceMapTarget", new Vector3(50, 0, 50));
        Invoke(_map, "SetTarget", target);
        var data = Activator.CreateInstance(FindType("WorldMapData"),
            _texture, _sprite, new Vector2Int(100, 100), logic);
        Invoke(_map, "HandleDataChanged", data);
        Layout();
        Invoke(_map, "RefreshResourceMarkers");

        var markers = (System.Collections.IDictionary)Field(_map, "_resourceMarkers");
        Assert.That(markers.Count, Is.EqualTo(5));
        for (int i = 0; i < names.Length; i++)
        {
            object marker = markers[resources[i]];
            var expected = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                $"Assets/Sprites/Icons/ResourceNode/ResourceNode_{names[i]}_icon.png");
            Assert.That(Field(marker, "Icon"), Is.SameAs(expected));
        }
        foreach (Component graphic in ResourceGraphics) Assert.That(Property(graphic, "IconSize"), Is.EqualTo(16f));
        var drawOrder = OrderedResourceDisposes();
        int[] leftToRight = { 1, 3, 4, 2, 0 };
        for (int i = 1; i < leftToRight.Length; i++)
            Assert.That(drawOrder.IndexOf(resources[leftToRight[i]]),
                Is.GreaterThan(drawOrder.IndexOf(resources[leftToRight[i - 1]])));

        Invoke(chunk, "MarkObjectDestroyed", 1, 100f);
        Invoke(_map, "FlushResourceChanges");
        Assert.That(markers.Contains(resources[0]), Is.False);
        Invoke(chunk, "MarkObjectRespawned", 1);
        Invoke(_map, "FlushResourceChanges");
        Assert.That(markers.Contains(resources[0]), Is.True);

        var distantTarget = CreateMapTarget("DistantMapTarget", new Vector3(10, 0, 10));
        Invoke(_map, "SetTarget", distantTarget);
        Invoke(_map, "RefreshResourceMarkers");
        Assert.That(markers.Count, Is.Zero);
        Invoke(_map, "SetTarget", target);
        Invoke(_map, "RefreshResourceMarkers");
        Assert.That(markers.Count, Is.EqualTo(5));

        Invoke(_hud, "ToggleMap");
        Layout();
        Invoke(_map, "RefreshResourceMarkers");
        foreach (Component graphic in ResourceGraphics) Assert.That(Property(graphic, "IconSize"), Is.EqualTo(30f));

        // 제단의 비워 둔 공간에는 실제 자원이 없으므로 지도 아이콘도 표시하지 않는다.
        logic.GetType().GetProperty("SpawnTile").SetValue(logic, new Vector2Int(52, 50));
        Invoke(_map, "RefreshResourceMarkers");
        Assert.That(markers.Count, Is.Zero);

        Invoke(_map, "ClearView");
        Assert.That(markers.Count, Is.Zero);
    }

    private System.Collections.IDictionary RemoteMarkers =>
        (System.Collections.IDictionary)Field(_map, "_remoteMarkers");

    [Test]
    public void ResourceMarkers_BatchFiveHundredIconsAndKeepOrderAcrossZoomAndEvents()
    {
        var logic = Activator.CreateInstance(FindType("WorldLogicData"), new Vector2Int(100, 100), 10);
        logic.GetType().GetProperty("SpawnTile").SetValue(logic, new Vector2Int(10, 10));
        var resources = new object[500];
        for (int i = 0; i < resources.Length; i++)
        {
            var dispose = Activator.CreateInstance(FindType("DisposeData"));
            var tile = new Vector2Int(44 + i % 25 / 2, 44 + i / 25 / 2);
            dispose.GetType().GetField("instanceId").SetValue(dispose, i + 1);
            dispose.GetType().GetField("prefabName").SetValue(dispose, "ResourceNode_Fir");
            dispose.GetType().GetField("tilePosition").SetValue(dispose, tile);
            dispose.GetType().GetField("localOffset").SetValue(dispose,
                new Vector2(i % 2 * 0.5f, i / 25 % 2 * 0.5f));
            var chunk = Invoke(logic, "GetOrCreateChunk", new Vector2Int(tile.x / 10, tile.y / 10));
            ((System.Collections.IList)Property(chunk, "DisposeDatas")).Add(dispose);
            resources[i] = dispose;
        }
        var target = CreateMapTarget("DenseResourceTarget", new Vector3(50, 0, 50));
        Invoke(_map, "SetTarget", target);
        Invoke(_map, "HandleDataChanged", Activator.CreateInstance(FindType("WorldMapData"),
            _texture, _sprite, new Vector2Int(100, 100), logic));
        Layout();
        Invoke(_map, "RefreshResourceMarkers");
        var markers = (System.Collections.IDictionary)Field(_map, "_resourceMarkers");
        Assert.That(markers.Count, Is.EqualTo(500));
        var originalViews = new System.Collections.Generic.HashSet<Component>(ResourceGraphics);
        Assert.That(originalViews.Count, Is.EqualTo(2), "500개 자원을 X 구간별 두 Graphic으로 표시한다.");
        object first = markers[resources[0]];
        var layer = (RectTransform)Field(_map, "_resourceMarkerLayer");
        Assert.That(layer.parent, Is.SameAs(((Component)Property(_navigation, "MapImage")).transform));
        Assert.That(layer.GetComponent<Canvas>(), Is.Not.Null);
        Assert.That(layer.GetComponentsInChildren<Image>(true), Is.Empty,
            "자원별 Image는 생성하지 않는다.");
        Assert.That(_map.GetComponent<Canvas>(), Is.Not.Null);
        Assert.That(_map.GetComponent<Canvas>().overrideSorting, Is.False);

        // 지도만 이동/확대해도 자원 좌표를 다시 대입하지 않고 고정 크기로 따라와야 한다.
        _navigation.GetType().GetProperty("Zoom").SetValue(_navigation, 8f);
        target.position = new Vector3(51, 0, 51);
        Invoke(_map, "UpdatePlayerMarker");
        Layout();
        var content = ((Component)Property(_navigation, "MapImage")).GetComponent<RectTransform>();
        Assert.That(Field(first, "Normalized"), Is.EqualTo(new Vector2(0.44f, 0.44f)));
        foreach (Component graphic in ResourceGraphics)
        {
            Assert.That(Property(graphic, "IconSize"), Is.EqualTo(16f));
            Assert.That(((RectTransform)graphic.transform).rect.size,
                Is.EqualTo(content.rect.size + Vector2.one * 16f));
        }

        target.position = new Vector3(95, 0, 95);
        Invoke(_map, "UpdatePlayerMarker");
        Invoke(_map, "RefreshResourceMarkers");
        Assert.That(markers.Count, Is.Zero);
        Assert.That(layer.childCount, Is.EqualTo(2));
        foreach (Component view in originalViews) Assert.That(view.gameObject.activeSelf, Is.False);
        target.position = new Vector3(50, 0, 50);
        _navigation.GetType().GetProperty("Zoom").SetValue(_navigation, 4f);
        Invoke(_map, "UpdatePlayerMarker");
        Invoke(_map, "RefreshResourceMarkers");
        Assert.That(markers.Count, Is.EqualTo(500));
        Assert.That(layer.childCount, Is.EqualTo(2), "이동 후에도 기존 두 Graphic만 재사용한다.");
        foreach (Component view in ResourceGraphics) Assert.That(originalViews.Contains(view), Is.True);

        // 중간 마커 하나의 파괴/재생성에서도 나머지 UI와 좌우 순서는 유지한다.
        var victim = resources[250];
        object victimView = markers[victim];
        var victimTile = (Vector2Int)victim.GetType().GetField("tilePosition").GetValue(victim);
        var victimChunk = Invoke(logic, "GetOrCreateChunk", new Vector2Int(victimTile.x / 10, victimTile.y / 10));
        var unchangedGraphic = (Graphic)ResourceGraphics[1];
        int unrelatedMeshChanges = 0;
        UnityEngine.Events.UnityAction trackUnrelatedChange = () => unrelatedMeshChanges++;
        unchangedGraphic.RegisterDirtyVerticesCallback(trackUnrelatedChange);
        Invoke(_map, "FlushResourceChanges");
        Invoke(victimChunk, "MarkObjectDestroyed", 251, 100f);
        Invoke(_map, "FlushResourceChanges");
        Assert.That(markers.Count, Is.EqualTo(499));
        Invoke(victimChunk, "MarkObjectRespawned", 251);
        Invoke(_map, "FlushResourceChanges");
        Assert.That(markers[victim], Is.SameAs(victimView));
        Assert.That(unrelatedMeshChanges, Is.Zero, "다른 X 구간의 메시에는 갱신을 요청하지 않는다.");
        unchangedGraphic.UnregisterDirtyVerticesCallback(trackUnrelatedChange);
        var ordered = OrderedResourceDisposes();
        Assert.That(ordered.Count, Is.EqualTo(500));
        int previousX = -1;
        foreach (object dispose in ordered)
        {
            int x = ((Vector2Int)Field(dispose, "tilePosition")).x;
            Assert.That(x, Is.GreaterThanOrEqualTo(previousX));
            previousX = x;
        }

        // 작은 이동은 캐시한 여유 영역 안에서 부모만 움직이고, 범위를 벗어나면 목록을 바꾼다.
        target.position = new Vector3(50.1f, 0, 50);
        Invoke(_map, "UpdatePlayerMarker");
        Assert.That((bool)Field(_map, "_resourceViewDirty"), Is.False);
        target.position = new Vector3(95, 0, 95);
        Invoke(_map, "UpdatePlayerMarker");
        Invoke(_map, "FlushResourceChanges");
        Assert.That(markers.Count, Is.Zero);
        Invoke(victimChunk, "MarkObjectDestroyed", 251, 100f);
        var dirtyChunks = Field(_map, "_dirtyResourceChunks");
        Assert.That((int)Property(dirtyChunks, "Count"), Is.Zero, "화면 밖 청크 변경은 탐색을 예약하지 않는다.");
        target.position = new Vector3(50, 0, 50);
        Invoke(_map, "UpdatePlayerMarker");
        Invoke(_map, "FlushResourceChanges");
        Assert.That(markers.Count, Is.EqualTo(499), "다시 진입할 때는 화면 밖에서 바뀐 상태를 읽는다.");
        Assert.That(markers.Contains(victim), Is.False);
        Invoke(_map, "ClearView");
        Invoke(victimChunk, "MarkObjectRespawned", 251);
        Assert.That((int)Property(dirtyChunks, "Count"), Is.Zero, "해제한 지도의 청크 이벤트를 계속 받으면 안 된다.");
    }

    private Component[] ResourceGraphics => Array.FindAll(
        _map.GetComponentsInChildren(FindType("UI_ResourceMarkerGraphic"), true),
        graphic => graphic.gameObject.activeInHierarchy);

    private Component[] StructureGraphics => Array.FindAll(
        _map.GetComponentsInChildren(FindType("UI_StructureMarkerGraphic"), true),
        graphic => graphic.gameObject.activeInHierarchy);

    private int TotalStructureIconCount()
    {
        int total = 0;
        foreach (Component graphic in StructureGraphics)
            total += (int)Property(graphic, "IconCount");
        return total;
    }

    private System.Collections.Generic.List<object> OrderedResourceDisposes()
    {
        var result = new System.Collections.Generic.List<object>();
        foreach (object pair in (System.Collections.IEnumerable)Field(_map, "_resourceColumns"))
        {
            object column = Property(pair, "Value");
            foreach (object entry in (System.Collections.IEnumerable)Field(column, "Entries"))
                result.Add(Field(entry, "Dispose"));
        }
        return result;
    }

    [Test]
    public void ResourceStateNotifications_OnlyFireOnChangesAndIncludeSpawnExclusions()
    {
        var logic = Activator.CreateInstance(FindType("WorldLogicData"), new Vector2Int(100, 100), 10);
        var chunk = Invoke(logic, "GetOrCreateChunk", new Vector2Int(5, 5));
        int changes = 0;
        Action<object> callback = _ => changes++;
        chunk.GetType().GetEvent("OnResourceStateChanged").AddEventHandler(chunk, callback);
        Invoke(chunk, "MarkObjectDestroyed", 1, 100f);
        Invoke(chunk, "MarkObjectDestroyed", 1, 200f);
        Assert.That(changes, Is.EqualTo(1));
        Assert.That(Invoke(chunk, "MarkObjectRespawned", 1), Is.EqualTo(true));
        Assert.That(Invoke(chunk, "MarkObjectRespawned", 1), Is.EqualTo(false));
        Assert.That(changes, Is.EqualTo(2));
        Invoke(chunk, "MarkObjectDestroyed", 2, 100f);
        Invoke(chunk, "ClearDestroyedObjects");
        Invoke(chunk, "ClearDestroyedObjects");
        Assert.That(changes, Is.EqualTo(4));

        var spawner = Activator.CreateInstance(FindType("WorldObjectSpawner"));
        Invoke(spawner, "Initialize", logic, null);
        Invoke(spawner, "ExcludeSpawn", chunk, 1);
        Invoke(spawner, "ExcludeSpawn", chunk, 1);
        Assert.That(changes, Is.EqualTo(5));
        Assert.That(Invoke(spawner, "IsSpawnExcluded", 1), Is.EqualTo(true));
        Invoke(spawner, "Clear");
        Assert.That(changes, Is.EqualTo(6));
        Assert.That(Invoke(spawner, "IsSpawnExcluded", 1), Is.EqualTo(false));
    }

    [Test]
    public void FogOfWar_HidesUnexploredMarkersDimsVisitedAndRestoresFromBase64()
    {
        var logic = Activator.CreateInstance(FindType("WorldLogicData"), new Vector2Int(100, 100), 10);
        logic.GetType().GetProperty("SpawnTile").SetValue(logic, new Vector2Int(10, 10));

        var nearDispose = Activator.CreateInstance(FindType("DisposeData"));
        nearDispose.GetType().GetField("instanceId").SetValue(nearDispose, 101);
        nearDispose.GetType().GetField("prefabName").SetValue(nearDispose, "ResourceNode_Fir");
        nearDispose.GetType().GetField("tilePosition").SetValue(nearDispose, new Vector2Int(50, 50));
        ((System.Collections.IList)Property(Invoke(logic, "GetOrCreateChunk", new Vector2Int(5, 5)), "DisposeDatas")).Add(nearDispose);

        var farDispose = Activator.CreateInstance(FindType("DisposeData"));
        farDispose.GetType().GetField("instanceId").SetValue(farDispose, 102);
        farDispose.GetType().GetField("prefabName").SetValue(farDispose, "ResourceNode_Fir");
        farDispose.GetType().GetField("tilePosition").SetValue(farDispose, new Vector2Int(58, 58));
        ((System.Collections.IList)Property(Invoke(logic, "GetOrCreateChunk", new Vector2Int(5, 5)), "DisposeDatas")).Add(farDispose);

        var data = Activator.CreateInstance(FindType("WorldMapData"), _texture, _sprite, new Vector2Int(100, 100), logic);
        Invoke(data, "InitializeFogOfWar", 4f);

        var target = CreateMapTarget("FogLocalTarget", new Vector3(50, 0, 50));
        Invoke(_map, "SetTarget", target);
        Invoke(_map, "HandleDataChanged", data);
        Layout();
        Invoke(_map, "RefreshResourceMarkers");
        Invoke(_map, "FlushResourceChanges");

        var markers = (System.Collections.IDictionary)Field(_map, "_resourceMarkers");
        Assert.That(markers.Contains(nearDispose), Is.True, "현재 시야 내 자원은 표시되어야 한다.");
        Assert.That(markers.Contains(farDispose), Is.False, "미탐사 지역의 자원은 안개 아래 숨겨야 한다.");
        Assert.That(Invoke(data, "GetMarkerBrightness", new Vector2(0.5f, 0.5f)), Is.EqualTo(1f));

        // 플레이어가 이동하면 이전 위치는 탐사 완료(반투명 안개) 상태로 남고 마커 밝기가 낮아진다.
        target.position = new Vector3(58, 0, 58);
        Invoke(_map, "UpdatePlayerMarker");
        Invoke(_map, "FlushResourceChanges");
        Assert.That(markers.Contains(nearDispose), Is.True, "이미 탐사한 지역의 자원은 시야가 떠나도 유지된다.");
        Assert.That(markers.Contains(farDispose), Is.True, "새로 밝힌 지역의 자원이 나타난다.");
        Assert.That((float)Invoke(data, "GetMarkerBrightness", new Vector2(0.5f, 0.5f)), Is.LessThan(1f));

        string savedBase64 = (string)Invoke(data, "CaptureExploredFogBase64");
        Assert.That(savedBase64, Is.Not.Empty);

        var restoredData = Activator.CreateInstance(FindType("WorldMapData"), _texture, _sprite, new Vector2Int(100, 100), logic);
        Invoke(restoredData, "InitializeFogOfWar", 4f);
        Assert.That(Invoke(restoredData, "RestoreExploredFogBase64", savedBase64), Is.EqualTo(true));
        Assert.That(Invoke(restoredData, "IsNormalizedPositionExplored", new Vector2(0.5f, 0.5f)), Is.EqualTo(true));
        Assert.That(Invoke(restoredData, "IsNormalizedPositionExplored", new Vector2(0.9f, 0.9f)), Is.EqualTo(false));

        Invoke(data, "Dispose");
        Invoke(restoredData, "Dispose");
    }

    [Test]
    public void DeathMarker_UsesSkullIconTracksViewportOverwritesLatestAndClearsOnArrival()
    {
        var expectedSkull = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/00_Externals/PremiumAssets/UI/Artsystack - Fantasy RPG GUI/ResourcesData/Sprites/flaticon/white/btn_skull.png");
        Assert.That(expectedSkull, Is.Not.Null);
        Assert.That(Field(_map, "_deathMarkerIcon"), Is.SameAs(expectedSkull));

        var deathMarker = (RectTransform)Field(_map, "_deathMarker");
        var localMarker = (RectTransform)Field(_map, "_playerMarker");
        Assert.That(deathMarker, Is.Not.Null);
        Assert.That(deathMarker.gameObject.activeSelf, Is.False);
        Assert.That(deathMarker.parent, Is.SameAs(_navigation.transform));
        Assert.That(deathMarker.GetSiblingIndex(), Is.LessThan(localMarker.GetSiblingIndex()));
        var skullImage = deathMarker.GetComponent<Image>();
        Assert.That(skullImage, Is.Not.Null);
        Assert.That(skullImage.raycastTarget, Is.False);
        Assert.That(skullImage.preserveAspect, Is.True);
        Assert.That(skullImage.maskable, Is.True);

        var target = CreateMapTarget("DeathMarkerPlayer", new Vector3(500f, 0f, 500f));
        var player = target.gameObject.AddComponent(FindType("Player"));
        Assert.That((float)Property(player, "DeathMarkerArrivalRadius"), Is.EqualTo(3f).Within(0.001f));
        Invoke(_hud, "SetTarget", target);
        Layout();
        Assert.That(deathMarker.gameObject.activeSelf, Is.False);

        // 첫 사망 위치 기록 시 btn_skull 마커가 미니맵에 표시된다.
        Vector3 firstDeath = new Vector3(480f, 12f, 520f);
        Invoke(player, "SetDeathMarker", firstDeath);
        Assert.That(deathMarker.gameObject.activeSelf, Is.True);
        Assert.That(skullImage.sprite, Is.SameAs(expectedSkull));
        Assert.That(deathMarker.sizeDelta, Is.EqualTo(Vector2.one * 20f));
        AssertMarkerWorldPosition(deathMarker, firstDeath);

        // 전체 지도 전환 및 드래그·줌에서도 위치가 동기화되고 확대 크기가 유지된다.
        Invoke(_hud, "ToggleMap");
        Layout();
        Assert.That(deathMarker.sizeDelta, Is.EqualTo(Vector2.one * 52f));
        AssertMarkerWorldPosition(deathMarker, firstDeath);

        var eventsObject = new GameObject("DeathMarkerMapEvents", typeof(EventSystem));
        eventsObject.transform.SetParent(_root.transform, false);
        var pointer = new PointerEventData(eventsObject.GetComponent<EventSystem>())
        {
            button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, _navigation.transform.position),
            scrollDelta = Vector2.up * 2f,
            pointerId = -1
        };
        ((IScrollHandler)_navigation).OnScroll(pointer);
        ((IBeginDragHandler)_navigation).OnBeginDrag(pointer);
        pointer.delta = new Vector2(18f, -10f);
        pointer.position += pointer.delta;
        ((IDragHandler)_navigation).OnDrag(pointer);
        AssertMarkerWorldPosition(deathMarker, firstDeath);
        Assert.That(deathMarker.sizeDelta, Is.EqualTo(Vector2.one * 52f));

        // 재사망 시 최신 사망 위치 1개만 유지한다.
        Vector3 secondDeath = new Vector3(250f, 5f, 750f);
        Invoke(player, "SetDeathMarker", secondDeath);
        Assert.That((Vector3)Property(player, "DeathMarkerPosition"), Is.EqualTo(secondDeath));
        AssertMarkerWorldPosition(deathMarker, secondDeath);

        // 도착 반경(수평 3m) 밖에서는 유지되고, 안으로 진입하면 Y 차이와 무관하게 해제된다.
        Assert.That(Invoke(player, "TryClearDeathMarkerOnArrival", new Vector3(253.1f, 5f, 750f)), Is.False);
        Assert.That(Property(player, "HasDeathMarker"), Is.True);
        Assert.That(deathMarker.gameObject.activeSelf, Is.True);

        Assert.That(Invoke(player, "TryClearDeathMarkerOnArrival", new Vector3(252.9f, 45f, 750f)), Is.True);
        Assert.That(Property(player, "HasDeathMarker"), Is.False);
        Assert.That(deathMarker.gameObject.activeSelf, Is.False);

        // 월드 밖 좌표는 숨김, ClearView에서도 숨김 처리된다.
        Invoke(player, "SetDeathMarker", new Vector3(-5f, 0f, 500f));
        Assert.That(deathMarker.gameObject.activeSelf, Is.False);
        Invoke(player, "SetDeathMarker", secondDeath);
        Assert.That(deathMarker.gameObject.activeSelf, Is.True);
        Invoke(_map, "ClearView");
        Assert.That(deathMarker.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void StructureMarkers_DisplayPlacedStructuresStartSofaAndMonsterSpawnersWithFogAndCleanup()
    {
        var expectedGuids = new[]
        {
            "d02e50950306d6d4e8407332965c8ab0", // Tool_Bonefire_icon
            "9f2424195d5d54743858c8a89609fb8b", // Chest_icon
            "1e70c93b00b636149b148ad338f1d29a", // Tool_CookingPot_icon
            "d7bd6bf14e2f7f648bd9621bde7c1d54", // Tool_LunarForge_icon
            "9844010945c4dce4d8cebdaef273c0a8", // Tool_MoonGate_icon
            "87e7e2054dcb1154c8f5e7c77bbe9cd4", // Refrigerator_icon
            "2c3713783f457ab48a6554f0ad56d1d0", // Workbench_icon
            "28b95d469a037304684270f3d97f13ee", // btn_home_1
            "296300ed3f074c541a93e58a71345932"  // btn_danger
        };
        var boundSprites = new System.Collections.Generic.HashSet<Sprite>();
        var serializedMap = new UnityEditor.SerializedObject(_map);
        var iterator = serializedMap.GetIterator();
        while (iterator.NextVisible(true))
        {
            if (iterator.propertyType == UnityEditor.SerializedPropertyType.ObjectReference &&
                iterator.objectReferenceValue is Sprite boundSprite)
            {
                boundSprites.Add(boundSprite);
            }
        }
        foreach (string guid in expectedGuids)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var expectedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            Assert.That(expectedSprite, Is.Not.Null, $"스프라이트 로드 실패: {guid}");
            Assert.That(boundSprites.Contains(expectedSprite), Is.True, $"프리팹 바인딩 누락: {assetPath}");
        }
        var outlineProp = serializedMap.FindProperty("_enableIconOutline");
        Assert.That(outlineProp, Is.Not.Null);
        Assert.That(outlineProp.boolValue, Is.True);

        var structureLayer = (RectTransform)Field(_map, "_structureMarkerLayer");
        Assert.That(structureLayer, Is.Not.Null);
        var viewportTransform = (Transform)Property(_navigation, "Viewport");
        Assert.That(structureLayer.parent, Is.SameAs(viewportTransform));
        var teleportTransform = viewportTransform.Find("TeleportMarkers");
        Assert.That(teleportTransform, Is.Not.Null);
        var deathMarker = (RectTransform)Field(_map, "_deathMarker");
        var playerMarker = (RectTransform)Field(_map, "_playerMarker");
        Assert.That(structureLayer.GetSiblingIndex(), Is.GreaterThan(teleportTransform.GetSiblingIndex()));
        Assert.That(structureLayer.GetSiblingIndex(), Is.LessThan(deathMarker.GetSiblingIndex()));
        Assert.That(structureLayer.GetSiblingIndex(), Is.LessThan(playerMarker.GetSiblingIndex()));

        // Bonfire 등 Awake/OnEnable이 Main을 호출하지 않도록 PlacedStructure 컴포넌트만 사용해 7종을 등록한다.
        var logic = Activator.CreateInstance(FindType("WorldLogicData"), new Vector2Int(100, 100), 10);
        logic.GetType().GetProperty("SpawnTile").SetValue(logic, new Vector2Int(50, 50));
        var chunk5 = Invoke(logic, "GetOrCreateChunk", new Vector2Int(5, 5));
        var placements5 = (System.Collections.IList)Property(chunk5, "DisposeDatas");
        var chunk6 = Invoke(logic, "GetOrCreateChunk", new Vector2Int(6, 5));
        var placements6 = (System.Collections.IList)Property(chunk6, "DisposeDatas");

        // WorldSpawnArea.DefaultClearRadius(6m) 밖이면서 시야(10m) 안인 (58, 50)에 근거리 스포너를 배치한다.
        var nearSpawner = Activator.CreateInstance(FindType("DisposeData"));
        nearSpawner.GetType().GetField("instanceId").SetValue(nearSpawner, 901);
        nearSpawner.GetType().GetField("prefabName").SetValue(nearSpawner, "TreeantMinionAutmnSpawner");
        nearSpawner.GetType().GetField("tilePosition").SetValue(nearSpawner, new Vector2Int(58, 50));
        placements5.Add(nearSpawner);

        var farSpawner = Activator.CreateInstance(FindType("DisposeData"));
        farSpawner.GetType().GetField("instanceId").SetValue(farSpawner, 902);
        farSpawner.GetType().GetField("prefabName").SetValue(farSpawner, "TreeantMinionAutmnSpawner");
        farSpawner.GetType().GetField("tilePosition").SetValue(farSpawner, new Vector2Int(65, 50));
        placements6.Add(farSpawner);

        string[] placedKeys =
        {
            "Tool_Bonfire",
            "Tool_Chest",
            "Tool_CookingPot",
            "Tool_LunarForge",
            "Tool_MoonGate",
            "Tool_Refrigerator",
            "Tool_Workbench"
        };
        var placedType = FindType("PlacedStructure");
        var placedObjects = new GameObject[placedKeys.Length];
        for (int i = 0; i < placedKeys.Length; i++)
        {
            var go = new GameObject($"TestPlaced_{placedKeys[i]}");
            go.transform.SetParent(_root.transform, false);
            go.transform.position = new Vector3(49f + i * 0.25f, 0f, 50f);
            var placed = go.AddComponent(placedType);
            placedType.GetProperty("ItemKey").SetValue(placed, placedKeys[i]);
            placedObjects[i] = go;
        }

        // 배치 컴포넌트가 없는 고스트/프리뷰 오브젝트는 지도에 표시하지 않는다.
        var ghostObject = new GameObject("Tool_Chest");
        ghostObject.transform.SetParent(_root.transform, false);
        ghostObject.transform.position = new Vector3(50f, 0f, 50f);

        var data = Activator.CreateInstance(FindType("WorldMapData"), _texture, _sprite, new Vector2Int(100, 100), logic);
        Invoke(data, "InitializeFogOfWar", 10f);
        var target = CreateMapTarget("StructureTarget", new Vector3(50f, 0f, 50f));
        Invoke(_map, "SetTarget", target);
        Invoke(_map, "HandleDataChanged", data);
        Layout();
        Invoke(_map, "UpdatePlayerMarker");
        Invoke(_map, "LateUpdate");

        // 7종 PlacedStructure + StartSofa(50,50) + 시야 내 몬스터 스포너(58,50) = 9개 (미탐사 스포너 65,50 제외)
        Assert.That(TotalStructureIconCount(), Is.EqualTo(9));
        var initialGraphics = StructureGraphics;
        Assert.That(initialGraphics.Length, Is.GreaterThan(0));
        float compactOutlineWidth = (float)Property(initialGraphics[0], "OutlineWidth");
        Assert.That(compactOutlineWidth, Is.GreaterThan(0f));
        foreach (Component graphic in initialGraphics)
        {
            Assert.That((bool)Property(graphic, "OutlineEnabled"), Is.True);
            Assert.That((float)Property(graphic, "OutlineWidth"), Is.EqualTo(compactOutlineWidth));
        }

        // 미탐사 스포너(65, 50) 방향으로 이동하면 시야에 들어온 스포너가 추가되어 10개가 된다.
        target.position = new Vector3(65f, 0f, 50f);
        Invoke(_map, "UpdatePlayerMarker");
        Invoke(_map, "LateUpdate");
        Assert.That(TotalStructureIconCount(), Is.EqualTo(10));

        // 파괴된 몬스터 스포너 및 비활성화된 배치 구조물은 즉시 제외된다.
        Invoke(chunk5, "MarkObjectDestroyed", 901, 100f);
        Invoke(_map, "FlushResourceChanges");
        Invoke(_map, "LateUpdate");
        Assert.That(TotalStructureIconCount(), Is.EqualTo(9));

        placedObjects[1].SetActive(false);
        Invoke(_map, "LateUpdate");
        Assert.That(TotalStructureIconCount(), Is.EqualTo(8));

        // 확장 지도 전환 시 테두리 두께가 커지고 ClearView 호출 시 구조물 마커가 정리된다.
        Invoke(_hud, "ToggleMap");
        Layout();
        Invoke(_map, "LateUpdate");
        Assert.That(TotalStructureIconCount(), Is.EqualTo(8));
        foreach (Component graphic in StructureGraphics)
        {
            Assert.That((bool)Property(graphic, "OutlineEnabled"), Is.True);
            Assert.That((float)Property(graphic, "OutlineWidth"), Is.GreaterThan(compactOutlineWidth));
        }

        Invoke(_hud, "ToggleMap");
        Layout();
        Invoke(_map, "ClearView");
        Assert.That(TotalStructureIconCount(), Is.Zero);
        Invoke(data, "Dispose");
    }

    [Test]
    public void MapIconOutline_GeneratesSilhouetteOutlineVerticesForResourcesAndStructures()
    {
        var content = ((Component)Property(_navigation, "MapImage")).transform;
        var resourceObj = new GameObject("OutlineTestResourceGraphic", typeof(RectTransform));
        resourceObj.transform.SetParent(content, false);
        var resRect = (RectTransform)resourceObj.transform;
        resRect.anchorMin = Vector2.zero;
        resRect.anchorMax = Vector2.one;
        resRect.pivot = Vector2.one * 0.5f;
        resRect.anchoredPosition = Vector2.zero;
        var resourceGraphic = resourceObj.AddComponent(FindType("UI_ResourceMarkerGraphic"));
        var resIconType = FindType("UI_ResourceMarkerGraphic+Icon");
        var resListType = typeof(System.Collections.Generic.List<>).MakeGenericType(resIconType);
        var resList = (System.Collections.IList)Activator.CreateInstance(resListType);
        resList.Add(Activator.CreateInstance(resIconType, _sprite, new Vector2(0.5f, 0.5f), (byte)255));

        Invoke(resourceGraphic, "SetOutline", true, 1.5f, new Color32(18, 14, 14, 235));
        Invoke(resourceGraphic, "SetIcons", resList, 0, 1, _texture, 16f);
        Canvas.ForceUpdateCanvases();
        Assert.That(resRect.sizeDelta, Is.EqualTo(Vector2.one * 16f));
        Assert.That(resRect.rect.size, Is.EqualTo(((RectTransform)content).rect.size + Vector2.one * 16f));

        using (var vh = new VertexHelper())
        {
            Invoke(resourceGraphic, "OnPopulateMesh", vh);
            Assert.That(vh.currentVertCount, Is.EqualTo(36), "테두리 ON 시 8방향 실루엣(32) + 원본(4) = 36 정점이어야 한다.");
            UIVertex outlineVert = default;
            vh.PopulateUIVertex(ref outlineVert, 0);
            Assert.That(outlineVert.color.r, Is.EqualTo(18));
            UIVertex mainVert = default;
            vh.PopulateUIVertex(ref mainVert, 32);
            Assert.That(mainVert.color.r, Is.EqualTo(255));
        }

        Invoke(resourceGraphic, "SetOutline", false, 0f, new Color32(18, 14, 14, 235));
        using (var vh = new VertexHelper())
        {
            Invoke(resourceGraphic, "OnPopulateMesh", vh);
            Assert.That(vh.currentVertCount, Is.EqualTo(4), "테두리 OFF 시 원본 4 정점만 생성해야 한다.");
        }

        string bonfirePath = UnityEditor.AssetDatabase.GUIDToAssetPath("d02e50950306d6d4e8407332965c8ab0");
        var bonfireSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(bonfirePath);
        Assert.That(bonfireSprite, Is.Not.Null);

        var viewportTransform = (Transform)Property(_navigation, "Viewport");
        var structureObj = new GameObject("OutlineTestStructureGraphic", typeof(RectTransform));
        structureObj.transform.SetParent(viewportTransform, false);
        var structureRect = (RectTransform)structureObj.transform;
        structureRect.anchorMin = Vector2.zero;
        structureRect.anchorMax = Vector2.one;
        structureRect.offsetMin = structureRect.offsetMax = Vector2.zero;
        var structureGraphic = structureObj.AddComponent(FindType("UI_StructureMarkerGraphic"));
        var structIconType = FindType("UI_StructureMarkerGraphic+Icon");
        var structListType = typeof(System.Collections.Generic.List<>).MakeGenericType(structIconType);
        var structList = (System.Collections.IList)Activator.CreateInstance(structListType);
        structList.Add(Activator.CreateInstance(structIconType, bonfireSprite, new Vector2(0.5f, 0.5f), 24f, new Color32(255, 255, 255, 255)));

        Invoke(structureGraphic, "SetIcons", _navigation, bonfireSprite.texture, structList, true, 1.5f, new Color32(18, 14, 14, 235));
        Canvas.ForceUpdateCanvases();
        using (var vh = new VertexHelper())
        {
            Invoke(structureGraphic, "OnPopulateMesh", vh);
            Assert.That(vh.currentVertCount, Is.EqualTo(36));
            UIVertex bl = default, tr = default;
            vh.PopulateUIVertex(ref bl, 32);
            vh.PopulateUIVertex(ref tr, 34);
            float quadWidth = Mathf.Abs(tr.position.x - bl.position.x);
            float quadHeight = Mathf.Abs(tr.position.y - bl.position.y);
            float expectedRatio = bonfireSprite.rect.width / bonfireSprite.rect.height;
            Assert.That(quadWidth / quadHeight, Is.EqualTo(expectedRatio).Within(0.01f), "구조물 아이콘 종횡비가 유지되어야 한다.");
        }

        Invoke(structureGraphic, "SetIcons", _navigation, bonfireSprite.texture, structList, false, 0f, new Color32(18, 14, 14, 235));
        using (var vh = new VertexHelper())
        {
            Invoke(structureGraphic, "OnPopulateMesh", vh);
            Assert.That(vh.currentVertCount, Is.EqualTo(4));
        }
    }

    private void AssertMarkerWorldPosition(RectTransform marker, Vector3 worldPosition)
    {
        var image = (Component)Property(_navigation, "MapImage");
        var content = (RectTransform)image.transform;
        Vector2 normalized = new Vector2(worldPosition.x, worldPosition.z) / 1000f;
        Vector3 expected = content.TransformPoint(Vector2.Scale(normalized - Vector2.one * 0.5f, content.rect.size));
        Assert.That(Vector3.Distance(marker.position, expected), Is.LessThan(0.01f));
    }

    private Transform CreateMapTarget(string name, Vector3 position)
    {
        var target = new GameObject(name).transform;
        target.SetParent(_root.transform, false);
        target.position = position;
        return target;
    }

    private void AssertRemotePosition(Transform target)
    {
        Invoke(_map, "UpdateRemotePlayerMarkers");
        var marker = (RectTransform)RemoteMarkers[target];
        var image = (Component)Property(_navigation, "MapImage");
        var content = (RectTransform)image.transform;
        Vector2 normalized = new Vector2(target.position.x, target.position.z) / 1000f;
        Vector3 expected = content.TransformPoint(Vector2.Scale(normalized - Vector2.one * 0.5f, content.rect.size));
        Assert.That(Vector3.Distance(marker.position, expected), Is.LessThan(0.01f));
    }

    private void AssertDefaultView()
    {
        Assert.That((float)Property(_navigation, "Zoom"), Is.EqualTo(6f));
        AssertPlayerCentered();
    }

    private void AssertPlayerCentered()
    {
        Assert.That(Property(_navigation, "IsNavigating"), Is.False);
        var marker = (RectTransform)Field(_map, "_playerMarker");
        Assert.That(marker.anchoredPosition.magnitude, Is.LessThan(0.001f),
            "월드 가장자리에서도 플레이어가 표시 영역 중앙에 있어야 한다.");
    }

    private void Layout()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_hud.transform);
        Canvas.ForceUpdateCanvases();
        Invoke(_navigation, "Refresh");
    }

    private static Type FindType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static object Field(object target, string name) => target.GetType().GetField(name, Members).GetValue(target);
    private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static object Invoke(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Members).Invoke(target, args);
}
