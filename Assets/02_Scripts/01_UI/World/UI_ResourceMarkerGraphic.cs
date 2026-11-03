using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>같은 텍스처의 자원 아이콘들을 고정 화면 크기의 사각형으로 묶어 그린다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UI_ResourceMarkerGraphic : MaskableGraphic
{
    // UGUI의 메시 정점 제한보다 충분히 작게 나눈다(아이콘당 정점 4개).
    public const int MaxIcons = 1024;

    public readonly struct Icon
    {
        public readonly Sprite Sprite;
        public readonly Vector2 NormalizedPosition;

        public Icon(Sprite sprite, Vector2 normalizedPosition)
        {
            Sprite = sprite;
            NormalizedPosition = normalizedPosition;
        }
    }

    private struct SpriteGeometry
    {
        public Vector4 Uv;
        public Vector4 Padding;
    }

    private readonly List<Icon> _icons = new();
    private readonly Dictionary<Sprite, SpriteGeometry> _spriteGeometry = new();
    private Texture _texture;
    private float _iconSize = 16f;

    public int IconCount => _icons.Count;
    public float IconSize => _iconSize;
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
        Color32 tint = color;
        foreach (Icon icon in _icons)
        {
            Sprite sprite = icon.Sprite;
            if (sprite == null || sprite.texture != _texture) continue;
            if (!_spriteGeometry.TryGetValue(sprite, out SpriteGeometry geometry))
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
                _spriteGeometry.Add(sprite, geometry);
            }

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
            Vector4 uv = geometry.Uv;
            int vertex = vh.currentVertCount;
            vh.AddVert(new Vector3(left, bottom), tint, new Vector2(uv.x, uv.y));
            vh.AddVert(new Vector3(left, top), tint, new Vector2(uv.x, uv.w));
            vh.AddVert(new Vector3(right, top), tint, new Vector2(uv.z, uv.w));
            vh.AddVert(new Vector3(right, bottom), tint, new Vector2(uv.z, uv.y));
            vh.AddTriangle(vertex, vertex + 1, vertex + 2);
            vh.AddTriangle(vertex + 2, vertex + 3, vertex);
        }
    }
}
