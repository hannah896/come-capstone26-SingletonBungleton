using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 월드 전체 지도를 한 번 생성하고 소유하는 모델이다.
/// 하나의 지도 UI가 이 데이터를 표시하며, HUD 크기와 확대 크기 사이에서 전환한다.
/// 바이옴별 전용 일러스트 컬러 팔레트와 해안선·등고선·경사 음영·종이 질감을 결합해 높은 가시성을 제공한다.
/// </summary>
public class WorldMap : MonoBehaviour
{
    // HUD와 확대 화면이 한 장의 원본을 공유하므로 확대 표시에도 충분한 해상도로 둔다.
    public const int DefaultMaxResolution = 1024;
    public const float DefaultSightRadius = 52f;

    private const ushort MaxCoastDistanceSteps = 300;
    private const float CoastStepScale = 3f;
    private const float ShallowWaterPixelRange = 14f;
    private const float CoastalBeachPixelRange = 4.5f;
    private const float CoastOutlinePixelWidth = 1.45f;
    private const float ContourHeightInterval = 12f;

    private static readonly Color32 DeepOceanColor = new Color32(52, 108, 148, 255);
    private static readonly Color32 MidOceanColor = new Color32(72, 136, 174, 255);
    private static readonly Color32 ShallowOceanColor = new Color32(114, 178, 206, 255);
    private static readonly Color32 WaterRippleColor = new Color32(146, 202, 224, 255);
    private static readonly Color32 CoastOutlineColor = new Color32(42, 48, 54, 255);
    private static readonly Color32 BorderColor = new Color32(54, 48, 44, 255);
    private static readonly Color32 MissingRegionColor = new Color32(148, 148, 148, 255);

    private static readonly BiomeMapPalette MissingBiomePalette = new BiomeMapPalette(
        MissingRegionColor,
        new Color32(134, 134, 134, 255),
        new Color32(106, 106, 106, 255),
        new Color32(176, 172, 162, 255));

    [Header("Tile Sampling")]
    [SerializeField, Min(0.01f)] private float _tileWorldSize = 16f;

    [Header("Fog of War")]
    [Tooltip("파티원 주변에 실시간으로 밝히는 전장의 안개 시야 반경(월드 미터)이다.")]
    [SerializeField, Min(4f)] private float _sightRadius = DefaultSightRadius;

    private readonly List<Player> _cachedPlayers = new();
    private readonly List<Vector3> _partyPositions = new();
    private float _nextPlayerSearchTime;

    public static WorldMap Instance { get; private set; }
    public WorldMapData CurrentData { get; private set; }
    public float SightRadius => Mathf.Max(4f, _sightRadius);

    public event Action<WorldMapData> OnDataChanged;
    public event Action OnDataCleared;

    private readonly struct BiomeMapPalette
    {
        public readonly Color32 BaseColor;
        public readonly Color32 AccentColor;
        public readonly Color32 CliffColor;
        public readonly Color32 CoastTint;

        public BiomeMapPalette(Color32 baseColor, Color32 accentColor, Color32 cliffColor, Color32 coastTint)
        {
            BaseColor = baseColor;
            AccentColor = accentColor;
            CliffColor = cliffColor;
            CoastTint = coastTint;
        }
    }

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

    /// <summary>월드 로직 데이터와 바이옴 전용 컬러 팔레트로 일러스트풍 전체 지도 이미지를 만든다.</summary>
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

        BiomeMapPalette[] palettes = BuildBiomePaletteTable(graphData);
        int totalPixels = textureWidth * textureHeight;
        int[] sampledOwners = new int[totalPixels];
        int[] sourceXTable = new int[textureWidth];
        int[] sourceYTable = new int[textureHeight];

        for (int px = 0; px < textureWidth; px++)
        {
            sourceXTable[px] = GetSourceCoordinate(px, textureWidth, terrainSize.x);
        }

        for (int py = 0; py < textureHeight; py++)
        {
            int worldY = GetSourceCoordinate(py, textureHeight, terrainSize.y);
            sourceYTable[py] = worldY;
            int rowOffset = py * textureWidth;

            for (int px = 0; px < textureWidth; px++)
            {
                sampledOwners[rowOffset + px] = logicData.TerritoryWorld[sourceXTable[px], worldY];
            }
        }

        ushort[] coastDistSteps = ComputeCoastDistanceSteps(sampledOwners, textureWidth, textureHeight);
        Color32[] pixels = new Color32[totalPixels];

        float seaLevel = settings.GetHeight(HeightLevel.Ocean);
        float maxHeight = Mathf.Max(seaLevel + 0.001f, settings.GetHeight(HeightLevel.Max));
        float invHeightRange = 1f / (maxHeight - seaLevel);
        float patchScale = 1f / Mathf.Max(4f, _tileWorldSize * 2.25f);

