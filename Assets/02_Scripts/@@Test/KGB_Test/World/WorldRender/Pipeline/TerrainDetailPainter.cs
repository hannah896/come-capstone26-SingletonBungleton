using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

public class TerrainDetailPainter
{
    private const int DETAIL_DENSITY_PER_CELL = 100; 

    public async UniTask PaintChunkDetailsAsync(
        TerrainData terrainData,
        ChunkData chunk,
        WorldGraphData graphData,
        TerrainDetailPalette palette,
        CancellationToken ct)
    {
        if (terrainData == null || chunk == null || graphData == null || palette == null) return;
        if (palette.Prototypes == null || palette.Prototypes.Length == 0)
        {
            Debug.LogWarning("🌿 [TerrainDetailPainter] prototypes=0");
            return;
        }

        terrainData.detailPrototypes = palette.Prototypes;
        terrainData.RefreshPrototypes();

        int detailWidth = terrainData.detailWidth;
        int detailHeight = terrainData.detailHeight;
        if (detailWidth <= 0 || detailHeight <= 0) return;

        int chunkSize = chunk.TerritoryMap.GetLength(0);
        int nodeCount = graphData.Nodes != null ? graphData.Nodes.Count : 0;
        if (nodeCount == 0) return;

        List<int>[] nodeDetailIndices = new List<int>[nodeCount];
        HashSet<int> usedDetailIndices = new HashSet<int>();

        for (int i = 0; i < nodeCount; i++)
        {
            var node = graphData.Nodes[i];
            if (node?.BiomeData?.DetailKeys == null || node.BiomeData.DetailKeys.Count == 0) continue;

            HashSet<int> uniquePerNode = new HashSet<int>();
            foreach (string rawKey in node.BiomeData.DetailKeys)
            {
                if (string.IsNullOrWhiteSpace(rawKey)) continue;
                string key = rawKey.Trim();

                if (!palette.IndexMap.TryGetValue(key, out int detailIndex)) continue;

                uniquePerNode.Add(detailIndex);
                usedDetailIndices.Add(detailIndex);
            }

            if (uniquePerNode.Count > 0)
            {
                nodeDetailIndices[i] = new List<int>(uniquePerNode);
            }
        }

        if (usedDetailIndices.Count == 0)
        {
            Debug.LogWarning("🌿 [TerrainDetailPainter] usedDetailIndices=0");
            return;
        }

        Dictionary<int, int[,]> detailLayerMaps = new Dictionary<int, int[,]>(usedDetailIndices.Count);
        foreach (int detailIndex in usedDetailIndices)
        {
            detailLayerMaps[detailIndex] = new int[detailHeight, detailWidth];
        }

        int xDenom = Mathf.Max(1, detailWidth - 1);
        int yDenom = Mathf.Max(1, detailHeight - 1);

        Stopwatch yieldStopwatch = Stopwatch.StartNew();
        Stopwatch totalStopwatch = Stopwatch.StartNew();
        int landCellCount = 0;
        int paintedWriteCount = 0;

        for (int y = 0; y < detailHeight; y++)
        {
            for (int x = 0; x < detailWidth; x++)
            {
                int localX = Mathf.Clamp(Mathf.RoundToInt((float)x / xDenom * (chunkSize - 1)), 0, chunkSize - 1);
                int localY = Mathf.Clamp(Mathf.RoundToInt((float)y / yDenom * (chunkSize - 1)), 0, chunkSize - 1);

                int nodeIndex = chunk.TerritoryMap[localX, localY];
                if (nodeIndex < 0 || nodeIndex >= nodeCount) continue;

                landCellCount++;

                List<int> detailsOfNode = nodeDetailIndices[nodeIndex];
                if (detailsOfNode == null || detailsOfNode.Count == 0) continue;

                for (int i = 0; i < detailsOfNode.Count; i++)
                {
                    int detailIndex = detailsOfNode[i];
                    detailLayerMaps[detailIndex][y, x] = DETAIL_DENSITY_PER_CELL;
                    paintedWriteCount++;
                }
            }

            if (yieldStopwatch.ElapsedMilliseconds > 5)
            {
                ct.ThrowIfCancellationRequested();
                await UniTask.Yield(ct);
                yieldStopwatch.Restart();
            }
        }

        long mapFillMs = totalStopwatch.ElapsedMilliseconds;
        Stopwatch applyStopwatch = Stopwatch.StartNew();
        int appliedLayerCount = 0;

        foreach (KeyValuePair<int, int[,]> pair in detailLayerMaps)
        {
            terrainData.SetDetailLayer(0, 0, pair.Key, pair.Value);
            appliedLayerCount++;
        }
        long applyMs = applyStopwatch.ElapsedMilliseconds;

        Debug.Log(
            $"🌿 [TerrainDetailPainter] chunk:{chunk.ChunkCoord} " +
            $"detailRes:{detailWidth}x{detailHeight} " +
            $"land:{landCellCount} write:{paintedWriteCount} layers:{usedDetailIndices.Count} " +
            $"fillMs:{mapFillMs} setLayerMs:{applyMs} applied:{appliedLayerCount}");
    }
}