using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// 미니맵과 월드맵의 표시 영역, 커서 기준 줌, 드래그 이동을 공통으로 처리한다.
/// 지도와 마커는 같은 좌표 변환을 쓰되 마커의 화면 크기는 유지한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class UI_MapViewport : MonoBehaviour, IScrollHandler, IBeginDragHandler,
    IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerClickHandler
{
    private static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);

    private RectTransform _viewport;
    private RectTransform _marker;
    private Vector2 _center = Middle;
    private Vector2 _markerPosition = Middle;
    private Vector2 _contentSize;
    private float _initialZoom;
    private float _maxZoom;
    private float _zoomStep;
    private bool _fillViewport;
    private bool _followTarget;
    private int? _dragPointerId;

    public UI_Image MapImage { get; private set; }
    public RectTransform Viewport => _viewport;
    public float Zoom { get; private set; }
    public bool IsNavigating { get; private set; }
    public Vector2 FocusPosition { get; set; } = Middle;

    /// <summary>기존의 단일 이미지 프리팹도 고정 표시 영역과 이동할 지도 이미지로 분리한다.</summary>
    public static UI_MapViewport Create(UI_Image source, RectTransform viewport,
        RectTransform marker, float initialZoom, float maxZoom, float zoomStep,
        bool fillViewport, bool followTarget)
    {
        if (source == null) return null;
        source.Initialize();
        if (viewport == null) viewport = source.Rect;

        UI_MapViewport navigation = viewport.GetComponent<UI_MapViewport>();
        if (navigation != null && navigation.MapImage != null) return navigation;
        if (navigation == null) navigation = viewport.gameObject.AddComponent<UI_MapViewport>();

        if (viewport == source.Rect)
        {
            // 레이아웃이 제어하는 원래 Rect는 고정하고, 자식 이미지만 확대/이동한다.
            source.Sprite = null;
            var content = new GameObject("MapContent", typeof(RectTransform), typeof(UI_Image));
            content.layer = viewport.gameObject.layer;
            content.transform.SetParent(viewport, false);
            content.transform.SetAsFirstSibling();
            source = content.GetComponent<UI_Image>();
            source.Initialize();
        }
        else
        {
            source.transform.SetParent(viewport, false);
            source.transform.SetAsFirstSibling();
        }

        if (!viewport.TryGetComponent<RectMask2D>(out _))
            viewport.gameObject.AddComponent<RectMask2D>();

        // 빈 여백 위에서도 입력을 받고, 지도 밖으로는 입력 영역이 확장되지 않게 한다.
        if (!viewport.TryGetComponent<Graphic>(out var hitArea))
        {
            hitArea = viewport.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
        }
        hitArea.enabled = true;
        hitArea.raycastTarget = true;
        // 투명한 입력 영역도 GraphicRaycaster의 검사 대상으로 유지한다.
        hitArea.canvasRenderer.cullTransparentMesh = false;
        // 보이는 지도에서 받은 이벤트는 부모 Viewport의 핸들러로 전달된다.
        // RectMask2D가 표시 영역 밖의 지도에 대한 Raycast도 차단한다.
        source.Image.raycastTarget = true;
        source.Image.type = Image.Type.Simple;
        source.Image.preserveAspect = false;
        source.Rect.localScale = Vector3.one;
        source.Rect.localRotation = Quaternion.identity;
        source.Rect.anchorMin = Middle;
        source.Rect.anchorMax = Middle;
        source.Rect.pivot = Middle;

        if (marker != null)
        {
            marker.SetParent(viewport, false);
            marker.SetAsLastSibling();
            marker.anchorMin = Middle;
            marker.anchorMax = Middle;
            foreach (Graphic graphic in marker.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }

        navigation._viewport = viewport;
        navigation.MapImage = source;
        navigation._marker = marker;
        navigation._maxZoom = Mathf.Max(1f, maxZoom);
        navigation._initialZoom = Mathf.Clamp(initialZoom, 1f, navigation._maxZoom);
        navigation._zoomStep = Mathf.Max(0.01f, zoomStep);
        navigation._fillViewport = fillViewport;
        navigation._followTarget = followTarget;
        navigation.ResetView();
        return navigation;
    }

    /// <summary>기본 배율과 중심으로 복귀한다. 미니맵은 플레이어 추적도 재개한다.</summary>
    public void ResetView()
    {
        Zoom = _initialZoom;
        IsNavigating = false;
        _dragPointerId = null;
        _center = _followTarget ? FocusPosition : Middle;
        Refresh();
    }

    public void Refresh()
    {
        if (MapImage == null || MapImage.Sprite == null) return;
        Vector2 size = _viewport.rect.size;
        if (size.x <= 0f || size.y <= 0f) return;

        _contentSize = MapViewportGeometry.ContentSize(size, MapImage.Sprite.rect.size, Zoom, _fillViewport);

        if (_followTarget && !IsNavigating) _center = FocusPosition;
        // 지도 가장자리에서 더 끌어도 지도 바깥의 빈 공간이 늘어나지 않도록 제한한다.
        _center = MapViewportGeometry.ClampCenter(_center, size, _contentSize);

        MapImage.Rect.sizeDelta = _contentSize;
        MapImage.Rect.anchoredPosition = Vector2.Scale(Middle - _center, _contentSize);
        if (_marker != null)
            _marker.anchoredPosition = Vector2.Scale(_markerPosition - _center, _contentSize);
    }

    public void SetMarkerPosition(Vector2 normalizedPosition)
    {
        _markerPosition = normalizedPosition;
        if (_marker != null)
            _marker.anchoredPosition = Vector2.Scale(_markerPosition - _center, _contentSize);
    }

    public void OnScroll(PointerEventData eventData)
    {
        Debug.Log($"OnScroll 감지됨");
        if (!CanNavigate() || !RectTransformUtility.RectangleContainsScreenPoint(
                _viewport, eventData.position, eventData.enterEventCamera)) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _viewport, eventData.position, eventData.enterEventCamera, out Vector2 cursor)) return;

        float ticks = eventData.scrollDelta.y;
