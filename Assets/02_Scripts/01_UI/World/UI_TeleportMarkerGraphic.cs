using UnityEngine;
using UnityEngine.UI;

/// <summary>언로드된 출입구도 표시하는 고정 화면 크기의 다이아몬드 마커입니다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UI_TeleportMarkerGraphic : MaskableGraphic
{
    private WorldMapData _data;
    private UI_MapViewport _navigation;
    private float _size = 18f;

    public void Configure(WorldMapData data, UI_MapViewport navigation, bool expanded)
    {
        _data = data;
        _navigation = navigation;
        _size = expanded ? 28f : 18f;
        raycastTarget = false;
        SetVerticesDirty();
    }

    public void RefreshPositions() => SetVerticesDirty();

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_data?.LogicData == null || _navigation == null) return;
        foreach (var entry in _data.LogicData.Teleports)
        {
            Vector2 normalized = _data.NormalizeWorldPosition(entry.Position(_data.LogicData));
            Vector2 center = _navigation.NormalizedToViewportPosition(normalized);
            Diamond(vh, center, _size * 0.5f, new Color32(30, 20, 35, 255));
            Diamond(vh, center, _size * 0.38f, entry.highlands ?
                new Color32(255, 220, 30, 255) : new Color32(185, 70, 255, 255));
        }
    }

    private static void Diamond(VertexHelper vh, Vector2 center, float radius, Color32 tint)
    {
        int start = vh.currentVertCount;
        vh.AddVert(center + Vector2.up * radius, tint, Vector2.zero);
        vh.AddVert(center + Vector2.right * radius, tint, Vector2.zero);
        vh.AddVert(center + Vector2.down * radius, tint, Vector2.zero);
        vh.AddVert(center + Vector2.left * radius, tint, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}
