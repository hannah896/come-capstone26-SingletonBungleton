using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>구조물·시작 제단·몬스터 스포너 아이콘을 고정 화면 크기로 묶어 그린다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UI_StructureMarkerGraphic : MaskableGraphic
{
    public const int MaxIcons = UI_ResourceMarkerGraphic.MaxIcons;

    public readonly struct Icon
    {
        public readonly Sprite Sprite;
        public readonly Vector2 NormalizedPosition;
        public readonly float Size;
        public readonly Color32 Tint;

        public Icon(Sprite sprite, Vector2 normalizedPosition, float size, Color32 tint)
        {
            Sprite = sprite;
            NormalizedPosition = normalizedPosition;
            Size = size;
            Tint = tint;
        }
    }

    private readonly List<Icon> _icons = new();
    private readonly Dictionary<Sprite, UI_ResourceMarkerGraphic.SpriteGeometry> _spriteGeometry = new();
    private UI_MapViewport _navigation;
    private Texture _texture;
    private bool _outlineEnabled = true;
    private float _outlineWidth = 1f;
    private Color32 _outlineColor = new Color32(18, 14, 14, 235);

    public int IconCount => _icons.Count;
    public bool OutlineEnabled => _outlineEnabled;
    public float OutlineWidth => _outlineWidth;
    public Color32 OutlineColor => _outlineColor;
    public override Texture mainTexture => _texture != null ? _texture : Texture2D.whiteTexture;

    public void SetIcons(
        UI_MapViewport navigation,
        Texture texture,
        List<Icon> icons,
        bool outlineEnabled,
        float outlineWidth,
        Color32 outlineColor)
    {
        SetIcons(navigation, texture, icons, 0, icons != null ? icons.Count : 0, outlineEnabled, outlineWidth, outlineColor);
    }

    public void SetIcons(
        UI_MapViewport navigation,
        Texture texture,
        List<Icon> icons,
        int start,
        int count,
        bool outlineEnabled,
        float outlineWidth,
        Color32 outlineColor)
    {
        if (count > MaxIcons) throw new System.ArgumentOutOfRangeException(nameof(count));

        _navigation = navigation;
        _texture = texture;
        _icons.Clear();
        if (icons != null)
        {
            for (int i = 0; i < count; i++)
                _icons.Add(icons[start + i]);
        }

        _spriteGeometry.Clear();
        _outlineEnabled = outlineEnabled;
        _outlineWidth = Mathf.Max(0f, outlineWidth);
        _outlineColor = outlineColor;
        raycastTarget = false;
        maskable = true;
        SetVerticesDirty();
        SetMaterialDirty();
    }

    public void RefreshPositions() => SetVerticesDirty();

    public void ClearIcons()
    {
        _navigation = null;
        _texture = null;
        _icons.Clear();
        _spriteGeometry.Clear();
        SetVerticesDirty();
        SetMaterialDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_texture == null || _navigation == null) return;

        Rect viewportRect = rectTransform.rect;
        bool pixelPerfect = canvas != null && canvas.renderMode != RenderMode.WorldSpace
            && canvas.scaleFactor != 0f && canvas.pixelPerfect;
        Color32 baseTint = color;
        float outlinePad = _outlineEnabled ? _outlineWidth : 0f;

        for (int i = 0; i < _icons.Count; i++)
        {
            Icon icon = _icons[i];
            Sprite sprite = icon.Sprite;
            if (sprite == null || sprite.texture != _texture) continue;

            float iconSize = Mathf.Max(1f, icon.Size);
            Vector2 center = _navigation.NormalizedToViewportPosition(icon.NormalizedPosition);
            float halfExtent = iconSize * 0.5f + outlinePad;
            if (center.x + halfExtent < viewportRect.xMin || center.x - halfExtent > viewportRect.xMax
                || center.y + halfExtent < viewportRect.yMin || center.y - halfExtent > viewportRect.yMax)
                continue;

            // 비정방형 스프라이트(모닥불, 몬스터 스포너 등)의 종횡비를 보존해 찌그러짐을 방지한다.
            float spriteWidth = Mathf.Max(1f, sprite.rect.width);
            float spriteHeight = Mathf.Max(1f, sprite.rect.height);
            float drawWidth = spriteWidth >= spriteHeight ? iconSize : iconSize * (spriteWidth / spriteHeight);
            float drawHeight = spriteHeight >= spriteWidth ? iconSize : iconSize * (spriteHeight / spriteWidth);

            Vector2 halfSize = new Vector2(drawWidth * 0.5f, drawHeight * 0.5f);
            Vector2 min = center - halfSize;
            Vector2 max = center + halfSize;
            if (pixelPerfect)
            {
                min = RectTransformUtility.PixelAdjustPoint(min, rectTransform, canvas);
                max = RectTransformUtility.PixelAdjustPoint(max, rectTransform, canvas);
            }

            UI_ResourceMarkerGraphic.SpriteGeometry geometry =
                UI_ResourceMarkerGraphic.GetSpriteGeometry(sprite, _spriteGeometry);
            Vector2 size = max - min;
            Vector4 p = geometry.Padding;
            float left = min.x + size.x * p.x;
            float bottom = min.y + size.y * p.y;
            float right = min.x + size.x * p.z;
            float top = min.y + size.y * p.w;

            Color32 mainTint = UI_ResourceMarkerGraphic.ScaleColor(baseTint, icon.Tint);
            Color32 outlineTint = new Color32(
                _outlineColor.r,
                _outlineColor.g,
                _outlineColor.b,
                (byte)(_outlineColor.a * mainTint.a / 255));

            UI_ResourceMarkerGraphic.AddOutlinedSpriteQuad(
                vh, left, bottom, right, top, geometry.Uv, mainTint,
                _outlineEnabled, _outlineWidth, outlineTint);
        }
    }
}