        for (int py = 0; py < textureHeight; py++)
        {
            ct.ThrowIfCancellationRequested();
            int worldY = sourceYTable[py];
            int worldYDown = Mathf.Max(0, worldY - 1);
            int worldYUp = Mathf.Min(terrainSize.y - 1, worldY + 1);
            int rowOffset = py * textureWidth;

            for (int px = 0; px < textureWidth; px++)
            {
                int idx = rowOffset + px;
                int worldX = sourceXTable[px];
                int owner = sampledOwners[idx];
                float coastPixels = coastDistSteps[idx] / CoastStepScale;
                float paperGrain = SamplePaperGrain(px, py, worldX, worldY);

                if (owner < 0)
                {
                    pixels[idx] = RenderOceanPixel(coastPixels, paperGrain);
                    continue;
                }

                BiomeMapPalette palette = owner < palettes.Length ? palettes[owner] : MissingBiomePalette;

                // 1. 저주파 패치 노이즈로 기본 평지 색과 보조 색을 부드럽게 블렌딩한다.
                float patchWeight = SamplePatchNoise(worldX * patchScale, worldY * patchScale);
                Color32 color = Blend(palette.BaseColor, palette.AccentColor, patchWeight * 0.72f);

                // 2. 인접 육지 타일 높이 기울기로 절벽/비탈 색상을 섞고 북서쪽 광원 기준 입체 음영(Hillshade)을 적용한다.
                int worldXLeft = Mathf.Max(0, worldX - 1);
                int worldXRight = Mathf.Min(terrainSize.x - 1, worldX + 1);
                float hCenter = logicData.HeightWorld[worldX, worldY];
                float hLeft = SampleLandHeight(logicData, worldXLeft, worldY, hCenter);
                float hRight = SampleLandHeight(logicData, worldXRight, worldY, hCenter);
                float hDown = SampleLandHeight(logicData, worldX, worldYDown, hCenter);
                float hUp = SampleLandHeight(logicData, worldX, worldYUp, hCenter);

                float dx = (hRight - hLeft) * 0.5f;
                float dy = (hUp - hDown) * 0.5f;
                float slope = Mathf.Sqrt((dx * dx) + (dy * dy));
                float cliffWeight = Mathf.SmoothStep(0.55f, 2.25f, slope);
                if (cliffWeight > 0.001f)
                {
                    color = Blend(color, palette.CliffColor, cliffWeight * 0.78f);
                }

                float height01 = Mathf.Clamp01((hCenter - seaLevel) * invHeightRange);
                float hillshade = Mathf.Clamp(((-dx * 0.7071f) + (dy * 0.7071f)) * 0.085f, -0.11f, 0.11f);
                float elevationShade = Mathf.Lerp(-0.04f, 0.05f, height01);
                color = MultiplyBrightness(color, 1f + hillshade + elevationShade + paperGrain);

                // 3. 해안가 안쪽 육지에 부드러운 해변/해안 림 톤을 더한다.
                if (coastPixels <= CoastalBeachPixelRange)
                {
                    float beachFactor = 1f - Mathf.SmoothStep(1f, CoastalBeachPixelRange, coastPixels);
                    float lowCoastFactor = 1f - Mathf.SmoothStep(0.15f, 0.45f, height01);
                    color = Blend(color, palette.CoastTint, beachFactor * lowCoastFactor * 0.42f);
                }

                // 4. 완만한 구릉/산악 구간에 일정 높이 간격마다 은은한 등고선을 넣어 높낮이를 표현한다.
                if (coastPixels > 2.2f && cliffWeight < 0.55f)
                {
                    float hNextX = px + 1 < textureWidth && sampledOwners[idx + 1] >= 0
                        ? logicData.HeightWorld[sourceXTable[px + 1], worldY]
                        : hCenter;
                    float hNextY = py + 1 < textureHeight && sampledOwners[idx + textureWidth] >= 0
                        ? logicData.HeightWorld[worldX, sourceYTable[py + 1]]
                        : hCenter;

                    if (IsContourLine(hCenter, hNextX, hNextY, seaLevel))
                    {
                        color = MultiplyBrightness(color, 0.91f);
                    }
                }

                // 5. 바이옴/지역 경계선을 또렷한 양피지 잉크 톤으로 표시한다.
                if (IsRegionBorder(logicData, sampledOwners, textureWidth, textureHeight, px, py, worldX, worldY, owner))
                {
                    color = Blend(color, BorderColor, 0.55f);
                }

                // 6. 육지와 바다가 맞닿는 최외곽 해안선에 선명한 잉크 외곽선을 그린다.
                if (coastPixels <= CoastOutlinePixelWidth)
                {
                    float outlineFactor = 1f - Mathf.SmoothStep(0.85f, CoastOutlinePixelWidth, coastPixels);
                    color = Blend(color, CoastOutlineColor, outlineFactor * 0.82f);
                }

                pixels[idx] = color;
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

        var mapData = new WorldMapData(texture, sprite, terrainSize, logicData);
        mapData.InitializeFogOfWar(SightRadius);

        // 시작 제단/스폰 위치 주변은 기본 탐사 영역으로 개방한다.
        if (logicData.SpawnTile.x >= 0 && logicData.SpawnTile.y >= 0)
        {
            mapData.RevealArea(
                new Vector3(logicData.SpawnTile.x, 0f, logicData.SpawnTile.y),
                SightRadius);
        }

        // 세이브 이어하기 또는 멀티플레이 참가 시 저장된 파티 탐사 상태를 즉시 복원한다.
        string savedFog = Main.Save?.PendingLoad?.world?.fogExploredBase64;
        if (!string.IsNullOrEmpty(savedFog))
        {
            mapData.RestoreExploredFogBase64(savedFog);
        }

        _nextPlayerSearchTime = 0f;
        SetData(mapData);
        RefreshPartyFogVision();
    }

    private void LateUpdate()
    {
        RefreshPartyFogVision();
    }

    /// <summary>활성 파티원(로컬 및 원격 플레이어)의 위치를 수집해 공유 전장의 안개를 갱신한다.</summary>
    public void RefreshPartyFogVision()
    {
        if (CurrentData == null || !CurrentData.HasFogOfWar) return;

        if (Time.unscaledTime >= _nextPlayerSearchTime)
        {
            _nextPlayerSearchTime = Time.unscaledTime + 0.4f;
            _cachedPlayers.Clear();
            foreach (Player player in UnityEngine.Object.FindObjectsByType<Player>(FindObjectsSortMode.None))
            {
                if (player != null && player.isActiveAndEnabled)
                {
                    _cachedPlayers.Add(player);
                }
            }
        }

        _partyPositions.Clear();
        for (int i = _cachedPlayers.Count - 1; i >= 0; i--)
        {
            Player player = _cachedPlayers[i];
            if (player == null || !player.isActiveAndEnabled)
            {
                _cachedPlayers.RemoveAt(i);
                continue;
            }
            _partyPositions.Add(player.transform.position);
        }

        if (_partyPositions.Count > 0)
        {
            CurrentData.UpdatePartyVision(_partyPositions, SightRadius, out _);
        }
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

    private static int GetSourceCoordinate(int textureCoordinate, int textureSize, int terrainSize)
    {
        return Mathf.Clamp(Mathf.FloorToInt(((textureCoordinate + 0.5f) / textureSize) * terrainSize), 0, terrainSize - 1);
    }

    private static BiomeMapPalette[] BuildBiomePaletteTable(WorldGraphData graphData)
    {
        BiomeMapPalette[] palettes = new BiomeMapPalette[graphData.Nodes.Count];
        for (int i = 0; i < graphData.Nodes.Count; i++)
        {
            palettes[i] = ResolveBiomePalette(graphData.Nodes[i]?.BiomeData);
        }
        return palettes;
    }

    private static BiomeMapPalette ResolveBiomePalette(BiomeData biome)
    {
        if (biome == null) return MissingBiomePalette;

        string topKey = biome.TopKey ?? string.Empty;
        string assetName = biome.name ?? string.Empty;

        // RockyDesert는 BiomeType이 Rocky로 설정되어 있으므로 TopKey/이름으로 먼저 구분한다.
        if (topKey.IndexOf("RockyDesert", StringComparison.OrdinalIgnoreCase) >= 0 ||
            assetName.IndexOf("RockyDesert", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return new BiomeMapPalette(
                new Color32(206, 158, 118, 255),
                new Color32(190, 142, 102, 255),
                new Color32(152, 108, 76, 255),
                new Color32(224, 190, 148, 255));
        }

        if (assetName.IndexOf("Start", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return new BiomeMapPalette(
                new Color32(162, 198, 132, 255),
                new Color32(146, 184, 116, 255),
                new Color32(116, 138, 98, 255),
                new Color32(226, 212, 168, 255));
        }

        return biome.BiomeType switch
        {
            BiomeType.Plains => new BiomeMapPalette(
                new Color32(152, 190, 124, 255),
                new Color32(136, 176, 108, 255),
                new Color32(108, 132, 92, 255),
                new Color32(226, 210, 166, 255)),

            BiomeType.Forest => new BiomeMapPalette(
                new Color32(104, 154, 116, 255),
                new Color32(90, 140, 102, 255),
                new Color32(72, 108, 82, 255),
                new Color32(214, 200, 158, 255)),

            BiomeType.Desert => new BiomeMapPalette(
                new Color32(230, 202, 144, 255),
                new Color32(216, 186, 128, 255),
                new Color32(180, 148, 96, 255),
                new Color32(238, 218, 170, 255)),

            BiomeType.Snow => new BiomeMapPalette(
                new Color32(226, 236, 244, 255),
                new Color32(210, 224, 236, 255),
                new Color32(164, 184, 202, 255),
                new Color32(236, 242, 248, 255)),

            BiomeType.Swamp => new BiomeMapPalette(
                new Color32(134, 126, 140, 255),
                new Color32(120, 112, 126, 255),
                new Color32(94, 86, 100, 255),
                new Color32(176, 168, 162, 255)),

            BiomeType.Savanna => new BiomeMapPalette(
                new Color32(212, 180, 106, 255),
                new Color32(196, 164, 90, 255),
                new Color32(158, 128, 70, 255),
                new Color32(228, 204, 152, 255)),

            BiomeType.Rocky => new BiomeMapPalette(
                new Color32(160, 166, 172, 255),
                new Color32(144, 150, 158, 255),
                new Color32(114, 120, 128, 255),
                new Color32(192, 194, 192, 255)),

            BiomeType.Highlands => new BiomeMapPalette(
                new Color32(212, 140, 96, 255),
                new Color32(196, 124, 82, 255),
                new Color32(156, 94, 60, 255),
                new Color32(224, 184, 146, 255)),

            _ => MissingBiomePalette
        };
    }

    /// <summary>
    /// 2-Pass Chamfer(3-4) 거리 변환으로 모든 픽셀에서 가장 가까운 해안선까지의 픽셀 거리를 계산한다.
    /// </summary>
    private static ushort[] ComputeCoastDistanceSteps(int[] sampledOwners, int width, int height)
    {
        int length = width * height;
        ushort[] dist = new ushort[length];

        for (int py = 0; py < height; py++)
        {
            int rowOffset = py * width;
            for (int px = 0; px < width; px++)
            {
                int idx = rowOffset + px;
                bool isLand = sampledOwners[idx] >= 0;
                bool touchesCoast =
                    (px > 0 && (sampledOwners[idx - 1] >= 0) != isLand) ||
                    (px + 1 < width && (sampledOwners[idx + 1] >= 0) != isLand) ||
                    (py > 0 && (sampledOwners[idx - width] >= 0) != isLand) ||
                    (py + 1 < height && (sampledOwners[idx + width] >= 0) != isLand);

                dist[idx] = touchesCoast ? (ushort)3 : MaxCoastDistanceSteps;
            }
        }

        // Forward pass
        for (int py = 0; py < height; py++)
        {
            int rowOffset = py * width;
            for (int px = 0; px < width; px++)
            {
                int idx = rowOffset + px;
                ushort d = dist[idx];
                if (d <= 3) continue;

                if (px > 0)
                {
                    ushort cand = (ushort)(dist[idx - 1] + 3);
                    if (cand < d) d = cand;
                }
                if (py > 0)
                {
                    int prevRowIdx = idx - width;
                    ushort cand = (ushort)(dist[prevRowIdx] + 3);
                    if (cand < d) d = cand;

                    if (px > 0)
                    {
                        cand = (ushort)(dist[prevRowIdx - 1] + 4);
                        if (cand < d) d = cand;
                    }
                    if (px + 1 < width)
                    {
                        cand = (ushort)(dist[prevRowIdx + 1] + 4);
                        if (cand < d) d = cand;
                    }
                }
                dist[idx] = d;
            }
        }

        // Backward pass
        for (int py = height - 1; py >= 0; py--)
        {
            int rowOffset = py * width;
            for (int px = width - 1; px >= 0; px--)
            {
                int idx = rowOffset + px;
                ushort d = dist[idx];
                if (d <= 3) continue;

                if (px + 1 < width)
                {
                    ushort cand = (ushort)(dist[idx + 1] + 3);
                    if (cand < d) d = cand;
                }
                if (py + 1 < height)
                {
                    int nextRowIdx = idx + width;
                    ushort cand = (ushort)(dist[nextRowIdx] + 3);
                    if (cand < d) d = cand;

                    if (px + 1 < width)
                    {
                        cand = (ushort)(dist[nextRowIdx + 1] + 4);
                        if (cand < d) d = cand;
                    }
                    if (px > 0)
                    {
                        cand = (ushort)(dist[nextRowIdx - 1] + 4);
                        if (cand < d) d = cand;
                    }
                }
                dist[idx] = d;
            }
        }

        return dist;
    }

    private static Color32 RenderOceanPixel(float coastPixels, float paperGrain)
    {
        Color32 waterColor;
        if (coastPixels <= ShallowWaterPixelRange)
        {
            float t = Mathf.Clamp01((coastPixels - 1f) / (ShallowWaterPixelRange - 1f));
            float smooth = t * t * (3f - (2f * t));
            waterColor = smooth < 0.5f
                ? Blend(ShallowOceanColor, MidOceanColor, smooth * 2f)
                : Blend(MidOceanColor, DeepOceanColor, (smooth - 0.5f) * 2f);

            // 해안선을 따라 은은한 일러스트 물결 라인을 두 줄 추가한다.
            float ripple1 = 1f - Mathf.Clamp01(Mathf.Abs(coastPixels - 3.0f) * 1.4f);
            float ripple2 = 1f - Mathf.Clamp01(Mathf.Abs(coastPixels - 6.8f) * 1.2f);
            float ripple = Mathf.Max(ripple1 * 0.35f, ripple2 * 0.22f);
            if (ripple > 0.01f)
            {
                waterColor = Blend(waterColor, WaterRippleColor, ripple);
            }
        }
        else
        {
            waterColor = DeepOceanColor;
        }

        return MultiplyBrightness(waterColor, 1f + (paperGrain * 0.65f));
    }

    private static float SamplePatchNoise(float u, float v)
    {
        float n1 = Mathf.PerlinNoise(u + 17.3f, v + 41.7f);
        float n2 = Mathf.PerlinNoise((u * 2.15f) + 83.1f, (v * 2.15f) + 19.4f);
        return Mathf.Clamp01((n1 * 0.7f) + (n2 * 0.3f));
    }

    private static float SamplePaperGrain(int px, int py, int worldX, int worldY)
    {
        // 픽셀 단위 미세 종이 입자와 완만한 양피지 농담을 결합해 모아레 현상 없이 매트한 질감을 만든다.
        uint hash = (uint)((px * 374761393) + (py * 668265263) + (worldX * 1442695041));
        hash = (hash ^ (hash >> 13)) * 1274126177u;
        float fineGrain = (((hash ^ (hash >> 16)) & 0xFFu) / 255f) - 0.5f;
        float paperWave = Mathf.PerlinNoise((worldX * 0.018f) + 5.7f, (worldY * 0.018f) + 11.3f) - 0.5f;
        return (fineGrain * 0.024f) + (paperWave * 0.032f);
    }

    private static float SampleLandHeight(WorldLogicData logicData, int x, int y, float fallbackHeight)
    {
        return logicData.TerritoryWorld[x, y] >= 0 ? logicData.HeightWorld[x, y] : fallbackHeight;
    }

    private static bool IsContourLine(float hCenter, float hRight, float hUp, float seaLevel)
    {
        float relCenter = hCenter - seaLevel;
        if (relCenter < ContourHeightInterval * 0.5f) return false;

        int bandCenter = Mathf.FloorToInt(relCenter / ContourHeightInterval);
        bool crossRight = Mathf.Abs(hRight - hCenter) < ContourHeightInterval &&
                          bandCenter != Mathf.FloorToInt((hRight - seaLevel) / ContourHeightInterval);
        bool crossUp = Mathf.Abs(hUp - hCenter) < ContourHeightInterval &&
                        bandCenter != Mathf.FloorToInt((hUp - seaLevel) / ContourHeightInterval);
        return crossRight || crossUp;
    }

    private static bool IsRegionBorder(
        WorldLogicData logicData,
        int[] sampledOwners,
        int width,
        int height,
        int px,
        int py,
        int worldX,
        int worldY,
        int owner)
    {
        if (logicData.BorderWorld != null && logicData.BorderWorld[worldX, worldY] == -2)
        {
            return true;
        }

        int idx = (py * width) + px;
        if (px + 1 < width)
        {
            int rightOwner = sampledOwners[idx + 1];
            if (rightOwner >= 0 && rightOwner != owner) return true;
        }
        if (py + 1 < height)
        {
            int upOwner = sampledOwners[idx + width];
            if (upOwner >= 0 && upOwner != owner) return true;
        }

        return false;
    }

    private static Color32 MultiplyBrightness(Color32 color, float multiplier)
    {
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

/// <summary>WorldMap이 생성한 런타임 지도 리소스와 좌표 변환 및 2단계 전장의 안개 상태를 제공한다.</summary>
public sealed class WorldMapData : IDisposable
{
    public const int FogResolution = 512;
    public const int FogCellCount = FogResolution * FogResolution;
    public const int FogSaveResolution = 256;
    public const int FogSaveCellCount = FogSaveResolution * FogSaveResolution;
    public const int FogMaskByteLength = FogSaveCellCount / 8;

    private const byte UnexploredFogAlpha = 255;
    // 검은 안개를 약 60% 합성해 탐사 완료 영역의 지도 밝기를 절반정도로 유지한다.
    private const byte ExploredFogAlpha = 153;
    private const byte VisibleFogAlpha = 0;
    private const byte ExploredThreshold = 64;
    private const float PositionChangeThresholdSqr = 0.25f;

    private static readonly Color32 BaseFogColor = new Color32(0, 0, 0, UnexploredFogAlpha);

    private byte[] _exploredWeights;
    private byte[] _visibleWeights;
    private Color32[] _fogPixels;
    private RectInt _previousVisibleBounds;
    private bool _hasPreviousVisibleBounds;
    private readonly List<Vector3> _lastVisionPositions = new();
    private float _lastSightRadius;

    public Texture2D Texture { get; }
    public Sprite Sprite { get; }
    public Vector2Int TerrainSize { get; }
    public WorldLogicData LogicData { get; }

    public Texture2D FogTexture { get; private set; }
    public Sprite FogSprite { get; private set; }
    public bool HasFogOfWar { get; private set; }
    public float SightRadius { get; private set; } = WorldMap.DefaultSightRadius;
    public int FogExplorationVersion { get; private set; }
    public int FogVisionVersion { get; private set; }

    public WorldMapData(Texture2D texture, Sprite sprite, Vector2Int terrainSize)
        : this(texture, sprite, terrainSize, null) { }

    public WorldMapData(Texture2D texture, Sprite sprite, Vector2Int terrainSize, WorldLogicData logicData)
    {
        Texture = texture;
        Sprite = sprite;
        TerrainSize = terrainSize;
        LogicData = logicData;
    }

    public Vector2 NormalizeWorldPosition(Vector3 worldPosition)
    {
        return new Vector2(Mathf.Clamp01(worldPosition.x / TerrainSize.x), Mathf.Clamp01(worldPosition.z / TerrainSize.y));
    }

    /// <summary>2단계 전장의 안개(미탐사 불투명 / 탐사 완료 반투명 / 현재 시야 투명) 텍스처와 버퍼를 초기화한다.</summary>
    public void InitializeFogOfWar(float sightRadius = WorldMap.DefaultSightRadius)
    {
        SightRadius = Mathf.Max(4f, sightRadius);
        if (HasFogOfWar) return;

        HasFogOfWar = true;
        _exploredWeights = new byte[FogCellCount];
        _visibleWeights = new byte[FogCellCount];
        _fogPixels = new Color32[FogCellCount];
        _hasPreviousVisibleBounds = false;
        _lastVisionPositions.Clear();

        for (int i = 0; i < FogCellCount; i++)
        {
            _fogPixels[i] = BaseFogColor;
        }

        FogTexture = new Texture2D(FogResolution, FogResolution, TextureFormat.RGBA32, false)
        {
            name = "Runtime_WorldMap_Fog",
            // 세 단계의 내부 밝기는 유지하고 경계 텍셀 사이만 보간해 확대 시 계단을 완화한다.
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        FogTexture.SetPixels32(_fogPixels);
        FogTexture.Apply(false, false);

        FogSprite = Sprite.Create(FogTexture, new Rect(0f, 0f, FogResolution, FogResolution), new Vector2(0.5f, 0.5f));
        FogSprite.name = "Runtime_WorldMap_Fog_Sprite";
        FogExplorationVersion = 1;
        FogVisionVersion = 1;
    }

    /// <summary>특정 월드 위치 주변을 영구 탐사(Explored) 영역으로 개방한다.</summary>
    public bool RevealArea(Vector3 worldPosition, float radius)
    {
        if (!HasFogOfWar || _exploredWeights == null) return false;

        RectInt bounds = StampCircle(_exploredWeights, worldPosition, Mathf.Max(1f, radius), out bool newlyExplored, out bool anyWeightChanged);
        if (!anyWeightChanged) return false;

        ApplyFogAlphaInBounds(bounds);
        FogTexture.SetPixels32(_fogPixels);
        FogTexture.Apply(false, false);

        if (newlyExplored) FogExplorationVersion++;
        FogVisionVersion++;
        return true;
    }

    /// <summary>파티원들의 월드 위치를 기반으로 실시간 시야(Visible)와 영구 탐사(Explored) 영역을 갱신한다.</summary>
    public bool UpdatePartyVision(IReadOnlyList<Vector3> worldPositions, float sightRadius, out bool explorationChanged)
    {
        explorationChanged = false;
        if (!HasFogOfWar || _exploredWeights == null || worldPositions == null) return false;

        sightRadius = Mathf.Max(4f, sightRadius);
        SightRadius = sightRadius;

        if (!HasVisionInputChanged(worldPositions, sightRadius))
        {
            return false;
        }

        RecordVisionInput(worldPositions, sightRadius);

        RectInt dirtyBounds = default;
        bool hasDirtyBounds = false;

        if (_hasPreviousVisibleBounds)
        {
            ClearVisibleInBounds(_previousVisibleBounds);
            dirtyBounds = _previousVisibleBounds;
            hasDirtyBounds = true;
        }

        RectInt currentVisibleBounds = default;
        bool hasCurrentVisibleBounds = false;

        for (int i = 0; i < worldPositions.Count; i++)
        {
            Vector3 pos = worldPositions[i];
            RectInt stamped = StampVisionAndExploration(pos, sightRadius, out bool newlyExplored);
            if (newlyExplored) explorationChanged = true;

            if (stamped.width > 0 && stamped.height > 0)
            {
                currentVisibleBounds = hasCurrentVisibleBounds ? UnionBounds(currentVisibleBounds, stamped) : stamped;
                hasCurrentVisibleBounds = true;
            }
        }

        if (hasCurrentVisibleBounds)
        {
            dirtyBounds = hasDirtyBounds ? UnionBounds(dirtyBounds, currentVisibleBounds) : currentVisibleBounds;
            hasDirtyBounds = true;
        }

        _previousVisibleBounds = currentVisibleBounds;
        _hasPreviousVisibleBounds = hasCurrentVisibleBounds;

        if (!hasDirtyBounds) return false;

        bool pixelsChanged = ApplyFogAlphaInBounds(dirtyBounds);
        if (pixelsChanged && FogTexture != null)
        {
            FogTexture.SetPixels32(_fogPixels);
            FogTexture.Apply(false, false);
        }

        if (explorationChanged) FogExplorationVersion++;
        if (pixelsChanged || explorationChanged) FogVisionVersion++;
        return pixelsChanged || explorationChanged;
    }

    /// <summary>해당 월드 좌표가 파티에 의해 탐사 완료되었는지 반환한다. 안개가 비활성화된 경우 항상 true다.</summary>
    public bool IsPositionExplored(Vector3 worldPosition)
    {
        return IsNormalizedPositionExplored(NormalizeWorldPosition(worldPosition));
    }

    /// <summary>해당 정규화 좌표(0~1)가 파티에 의해 탐사 완료되었는지 반환한다. 안개가 비활성화된 경우 항상 true다.</summary>
    public bool IsNormalizedPositionExplored(Vector2 normalizedPosition)
    {
        if (!HasFogOfWar || _exploredWeights == null) return true;

        int cx = Mathf.Clamp(Mathf.FloorToInt(normalizedPosition.x * FogResolution), 0, FogResolution - 1);
        int cy = Mathf.Clamp(Mathf.FloorToInt(normalizedPosition.y * FogResolution), 0, FogResolution - 1);
        return _exploredWeights[(cy * FogResolution) + cx] >= ExploredThreshold;
    }

    /// <summary>탐사된 마커의 밝기를 반환한다. 시야 밖에서는 지도와 같은 약 50% 밝기를 적용한다.</summary>
    public float GetMarkerBrightness(Vector2 normalizedPosition)
    {
        if (!HasFogOfWar || _exploredWeights == null || _visibleWeights == null) return 1f;

        int cx = Mathf.Clamp(Mathf.FloorToInt(normalizedPosition.x * FogResolution), 0, FogResolution - 1);
        int cy = Mathf.Clamp(Mathf.FloorToInt(normalizedPosition.y * FogResolution), 0, FogResolution - 1);
        int idx = (cy * FogResolution) + cx;
        return _visibleWeights[idx] != 0 ? 1f : 1f - ExploredFogAlpha / 255f;
    }

    /// <summary>영구 탐사 영역을 8,192바이트(256x256) 비트마스크 Base64 문자열로 직렬화한다.</summary>
    public string CaptureExploredFogBase64()
    {
        if (!HasFogOfWar || _exploredWeights == null) return null;

        byte[] mask = new byte[FogMaskByteLength];
        int scale = FogResolution / FogSaveResolution;
        for (int sy = 0; sy < FogSaveResolution; sy++)
        {
            int baseY = sy * scale;
            int saveRow = sy * FogSaveResolution;
            for (int sx = 0; sx < FogSaveResolution; sx++)
            {
                int baseX = sx * scale;
                byte maxWeight = 0;
                for (int dy = 0; dy < scale; dy++)
                {
                    int row = (baseY + dy) * FogResolution;
                    for (int dx = 0; dx < scale; dx++)
                    {
                        byte w = _exploredWeights[row + baseX + dx];
                        if (w > maxWeight) maxWeight = w;
                    }
                }

                if (maxWeight >= ExploredThreshold)
                {
                    int bitIndex = saveRow + sx;
                    mask[bitIndex >> 3] |= (byte)(1 << (bitIndex & 7));
                }
            }
        }
        return Convert.ToBase64String(mask);
    }

    /// <summary>저장된 Base64 비트마스크로부터 영구 탐사 영역을 복원한다.</summary>
    public bool RestoreExploredFogBase64(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return false;

        byte[] mask = DecodeAndValidateFogMask(base64);
        if (!HasFogOfWar) InitializeFogOfWar(SightRadius);

        bool anyNew = false;
        int scale = FogResolution / FogSaveResolution;
        for (int sy = 0; sy < FogSaveResolution; sy++)
        {
            int baseY = sy * scale;
            int saveRow = sy * FogSaveResolution;
            for (int sx = 0; sx < FogSaveResolution; sx++)
            {
                int bitIndex = saveRow + sx;
                bool explored = (mask[bitIndex >> 3] & (1 << (bitIndex & 7))) != 0;
                if (!explored) continue;

                int baseX = sx * scale;
                for (int dy = 0; dy < scale; dy++)
                {
                    int row = (baseY + dy) * FogResolution;
                    for (int dx = 0; dx < scale; dx++)
                    {
                        int idx = row + baseX + dx;
                        if (_exploredWeights[idx] < 255)
                        {
                            if (_exploredWeights[idx] < ExploredThreshold) anyNew = true;
                            _exploredWeights[idx] = 255;
                        }
                    }
                }
            }
        }

        ApplyFogAlphaInBounds(new RectInt(0, 0, FogResolution, FogResolution));

        if (FogTexture != null)
        {
            FogTexture.SetPixels32(_fogPixels);
            FogTexture.Apply(false, false);
        }

        if (anyNew) FogExplorationVersion++;
        FogVisionVersion++;
        return true;
    }

    /// <summary>세이브 데이터의 전장의 안개 Base64 문자열이 유효한지 검증한다.</summary>
    public static void ValidateExploredFogBase64(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return;
        DecodeAndValidateFogMask(base64);
    }

    private static byte[] DecodeAndValidateFogMask(string base64)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("전장의 안개 저장 데이터 형식이 올바르지 않습니다.", ex);
        }

        if (bytes == null || bytes.Length != FogMaskByteLength)
        {
            throw new InvalidOperationException("전장의 안개 저장 데이터 크기가 올바르지 않습니다.");
        }

        return bytes;
    }

    private bool HasVisionInputChanged(IReadOnlyList<Vector3> worldPositions, float sightRadius)
    {
        if (Mathf.Abs(sightRadius - _lastSightRadius) > 0.05f) return true;
        if (worldPositions.Count != _lastVisionPositions.Count) return true;

        for (int i = 0; i < worldPositions.Count; i++)
        {
            Vector3 a = worldPositions[i];
            Vector3 b = _lastVisionPositions[i];
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            if ((dx * dx) + (dz * dz) > PositionChangeThresholdSqr) return true;
        }
        return false;
    }

    private void RecordVisionInput(IReadOnlyList<Vector3> worldPositions, float sightRadius)
    {
        _lastSightRadius = sightRadius;
        _lastVisionPositions.Clear();
        for (int i = 0; i < worldPositions.Count; i++)
        {
            _lastVisionPositions.Add(worldPositions[i]);
        }
    }

    private void ClearVisibleInBounds(RectInt bounds)
    {
        int xMax = Mathf.Min(FogResolution, bounds.xMax);
        int yMax = Mathf.Min(FogResolution, bounds.yMax);
        for (int y = Mathf.Max(0, bounds.yMin); y < yMax; y++)
        {
            int rowOffset = y * FogResolution;
            for (int x = Mathf.Max(0, bounds.xMin); x < xMax; x++)
            {
                _visibleWeights[rowOffset + x] = 0;
            }
        }
    }

    private RectInt StampCircle(byte[] targetWeights, Vector3 worldPosition, float radius, out bool newlyExplored, out bool anyChanged)
    {
        newlyExplored = false;
        anyChanged = false;

        float normX = Mathf.Clamp01(worldPosition.x / Mathf.Max(1, TerrainSize.x));
        float normY = Mathf.Clamp01(worldPosition.z / Mathf.Max(1, TerrainSize.y));
        float centerX = normX * FogResolution;
        float centerY = normY * FogResolution;
        float radiusX = Mathf.Max(1.5f, (radius / Mathf.Max(1, TerrainSize.x)) * FogResolution);
        float radiusY = Mathf.Max(1.5f, (radius / Mathf.Max(1, TerrainSize.y)) * FogResolution);

        int minX = Mathf.Clamp(Mathf.FloorToInt(centerX - radiusX) - 1, 0, FogResolution - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(centerX + radiusX) + 1, 0, FogResolution - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(centerY - radiusY) - 1, 0, FogResolution - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(centerY + radiusY) + 1, 0, FogResolution - 1);

        float invRadiusX = 1f / radiusX;
        float invRadiusY = 1f / radiusY;

        for (int y = minY; y <= maxY; y++)
        {
            float dy = (y + 0.5f - centerY) * invRadiusY;
            float dy2 = dy * dy;
            int rowOffset = y * FogResolution;

            for (int x = minX; x <= maxX; x++)
            {
                float dx = (x + 0.5f - centerX) * invRadiusX;
                float distSq = (dx * dx) + dy2;
                if (distSq > 1f) continue;

                int idx = rowOffset + x;
                if (targetWeights[idx] < 255)
                {
                    if (targetWeights[idx] < ExploredThreshold)
                    {
                        newlyExplored = true;
                    }
                    targetWeights[idx] = 255;
                    anyChanged = true;
                }
            }
        }

        return new RectInt(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1);
    }

    private RectInt StampVisionAndExploration(Vector3 worldPosition, float sightRadius, out bool newlyExplored)
    {
        newlyExplored = false;

        float normX = Mathf.Clamp01(worldPosition.x / Mathf.Max(1, TerrainSize.x));
        float normY = Mathf.Clamp01(worldPosition.z / Mathf.Max(1, TerrainSize.y));
        float centerX = normX * FogResolution;
        float centerY = normY * FogResolution;

        // 마스크에는 세 단계만 기록하고, 경계 보간은 텍스처 필터가 처리한다.
        float radiusX = Mathf.Max(1.5f, (sightRadius / Mathf.Max(1, TerrainSize.x)) * FogResolution);
        float radiusY = Mathf.Max(1.5f, (sightRadius / Mathf.Max(1, TerrainSize.y)) * FogResolution);

        int minX = Mathf.Clamp(Mathf.FloorToInt(centerX - radiusX) - 1, 0, FogResolution - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(centerX + radiusX) + 1, 0, FogResolution - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(centerY - radiusY) - 1, 0, FogResolution - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(centerY + radiusY) + 1, 0, FogResolution - 1);

        float invRadiusX = 1f / radiusX;
        float invRadiusY = 1f / radiusY;

        for (int y = minY; y <= maxY; y++)
        {
            float dy = (y + 0.5f - centerY) * invRadiusY;
            float dy2 = dy * dy;
            int rowOffset = y * FogResolution;

            for (int x = minX; x <= maxX; x++)
            {
                float dx = (x + 0.5f - centerX) * invRadiusX;
                float distSq = (dx * dx) + dy2;
                if (distSq > 1f) continue;

                int idx = rowOffset + x;
                if (_exploredWeights[idx] < 255)
                {
                    if (_exploredWeights[idx] < ExploredThreshold)
                    {
                        newlyExplored = true;
                    }
                    _exploredWeights[idx] = 255;
                }

                _visibleWeights[idx] = 255;
            }
        }

        return new RectInt(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1);
    }

    private bool ApplyFogAlphaInBounds(RectInt bounds)
    {
        bool changed = false;
        int xMin = Mathf.Clamp(bounds.xMin, 0, FogResolution - 1);
        int yMin = Mathf.Clamp(bounds.yMin, 0, FogResolution - 1);
        int xMax = Mathf.Clamp(bounds.xMax, 0, FogResolution);
        int yMax = Mathf.Clamp(bounds.yMax, 0, FogResolution);

        for (int y = yMin; y < yMax; y++)
        {
            int rowOffset = y * FogResolution;
            for (int x = xMin; x < xMax; x++)
            {
                int idx = rowOffset + x;
                byte finalAlpha = _visibleWeights[idx] != 0
                    ? VisibleFogAlpha
                    : (_exploredWeights[idx] >= ExploredThreshold ? ExploredFogAlpha : UnexploredFogAlpha);

                if (_fogPixels[idx].a != finalAlpha)
                {
                    Color32 pixel = _fogPixels[idx];
                    pixel.a = finalAlpha;
                    _fogPixels[idx] = pixel;
                    changed = true;
                }
            }
        }

        return changed;
    }

    private static RectInt UnionBounds(RectInt a, RectInt b)
    {
        int xMin = Mathf.Min(a.xMin, b.xMin);
        int yMin = Mathf.Min(a.yMin, b.yMin);
        int xMax = Mathf.Max(a.xMax, b.xMax);
        int yMax = Mathf.Max(a.yMax, b.yMax);
        return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    public void Dispose()
    {
        DestroyRuntimeAsset(FogSprite);
        DestroyRuntimeAsset(FogTexture);
        DestroyRuntimeAsset(Sprite);
        DestroyRuntimeAsset(Texture);
    }

    private static void DestroyRuntimeAsset(UnityEngine.Object asset)
    {
        if (asset == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(asset);
        else UnityEngine.Object.DestroyImmediate(asset);
    }
}
