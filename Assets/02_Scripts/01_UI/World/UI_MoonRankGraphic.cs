using UnityEngine;
using UnityEngine.UI;

/// <summary>직사각형 금속 명패와 세 개의 계급 문양을 한 메시로 그린다. 별도 원 장식이나 글자는 없다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UI_MoonRankGraphic : MaskableGraphic
{
    [SerializeField, Range(0, 3)] private int _rank = 1;
    [SerializeField] private Color _plateColor = new Color32(36, 39, 34, 255);
    [SerializeField] private Color _edgeColor = new Color32(139, 121, 88, 255);
    [SerializeField] private Color _insetColor = new Color32(69, 70, 56, 255);
    [SerializeField] private Color _goldColor = new Color32(230, 190, 114, 255);
    [SerializeField] private Color _emptyColor = new Color32(90, 93, 76, 255);

    public int Rank => _rank;

    public void SetRank(int rank)
    {
        rank = Mathf.Clamp(rank, 0, 3);
        if (_rank == rank) return;
        _rank = rank;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        float scale = Mathf.Min(rect.width / 154f, rect.height / 48f);
        if (scale <= 0f) return;
        Vector2 center = rect.center;

        // 154×48 시안을 기준으로 테두리와 문양 두께도 함께 맞춘다.
        AddRect(vh, center, new Vector2(154f, 48f) * scale, _edgeColor);
        AddRect(vh, center, new Vector2(152f, 46f) * scale, _plateColor);
        AddRect(vh, center, new Vector2(146f, 40f) * scale, _insetColor);
        AddRect(vh, center, new Vector2(144f, 38f) * scale, _plateColor);
        AddRect(vh, center + Vector2.left * (66f * scale), Vector2.one * (3f * scale), _edgeColor);
        AddRect(vh, center + Vector2.right * (66f * scale), Vector2.one * (3f * scale), _edgeColor);

        for (int i = 0; i < 3; i++)
        {
            Vector2 position = center + Vector2.right * ((i - 1) * 31f * scale);
            bool active = i < _rank;
            AddDiamond(vh, position, new Vector2(9.5f, 11.5f) * scale, active ? _goldColor : _emptyColor);
            // 비활성 단계는 속을 비워 색을 구분하기 어려워도 활성 문양과 구별된다.
            if (!active) AddDiamond(vh, position, new Vector2(7.8f, 9.5f) * scale, _plateColor);
        }
    }

    private void AddRect(VertexHelper vh, Vector2 center, Vector2 size, Color tint)
    {
        Vector2 half = size * 0.5f;
        AddQuad(vh, center + new Vector2(-half.x, -half.y), center + new Vector2(-half.x, half.y),
            center + new Vector2(half.x, half.y), center + new Vector2(half.x, -half.y), tint);
    }

    private void AddDiamond(VertexHelper vh, Vector2 center, Vector2 radius, Color tint)
    {
        AddQuad(vh, center + Vector2.left * radius.x, center + Vector2.up * radius.y,
            center + Vector2.right * radius.x, center + Vector2.down * radius.y, tint);
    }

    private void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
    {
        int index = vh.currentVertCount;
        Color32 vertexColor = tint * color;
        vh.AddVert(a, vertexColor, Vector2.zero);
        vh.AddVert(b, vertexColor, Vector2.zero);
        vh.AddVert(c, vertexColor, Vector2.zero);
        vh.AddVert(d, vertexColor, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index + 2, index + 3, index);
    }
}
