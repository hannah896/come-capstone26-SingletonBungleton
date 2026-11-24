using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>같은 텍스처의 자원 아이콘들을 고정 화면 크기의 사각형으로 묶어 그린다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UI_ResourceMarkerGraphic : MaskableGraphic
{
    // UGUI의 메시 정점 제한보다 충분히 작게 나눈다(아이콘당 외곽선 포함 최대 정점 36개).
    public const int MaxIcons = 1024;

    private const float InvSqrt2 = 0.70710678f;

    internal static readonly Vector2[] SilhouetteOutlineDirections =
    {
        new Vector2( 1f,       0f),
        new Vector2( InvSqrt2, InvSqrt2),
        new Vector2( 0f,       1f),
        new Vector2(-InvSqrt2, InvSqrt2),
        new Vector2(-1f,       0f),
        new Vector2(-InvSqrt2,-InvSqrt2),
        new Vector2( 0f,      -1f),
        new Vector2( InvSqrt2,-InvSqrt2),
    };

    public readonly struct Icon
    {
        public readonly Sprite Sprite;
        public readonly Vector2 NormalizedPosition;
        public readonly byte Shade;

        public Icon(Sprite sprite, Vector2 normalizedPosition, byte shade = 255)
        {
            Sprite = sprite;
            NormalizedPosition = normalizedPosition;
            Shade = shade;
        }
    }

    internal struct SpriteGeometry
    {
        public Vector4 Uv;
        public Vector4 Padding;
    }

    private readonly List<Icon> _icons = new();
    private readonly Dictionary<Sprite, SpriteGeometry> _spriteGeometry = new();
    private Texture _texture;
    private float _iconSize = 16f;
    private bool _outlineEnabled = true;
    private float _outlineWidth = 1f;
    private Color32 _outlineColor = new Color32(18, 14, 14, 235);

    public int IconCount => _icons.Count;
    public float IconSize => _iconSize;
    public bool OutlineEnabled => _outlineEnabled;
    public float OutlineWidth => _outlineWidth;
    public Color32 OutlineColor => _outlineColor;
    public override Texture mainTexture => _texture != null ? _texture : Texture2D.whiteTexture;

    public void SetIcons(List<Icon> icons, int start, int count, Texture texture, float iconSize)
    {
        if (count > MaxIcons) throw new System.ArgumentOutOfRangeException(nameof(count));
        _icons.Clear();
        for (int i = 0; i < count; i++) _icons.Add(icons[start + i]);
        _spriteGeometry.Clear();
        _texture = texture;
        raycastTarget = false;
        SetIconSize(iconSize);
        SetVerticesDirty();
        SetMaterialDirty();
    }

    public void SetOutline(bool enabled, float width, Color32 color)
    {
        float clampedWidth = Mathf.Max(0f, width);
        if (_outlineEnabled == enabled
            && Mathf.Approximately(_outlineWidth, clampedWidth)
            && _outlineColor.r == color.r
            && _outlineColor.g == color.g
            && _outlineColor.b == color.b
            && _outlineColor.a == color.a)
            return;

        _outlineEnabled = enabled;
        _outlineWidth = clampedWidth;
        _outlineColor = color;
        SetVerticesDirty();
    }

    public void SetIconSize(float size)
    {
        _iconSize = Mathf.Max(1f, size);
        // 월드 가장자리 밖으로 반쯤 뻗은 아이콘도 Graphic 전체 컬링으로 사라지지 않게 한다.
        rectTransform.sizeDelta = Vector2.one * _iconSize;
        SetVerticesDirty();
    }

    public void ClearIcons()
    {
        _icons.Clear();
        _spriteGeometry.Clear();
        _texture = null;
        SetVerticesDirty();
        SetMaterialDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_texture == null) return;
        Rect rect = rectTransform.rect;
        Vector2 contentSize = rect.size - Vector2.one * _iconSize;
        if (contentSize.x <= 0f || contentSize.y <= 0f) return;
        bool pixelPerfect = canvas != null && canvas.renderMode != RenderMode.WorldSpace
            && canvas.scaleFactor != 0f && canvas.pixelPerfect;
        Color32 baseTint = color;
        Color32 outlineTint = new Color32(
            _outlineColor.r,
            _outlineColor.g,
            _outlineColor.b,
            (byte)(_outlineColor.a * baseTint.a / 255));

        foreach (Icon icon in _icons)
        {
            Sprite sprite = icon.Sprite;
            if (sprite == null || sprite.texture != _texture) continue;
            byte shade = icon.Shade;
            Color32 tint = shade == 255
                ? baseTint
                : new Color32(
                    (byte)(baseTint.r * shade / 255),
                    (byte)(baseTint.g * shade / 255),
                    (byte)(baseTint.b * shade / 255),
                    baseTint.a);
            SpriteGeometry geometry = GetSpriteGeometry(sprite, _spriteGeometry);

            Vector2 center = rect.center + Vector2.Scale(icon.NormalizedPosition - Vector2.one * 0.5f, contentSize);
            Vector2 min = center - Vector2.one * (_iconSize * 0.5f);
            Vector2 max = min + Vector2.one * _iconSize;
            if (pixelPerfect)
            {
                min = RectTransformUtility.PixelAdjustPoint(min, rectTransform, canvas);
                max = RectTransformUtility.PixelAdjustPoint(max, rectTransform, canvas);
            }
            Vector2 size = max - min;
            Vector4 p = geometry.Padding;
            float left = min.x + size.x * p.x;
            float bottom = min.y + size.y * p.y;
            float right = min.x + size.x * p.z;
            float top = min.y + size.y * p.w;
            AddOutlinedSpriteQuad(
                vh, left, bottom, right, top, geometry.Uv, tint,
                _outlineEnabled, _outlineWidth, outlineTint);
        }
    }

    internal static SpriteGeometry GetSpriteGeometry(Sprite sprite, Dictionary<Sprite, SpriteGeometry> cache)
    {
        if (!cache.TryGetValue(sprite, out SpriteGeometry geometry))
        {
            Vector4 padding = DataUtility.GetPadding(sprite);
            float width = Mathf.Max(1, Mathf.RoundToInt(sprite.rect.width));
            float height = Mathf.Max(1, Mathf.RoundToInt(sprite.rect.height));
            geometry = new SpriteGeometry
            {
                Uv = DataUtility.GetOuterUV(sprite),
                Padding = new Vector4(padding.x / width, padding.y / height,
                    1f - padding.z / width, 1f - padding.w / height)
            };
            cache.Add(sprite, geometry);
        }
        return geometry;
    }

    internal static Color32 ScaleColor(Color32 baseColor, Color32 tint)
    {
        return new Color32(
            (byte)(baseColor.r * tint.r / 255),
            (byte)(baseColor.g * tint.g / 255),
            (byte)(baseColor.b * tint.b / 255),
            (byte)(baseColor.a * tint.a / 255));
    }

    internal static void AddSpriteQuad(VertexHelper vh, float left, float bottom, float right, float top, Vector4 uv, Color32 tint)
    {
        int vertex = vh.currentVertCount;
        vh.AddVert(new Vector3(left, bottom), tint, new Vector2(uv.x, uv.y));
        vh.AddVert(new Vector3(left, top), tint, new Vector2(uv.x, uv.w));
        vh.AddVert(new Vector3(right, top), tint, new Vector2(uv.z, uv.w));
        vh.AddVert(new Vector3(right, bottom), tint, new Vector2(uv.z, uv.y));
        vh.AddTriangle(vertex, vertex + 1, vertex + 2);
        vh.AddTriangle(vertex + 2, vertex + 3, vertex);
    }

    internal static void AddOutlinedSpriteQuad(
        VertexHelper vh,
        float left,
        float bottom,
        float right,
        float top,
        Vector4 uv,
        Color32 mainTint,
        bool outlineEnabled,
        float outlineWidth,
        Color32 outlineTint)
    {
        if (outlineEnabled && outlineWidth > 0f && outlineTint.a > 0)
        {
            for (int i = 0; i < SilhouetteOutlineDirections.Length; i++)
            {
                Vector2 offset = SilhouetteOutlineDirections[i] * outlineWidth;
                AddSpriteQuad(vh, left + offset.x, bottom + offset.y, right + offset.x, top + offset.y, uv, outlineTint);
            }
        }

        AddSpriteQuad(vh, left, bottom, right, top, uv, mainTint);
    }
}
