using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MapViewportInputTests
{
    private GameObject _root;
    private Texture2D _texture;
    private Sprite _sprite;
    private Component _navigation;
    private RectTransform _viewport;
    private RectTransform _content;
    private GraphicRaycaster _raycaster;
    private EventSystem _events;
    private Camera _camera;
    private RenderTexture _renderTexture;
    private Transform _miniMap;
    private RectTransform _authoredMapRect;
    private Sprite _backgroundSprite;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("MapInputTest");
        _root.hideFlags = HideFlags.HideAndDontSave;
        var cameraObject = new GameObject("TestCamera", typeof(Camera));
        cameraObject.transform.SetParent(_root.transform, false);
        _camera = cameraObject.GetComponent<Camera>();
        _camera.enabled = false;
        _camera.orthographic = true;
        _renderTexture = new RenderTexture(800, 600, 24);
        _camera.targetTexture = _renderTexture;
        var canvasObject = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(_root.transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = _camera;
        canvas.planeDistance = 1f;
        _raycaster = canvasObject.GetComponent<GraphicRaycaster>();
        var eventObject = new GameObject("TestEventSystem", typeof(EventSystem));
        eventObject.transform.SetParent(_root.transform, false);
        _events = eventObject.GetComponent<EventSystem>();

        // 실제 HUD 프리팹의 미니맵만 복제하여 참조와 레이아웃 변경도 함께 검증한다.
        var hudPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/03_Prefabs/UI/World/UI_Hud_WorldState.prefab");
        Transform miniMapPrefab = hudPrefab.transform.Find("Top/UI_WorldMiniMap");
        _authoredMapRect = (RectTransform)miniMapPrefab.Find("MapImage");
        _backgroundSprite = miniMapPrefab.Find("BG").GetComponent<Image>().sprite;
        Component prefabPanel = miniMapPrefab.GetComponent(FindType("UI_Panel_WorldMiniMap"));
        var serializedPanel = new UnityEditor.SerializedObject(prefabPanel);
        Assert.That(serializedPanel.FindProperty("_mapImage").objectReferenceValue,
            Is.EqualTo(_authoredMapRect.GetComponent(FindType("UI_Image"))));
        Assert.That(serializedPanel.FindProperty("_playerMarker").objectReferenceValue,
            Is.EqualTo(_authoredMapRect.Find("PlayerMarker")));

        var frame = UnityEngine.Object.Instantiate(miniMapPrefab.gameObject, canvasObject.transform, false);
        _miniMap = frame.transform;
        var frameRect = (RectTransform)_miniMap;
        frameRect.anchorMin = frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        frameRect.anchoredPosition = Vector2.zero;
        var panel = frame.GetComponent(FindType("UI_Panel_WorldMiniMap"));
        panel.GetType().GetMethod("Initialize").Invoke(panel, null);
        _navigation = frame.GetComponentInChildren(FindType("UI_MapViewport"));
        Assert.That(_navigation, Is.Not.Null);
        _viewport = (RectTransform)_navigation.transform;

        Component image = (Component)GetProperty("MapImage");
        _content = (RectTransform)image.transform;
        _texture = new Texture2D(64, 64);
        _sprite = Sprite.Create(_texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        image.GetType().GetProperty("Sprite").SetValue(image, _sprite);
        image.GetComponent<Image>().enabled = true;
        Canvas.ForceUpdateCanvases();
        _navigation.GetType().GetMethod("Refresh").Invoke(_navigation, null);
        Canvas.ForceUpdateCanvases();
        // EditMode에서도 그래픽의 depth가 계산되도록 실제로 한 번 렌더링한다.
        _camera.Render();
    }

    [TearDown]
    public void TearDown()
    {
        if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        if (_sprite != null) UnityEngine.Object.DestroyImmediate(_sprite);
        if (_texture != null) UnityEngine.Object.DestroyImmediate(_texture);
        if (_renderTexture != null)
        {
            _renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(_renderTexture);
        }
    }

    [Test]
    public void MiniMap_UsesAuthoredMapImageRectAndPreservesBackground()
    {
        Assert.That(_viewport, Is.EqualTo(_miniMap.Find("MapImage")));
        Assert.That(_viewport.anchorMin, Is.EqualTo(_authoredMapRect.anchorMin));
        Assert.That(_viewport.anchorMax, Is.EqualTo(_authoredMapRect.anchorMax));
        Assert.That(_viewport.offsetMin, Is.EqualTo(_authoredMapRect.offsetMin));
        Assert.That(_viewport.offsetMax, Is.EqualTo(_authoredMapRect.offsetMax));
        Assert.That(_miniMap.Find("BG").GetComponent<Image>().sprite, Is.EqualTo(_backgroundSprite));
        Assert.That(_miniMap.Find("MapViewport"), Is.Null, "추가 패딩 창을 생성하지 않아야 한다.");
        Assert.That(_viewport.Find("PlayerMarker"), Is.Not.Null);
    }

    [Test]
    public void PaddedMiniMap_RaycastRoutesWheelAndDragToViewport()
    {
        var pointer = new PointerEventData(_events)
        {
            position = RectTransformUtility.WorldToScreenPoint(_camera, _viewport.TransformPoint(_viewport.rect.center)),
            button = PointerEventData.InputButton.Left,
            pointerId = -1,
            scrollDelta = Vector2.up
        };
        var hits = new List<RaycastResult>();
        _raycaster.Raycast(pointer, hits);
        Assert.That(hits.Count, Is.GreaterThan(0), "표시된 미니맵에서 실제 UI Raycast가 잡혀야 한다.");
        GameObject hit = hits[0].gameObject;
        Assert.That(ExecuteEvents.GetEventHandler<IScrollHandler>(hit), Is.EqualTo(_navigation.gameObject));
        Assert.That(ExecuteEvents.GetEventHandler<IDragHandler>(hit), Is.EqualTo(_navigation.gameObject));
        pointer.pointerCurrentRaycast = hits[0];
        pointer.pointerPressRaycast = hits[0];

        float oldZoom = (float)GetProperty("Zoom");
        ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.scrollHandler);
        Assert.That((float)GetProperty("Zoom"), Is.GreaterThan(oldZoom));
        Vector2 beforeDrag = _content.anchoredPosition;
        ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.beginDragHandler);
        pointer.delta = new Vector2(12f, -8f);
        pointer.position += pointer.delta;
        ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.dragHandler);
        Assert.That(_content.anchoredPosition.x, Is.GreaterThan(beforeDrag.x));
        Assert.That(_content.anchoredPosition.y, Is.LessThan(beforeDrag.y));
    }

    [Test]
    public void PaddedMiniMap_VisibleContentStillReceivesInputWhenTransparentViewportIsCulled()
    {
        // 투명 그래픽이 컬링되더라도 보이는 지도에서 입력을 받을 수 있어야 한다.
        _viewport.GetComponent<Graphic>().canvasRenderer.cull = true;
        var pointer = new PointerEventData(_events)
        {
            position = RectTransformUtility.WorldToScreenPoint(_camera, _viewport.TransformPoint(_viewport.rect.center))
        };
        var hits = new List<RaycastResult>();
        _raycaster.Raycast(pointer, hits);
        Assert.That(hits.Count, Is.GreaterThan(0));
        Assert.That(hits[0].gameObject, Is.EqualTo(_content.gameObject));
        Assert.That(ExecuteEvents.GetEventHandler<IScrollHandler>(hits[0].gameObject), Is.EqualTo(_navigation.gameObject));
    }

    [Test]
    public void PaddedMiniMap_ClippedContentDoesNotReceiveInputOnFramePadding()
    {
        Vector3 local = new Vector3(_viewport.rect.xMin - 2f, _viewport.rect.center.y, 0f);
        var pointer = new PointerEventData(_events)
        {
            position = RectTransformUtility.WorldToScreenPoint(_camera, _viewport.TransformPoint(local))
        };
        var hits = new List<RaycastResult>();
        _raycaster.Raycast(pointer, hits);
        Assert.That(hits.Exists(hit => hit.gameObject == _content.gameObject), Is.False);
    }

    private object GetProperty(string name) => _navigation.GetType().GetProperty(name).GetValue(_navigation);

    private static Type FindType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(name);
            if (type != null) return type;
        }
        throw new InvalidOperationException(name + " was not compiled.");
    }
}