#if ENABLE_INPUT_SYSTEM
        // Input System UI 모듈의 기본값은 휠 한 칸당 6이다. 레거시 입력과 감도를 맞춘다.
        if (eventData.currentInputModule is InputSystemUIInputModule inputModule)
            ticks /= Mathf.Max(0.01f, inputModule.scrollDeltaPerTick);
#endif
        float nextZoom = Mathf.Clamp(Zoom * Mathf.Pow(1f + _zoomStep, ticks), 1f, _maxZoom);
        if (Mathf.Approximately(Zoom, nextZoom)) return;

        Refresh();
        cursor -= _viewport.rect.center;
        float ratio = nextZoom / Zoom;
        _center = MapViewportGeometry.ZoomCenter(_center, cursor, _contentSize, ratio);
        Zoom = nextZoom;
        IsNavigating = true;
        Refresh();
        eventData.Use();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !CanNavigate()) return;
        Refresh();
        _dragPointerId = eventData.pointerId;
        IsNavigating = true;
        eventData.eligibleForClick = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left
            || _dragPointerId != eventData.pointerId || !CanNavigate()) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _viewport, eventData.position, eventData.pressEventCamera, out Vector2 current)) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _viewport, eventData.position - eventData.delta, eventData.pressEventCamera, out Vector2 previous)) return;

        Refresh();
        Vector2 delta = current - previous;
        _center -= new Vector2(delta.x / _contentSize.x, delta.y / _contentSize.y);
        Refresh();
        eventData.Use();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && _dragPointerId == eventData.pointerId)
            _dragPointerId = null;
    }

    // PointerDown을 받아야 이 오브젝트에 더블클릭 이벤트가 전달된다.
    public void OnPointerDown(PointerEventData eventData) { }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || eventData.clickCount != 2 || !CanNavigate()) return;
        ResetView();
        eventData.Use();
    }

    private bool CanNavigate() => isActiveAndEnabled && MapImage != null && MapImage.Sprite != null
        && _viewport.rect.width > 0f && _viewport.rect.height > 0f;

    private void OnDisable() => _dragPointerId = null;
}

/// <summary>Canvas 해상도와 무관한 지도 좌표 계산이다. 입력 위치는 표시 영역의 중심을 기준으로 한다.</summary>
internal static class MapViewportGeometry
{
    public static Vector2 ContentSize(Vector2 viewport, Vector2 sprite, float zoom, bool fill)
    {
        float scaleX = viewport.x / sprite.x;
        float scaleY = viewport.y / sprite.y;
        return sprite * ((fill ? Mathf.Max(scaleX, scaleY) : Mathf.Min(scaleX, scaleY)) * zoom);
    }

    public static Vector2 ClampCenter(Vector2 center, Vector2 viewport, Vector2 content)
    {
        Vector2 halfView = new Vector2(
            Mathf.Min(0.5f, viewport.x / content.x * 0.5f),
            Mathf.Min(0.5f, viewport.y / content.y * 0.5f));
        return new Vector2(
            Mathf.Clamp(center.x, halfView.x, 1f - halfView.x),
            Mathf.Clamp(center.y, halfView.y, 1f - halfView.y));
    }

    public static Vector2 ZoomCenter(Vector2 center, Vector2 cursor, Vector2 content, float ratio)
    {
        Vector2 underCursor = center + new Vector2(cursor.x / content.x, cursor.y / content.y);
        return underCursor - new Vector2(cursor.x / (content.x * ratio), cursor.y / (content.y * ratio));
    }
}
