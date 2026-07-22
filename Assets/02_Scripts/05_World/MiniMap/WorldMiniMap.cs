using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// 월드 생성 로직 데이터를 UI 미니맵에서 사용할 텍스처로 변환한다.
/// Terrain/청크 렌더링과 독립적이므로, 모든 청크가 로드되지 않은 상태에서도 사용할 수 있다.
/// </summary>
public class WorldMiniMap : MonoBehaviour
{
    public const int DefaultMaxResolution = 512;

    private static readonly Color32 OceanColor = new Color32(42, 111, 163, 255);
    private static readonly Color32 MissingRegionColor = new Color32(104, 104, 104, 255);
    private static readonly Color32 BorderColor = new Color32(38, 38, 38, 255);

    public static WorldMiniMap Instance { get; private set; }

    /// <summary>현재 월드에 대응하는 미니맵 데이터. 텍스처의 소유권은 WorldMiniMap에 있다.</summary>
    public WorldMiniMapData CurrentData { get; private set; }

    public event Action<WorldMiniMapData> OnDataChanged;
    public event Action OnDataCleared;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            return;
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Clear();
            Instance = null;
        }
    }

    /// <summary>
    /// 지형 구역, 바이옴 색상, 높이, 지역 경계를 한 장의 미니맵 텍스처로 만든다.
    /// </summary>
    /// <param name="logicData">TerritoryWorld, HeightWorld, BorderWorld가 채워진 월드 로직 데이터.</param>
    /// <param name="graphData">TerritoryWorld의 노드 인덱스를 바이옴 색상으로 변환할 그래프 데이터.</param>
    /// <param name="settings">해수면 및 최대 높이 계산에 사용할 월드 설정.</param>
    /// <param name="maxResolution">긴 변의 최대 픽셀 수. 256~512가 HUD 용도로 적절하다.</param>
    /// <param name="ct">월드 생성 취소 토큰.</param>
    public async UniTask BuildAsync(
        WorldLogicData logicData,
        WorldGraphData graphData,
        WorldSettings settings,
        int maxResolution = DefaultMaxResolution,
        CancellationToken ct = default)
    {
        ValidateInput(logicData, graphData, settings);
        ct.ThrowIfCancellationRequested();

        Vector2Int terrainSize = logicData.TerrainSize;
        GetTextureSize(terrainSize, maxResolution, out int textureWidth, out int textureHeight);

        Color32[] biomeColors = BuildBiomeColorTable(graphData);
        Color32[] pixels = new Color32[textureWidth * textureHeight];
        float seaLevel = settings.GetHeight(HeightLevel.Ocean);
        float maxHeight = Mathf.Max(seaLevel + 0.001f, settings.GetHeight(HeightLevel.Max));

        for (int py = 0; py < textureHeight; py++)
        {
            ct.ThrowIfCancellationRequested();

            int worldY = GetSourceCoordinate(py, textureHeight, terrainSize.y);
            int rowOffset = py * textureWidth;

            for (int px = 0; px < textureWidth; px++)
            {
                int worldX = GetSourceCoordinate(px, textureWidth, terrainSize.x);
                int owner = logicData.TerritoryWorld[worldX, worldY];

                Color32 color = GetTerrainColor(owner, biomeColors);
                if (owner >= 0)
                {
                    float height01 = Mathf.InverseLerp(seaLevel, maxHeight, logicData.HeightWorld[worldX, worldY]);
                    color = ApplyHeightShade(color, height01);

                    if (logicData.BorderWorld != null && logicData.BorderWorld[worldX, worldY] == -2)
                    {
                        color = Blend(color, BorderColor, 0.45f);
                    }
                }

                pixels[rowOffset + px] = color;
            }

            // 텍스처 준비 중 프레임을 오래 점유하지 않도록 주기적으로 양보한다.
            if ((py & 15) == 15)
            {
                await UniTask.Yield(ct);
            }
        }

        ct.ThrowIfCancellationRequested();

        Texture2D texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
        {
            name = "Runtime_MiniMap",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        SetData(new WorldMiniMapData(texture, terrainSize));
    }

    /// <summary>
    /// 외부에서 준비한 미니맵 데이터를 등록한다. 이전 런타임 텍스처는 자동으로 해제한다.
    /// </summary>
    public void SetData(WorldMiniMapData data)
    {
        if (ReferenceEquals(CurrentData, data)) return;

        WorldMiniMapData previousData = CurrentData;
        CurrentData = data;
        previousData?.Dispose();

        if (CurrentData != null)
        {
            OnDataChanged?.Invoke(CurrentData);
        }
        else
        {
            OnDataCleared?.Invoke();
        }
    }

    /// <summary>월드 교체 또는 종료 시 현재 텍스처를 해제하고 View에 비어 있음을 알린다.</summary>
    public void Clear()
    {
        if (CurrentData == null) return;

        WorldMiniMapData previousData = CurrentData;
        CurrentData = null;
        previousData.Dispose();
        OnDataCleared?.Invoke();
    }

    private static void ValidateInput(WorldLogicData logicData, WorldGraphData graphData, WorldSettings settings)
    {
        if (logicData == null) throw new ArgumentNullException(nameof(logicData));
        if (graphData == null) throw new ArgumentNullException(nameof(graphData));
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (logicData.TerritoryWorld == null) throw new ArgumentException("TerritoryWorld가 생성되지 않았습니다.", nameof(logicData));
        if (logicData.HeightWorld == null) throw new ArgumentException("HeightWorld가 생성되지 않았습니다.", nameof(logicData));
        if (logicData.TerrainSize.x <= 0 || logicData.TerrainSize.y <= 0)
            throw new ArgumentException("TerrainSize가 올바르지 않습니다.", nameof(logicData));
    }

    private static void GetTextureSize(Vector2Int terrainSize, int maxResolution, out int width, out int height)
    {
        int cappedResolution = Mathf.Max(1, maxResolution);
        float scale = Mathf.Min(1f, cappedResolution / (float)Mathf.Max(terrainSize.x, terrainSize.y));
        width = Mathf.Max(1, Mathf.RoundToInt(terrainSize.x * scale));
        height = Mathf.Max(1, Mathf.RoundToInt(terrainSize.y * scale));
    }

    private static Color32[] BuildBiomeColorTable(WorldGraphData graphData)
    {
        Color32[] colors = new Color32[graphData.Nodes.Count];
        for (int i = 0; i < graphData.Nodes.Count; i++)
        {
            Node node = graphData.Nodes[i];
            colors[i] = node?.BiomeData != null ? node.BiomeData.DebugColor : MissingRegionColor;
        }

        return colors;
    }

    private static int GetSourceCoordinate(int textureCoordinate, int textureSize, int terrainSize)
    {
        float normalized = (textureCoordinate + 0.5f) / textureSize;
        return Mathf.Clamp(Mathf.FloorToInt(normalized * terrainSize), 0, terrainSize - 1);
    }

    private static Color32 GetTerrainColor(int owner, Color32[] biomeColors)
    {
        if (owner < 0) return OceanColor;
        return owner < biomeColors.Length ? biomeColors[owner] : MissingRegionColor;
    }

    private static Color32 ApplyHeightShade(Color32 color, float height01)
    {
        // 평지는 약간 어둡게, 고지대는 약간 밝게 만들어 지형 윤곽만 보조한다.
        float multiplier = Mathf.Lerp(0.82f, 1.12f, height01);
        return new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * multiplier), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * multiplier), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * multiplier), 0, 255),
            color.a);
    }

    private static Color32 Blend(Color32 from, Color32 to, float t)
    {
        return new Color32(
            (byte)Mathf.RoundToInt(Mathf.Lerp(from.r, to.r, t)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(from.g, to.g, t)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(from.b, to.b, t)),
            255);
    }
}

/// <summary>
/// 미니맵 텍스처와 월드 좌표 정규화에 필요한 크기를 함께 보관한다.
/// Texture는 런타임 생성물이며, 월드 교체 시 Dispose로 해제해야 한다.
/// </summary>
public sealed class WorldMiniMapData : IDisposable
{
    public Texture2D Texture { get; }
    public Vector2Int TerrainSize { get; }

    public WorldMiniMapData(Texture2D texture, Vector2Int terrainSize)
    {
        Texture = texture;
        TerrainSize = terrainSize;
    }

    public Vector2 NormalizeWorldPosition(Vector3 worldPosition)
    {
        return new Vector2(
            Mathf.Clamp01(worldPosition.x / TerrainSize.x),
            Mathf.Clamp01(worldPosition.z / TerrainSize.y));
    }

    public void Dispose()
    {
        if (Texture != null)
        {
            UnityEngine.Object.Destroy(Texture);
        }
    }
}
