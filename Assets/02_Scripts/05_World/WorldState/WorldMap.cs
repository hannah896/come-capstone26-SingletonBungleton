using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 월드 전체 지도를 한 번 생성하고 소유하는 모델이다.
/// HUD 미니맵과 전체 지도 팝업은 모두 이 데이터만 표시한다.
/// </summary>
public class WorldMap : MonoBehaviour
{
    // 전체 지도와 미니맵이 한 장의 원본을 공유하므로 HUD 전용 해상도보다 높게 둔다.
    public const int DefaultMaxResolution = 1024;
    private const string MapSpriteAddressPrefix = "MiniMap_";

    private static readonly Color32 OceanColor = new Color32(42, 111, 163, 255);
    private static readonly Color32 MissingRegionColor = new Color32(104, 104, 104, 255);
    private static readonly Color32 BorderColor = new Color32(38, 38, 38, 255);

    [Header("Tile Sampling")]
    [SerializeField, Min(0.01f)] private float _tileWorldSize = 16f;

    public static WorldMap Instance { get; private set; }
    public WorldMapData CurrentData { get; private set; }

    public event Action<WorldMapData> OnDataChanged;
    public event Action OnDataCleared;

    protected virtual void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            return;
        }

        if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnDestroy()
    {
        if (Instance != this) return;

        Clear();
        Instance = null;
    }

    /// <summary>월드 로직 데이터와 바이옴 타일 스프라이트로 전체 지도 이미지를 만든다.</summary>
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

        string[] nodeTopKeys = BuildNodeTopKeyTable(graphData);
        Dictionary<string, MapTileSource> tileSources = await LoadTileSourcesAsync(nodeTopKeys, ct);
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
                Color32 color = GetTerrainColor(owner, nodeTopKeys, tileSources, biomeColors, worldX, worldY);

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

            if ((py & 15) == 15)
            {
                await UniTask.Yield(ct);
            }
        }

        ct.ThrowIfCancellationRequested();
        Texture2D texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
        {
            name = "Runtime_WorldMap",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, textureWidth, textureHeight), new Vector2(0.5f, 0.5f));
        sprite.name = "Runtime_WorldMap_Sprite";
        SetData(new WorldMapData(texture, sprite, terrainSize));
    }

    public void SetData(WorldMapData data)
    {
        if (ReferenceEquals(CurrentData, data)) return;

        WorldMapData previousData = CurrentData;
        CurrentData = data;
        previousData?.Dispose();

        if (CurrentData != null) OnDataChanged?.Invoke(CurrentData);
        else OnDataCleared?.Invoke();
    }

    public void Clear()
    {
        if (CurrentData == null) return;

        WorldMapData previousData = CurrentData;
        CurrentData = null;
        previousData.Dispose();
        OnDataCleared?.Invoke();
    }

    private static void ValidateInput(WorldLogicData logicData, WorldGraphData graphData, WorldSettings settings)
    {
        if (logicData == null) throw new ArgumentNullException(nameof(logicData));
        if (graphData == null) throw new ArgumentNullException(nameof(graphData));
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (logicData.TerritoryWorld == null) throw new ArgumentException("TerritoryWorld has not been generated.", nameof(logicData));
        if (logicData.HeightWorld == null) throw new ArgumentException("HeightWorld has not been generated.", nameof(logicData));
        if (logicData.TerrainSize.x <= 0 || logicData.TerrainSize.y <= 0)
            throw new ArgumentException("TerrainSize must be positive.", nameof(logicData));
    }

    private static void GetTextureSize(Vector2Int terrainSize, int maxResolution, out int width, out int height)
    {
        float scale = Mathf.Min(1f, Mathf.Max(1, maxResolution) / (float)Mathf.Max(terrainSize.x, terrainSize.y));
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

    private static string[] BuildNodeTopKeyTable(WorldGraphData graphData)
    {
        string[] topKeys = new string[graphData.Nodes.Count];
        for (int i = 0; i < graphData.Nodes.Count; i++)
        {
            topKeys[i] = graphData.Nodes[i]?.BiomeData?.TopKey;
        }
        return topKeys;
    }

    private static async UniTask<Dictionary<string, MapTileSource>> LoadTileSourcesAsync(IEnumerable<string> nodeTopKeys, CancellationToken ct)
    {
        var uniqueTopKeys = new HashSet<string>();
        foreach (string topKey in nodeTopKeys)
        {
            if (!string.IsNullOrWhiteSpace(topKey)) uniqueTopKeys.Add(topKey);
        }

        var tileSources = new Dictionary<string, MapTileSource>();
        foreach (string topKey in uniqueTopKeys)
        {
            ct.ThrowIfCancellationRequested();
            string address = MapSpriteAddressPrefix + topKey;
            Texture2D texture = await Extensions.LoadAssetAsync<Texture2D>(address, AssetCacheType.NonRequired, ct);
            if (texture == null)
            {
                Debug.LogWarning($"[WorldMap] Map texture Addressable was not found: {address}");
                continue;
            }

            if (!MapTileSource.TryCreate(texture, out MapTileSource tileSource))
            {
                Debug.LogWarning($"[WorldMap] Map texture needs Read/Write Enabled: {address}", texture);
                continue;
            }

            tileSources.Add(topKey, tileSource);
        }

        return tileSources;
    }

    private static int GetSourceCoordinate(int textureCoordinate, int textureSize, int terrainSize)
    {
        return Mathf.Clamp(Mathf.FloorToInt(((textureCoordinate + 0.5f) / textureSize) * terrainSize), 0, terrainSize - 1);
    }

    private Color32 GetTerrainColor(int owner, string[] nodeTopKeys, IReadOnlyDictionary<string, MapTileSource> tileSources, Color32[] biomeColors, int worldX, int worldY)
    {
        if (owner < 0) return OceanColor;
        if (owner >= biomeColors.Length) return MissingRegionColor;

        string topKey = nodeTopKeys[owner];
        if (!string.IsNullOrWhiteSpace(topKey) && tileSources.TryGetValue(topKey, out MapTileSource tileSource))
        {
            float tileWorldSize = Mathf.Max(0.01f, _tileWorldSize);
            return tileSource.Sample(worldX / tileWorldSize, worldY / tileWorldSize);
        }

        return biomeColors[owner];
    }

    private static Color32 ApplyHeightShade(Color32 color, float height01)
    {
        float multiplier = Mathf.Lerp(0.82f, 1.12f, height01);
        return new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * multiplier), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * multiplier), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * multiplier), 0, 255), color.a);
    }

    private static Color32 Blend(Color32 from, Color32 to, float t)
    {
        return new Color32(
            (byte)Mathf.RoundToInt(Mathf.Lerp(from.r, to.r, t)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(from.g, to.g, t)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(from.b, to.b, t)), 255);
    }

    private sealed class MapTileSource
    {
        private readonly Color32[] _pixels;
        private readonly int _textureWidth;
        private readonly int _xMin;
        private readonly int _yMin;
        private readonly int _width;
        private readonly int _height;

        private MapTileSource(Color32[] pixels, int textureWidth, Rect textureRect)
        {
            _pixels = pixels;
            _textureWidth = textureWidth;
            _xMin = Mathf.FloorToInt(textureRect.x);
            _yMin = Mathf.FloorToInt(textureRect.y);
            _width = Mathf.Max(1, Mathf.FloorToInt(textureRect.width));
            _height = Mathf.Max(1, Mathf.FloorToInt(textureRect.height));
        }

        public static bool TryCreate(Texture2D texture, out MapTileSource tileSource)
        {
            tileSource = null;
            if (texture == null || !texture.isReadable) return false;
            tileSource = new MapTileSource(
                texture.GetPixels32(),
                texture.width,
                new Rect(0f, 0f, texture.width, texture.height));
            return true;
        }

        public Color32 Sample(float u, float v)
        {
            int x = _xMin + Mathf.FloorToInt(Mathf.Repeat(u, 1f) * _width) % _width;
            int y = _yMin + Mathf.FloorToInt(Mathf.Repeat(v, 1f) * _height) % _height;
            return _pixels[(y * _textureWidth) + x];
        }
    }
}

/// <summary>WorldMap이 생성한 런타임 지도 리소스와 좌표 변환을 제공한다.</summary>
public sealed class WorldMapData : IDisposable
{
    public Texture2D Texture { get; }
    public Sprite Sprite { get; }
    public Vector2Int TerrainSize { get; }

    public WorldMapData(Texture2D texture, Sprite sprite, Vector2Int terrainSize)
    {
        Texture = texture;
        Sprite = sprite;
        TerrainSize = terrainSize;
    }

    public Vector2 NormalizeWorldPosition(Vector3 worldPosition)
    {
        return new Vector2(Mathf.Clamp01(worldPosition.x / TerrainSize.x), Mathf.Clamp01(worldPosition.z / TerrainSize.y));
    }

    public void Dispose()
    {
        if (Sprite != null) UnityEngine.Object.Destroy(Sprite);
        if (Texture != null) UnityEngine.Object.Destroy(Texture);
    }
}
