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
        Invoke(_hud, "SetTarget", target);
        _navigation.GetType().GetProperty("Zoom").SetValue(_navigation, 4f);
        _navigation.GetType().GetProperty("IsNavigating").SetValue(_navigation, true);
        _navigation.GetType().GetField("_center", Members).SetValue(_navigation, new Vector2(0.6f, 0.4f));
        Layout();
        Vector2 center = (Vector2)Field(_navigation, "_center");
        var image = (Component)Property(_navigation, "MapImage");

        for (int i = 0; i < 10; i++)
        {
            Invoke(_hud, "ToggleMap");
            Layout();
            Assert.That(Property(_hud, "WorldMapView"), Is.SameAs(_map));
            Assert.That(image.GetComponent<Image>().sprite, Is.SameAs(_sprite));
            Assert.That(Property(_navigation, "Zoom"), Is.EqualTo(4f));
            Assert.That(Vector2.Distance((Vector2)Field(_navigation, "_center"), center), Is.LessThan(0.001f));
            Assert.That(Property(_map, "Target"), Is.SameAs(target));
            Assert.That(_hud.GetComponentsInChildren(FindType("UI_Panel_WorldMap"), true).Length, Is.EqualTo(1));
            var marker = (RectTransform)Field(_map, "_playerMarker");
            Assert.That(marker.sizeDelta.x, Is.EqualTo(i % 2 == 0 ? 50f : 15f));
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

    [TestCase(false)]
    [TestCase(true)]
    public void DoubleClick_RestoresPlayerCenteredDefaultAfterExploration(bool expanded)
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
            position = RectTransformUtility.WorldToScreenPoint(null, viewport.position),
            scrollDelta = Vector2.up * 2f,
            pointerId = -1
        };
        ((IScrollHandler)_navigation).OnScroll(pointer);
        Assert.That((float)Property(_navigation, "Zoom"), Is.GreaterThan(4f));
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
        AssertDefaultView();
        Assert.That(pointer.used, Is.True);
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

    private void AssertDefaultView()
    {
        Assert.That((float)Property(_navigation, "Zoom"), Is.EqualTo(4f));
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
