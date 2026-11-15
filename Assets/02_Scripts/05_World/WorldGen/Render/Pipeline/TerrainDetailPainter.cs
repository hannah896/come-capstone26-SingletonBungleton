using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

public class TerrainDetailPainter
{
    private const int DETAIL_DENSITY_PER_CELL = 100;

    private sealed class Workspace
    {
        public List<int>[] NodeDetailIndices = Array.Empty<List<int>>();
        public readonly List<int> UsedDetailIndices = new();
        public readonly HashSet<int> UsedDetailSet = new();
        public readonly HashSet<int> UniquePerNodeSet = new();
        public int[][,] LayerBuffers = Array.Empty<int[,]>();
        public readonly Stopwatch YieldStopwatch = new();

        public void Prepare(int nodeCount, int prototypeCount)
        {
            UsedDetailIndices.Clear();
            UsedDetailSet.Clear();
            UniquePerNodeSet.Clear();

            if (NodeDetailIndices.Length < nodeCount)
            {
                int prevLen = NodeDetailIndices.Length;
                Array.Resize(ref NodeDetailIndices, nodeCount);
                for (int i = prevLen; i < nodeCount; i++)
                {
                    NodeDetailIndices[i] = new List<int>(4);
                }
            }

            for (int i = 0; i < nodeCount; i++)
            {
                if (NodeDetailIndices[i] == null)
                    NodeDetailIndices[i] = new List<int>(4);
                else
                    NodeDetailIndices[i].Clear();
            }

            if (LayerBuffers.Length < prototypeCount)
            {
                Array.Resize(ref LayerBuffers, prototypeCount);
            }
        }

        public int[,] GetOrClearLayerBuffer(int detailIndex, int detailHeight, int detailWidth)
        {
            int[,] buffer = LayerBuffers[detailIndex];
            if (buffer == null ||
                buffer.GetLength(0) != detailHeight ||
                buffer.GetLength(1) != detailWidth)
            {
                buffer = new int[detailHeight, detailWidth];
                LayerBuffers[detailIndex] = buffer;
            }
            else
            {
                Array.Clear(buffer, 0, buffer.Length);
            }

            return buffer;
        }
    }

    private readonly Stack<Workspace> _workspacePool = new();

    private Workspace RentWorkspace(int nodeCount, int prototypeCount)
    {
        Workspace ws = _workspacePool.Count > 0 ? _workspacePool.Pop() : new Workspace();
        ws.Prepare(nodeCount, prototypeCount);
        return ws;
    }

    private void ReturnWorkspace(Workspace ws)
    {
        if (ws == null) return;
        ws.UsedDetailIndices.Clear();
        ws.UsedDetailSet.Clear();
        ws.UniquePerNodeSet.Clear();
        _workspacePool.Push(ws);
    }

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

        int prototypeCount = palette.Prototypes.Length;
        Workspace ws = RentWorkspace(nodeCount, prototypeCount);

        try
        {
            for (int i = 0; i < nodeCount; i++)
            {
                var node = graphData.Nodes[i];
                if (node?.BiomeData?.DetailKeys == null || node.BiomeData.DetailKeys.Count == 0) continue;

                ws.UniquePerNodeSet.Clear();
                List<int> nodeDetails = ws.NodeDetailIndices[i];

                foreach (string rawKey in node.BiomeData.DetailKeys)
                {
                    if (string.IsNullOrWhiteSpace(rawKey)) continue;
                    string key = rawKey.Trim();

                    if (!palette.IndexMap.TryGetValue(key, out int detailIndex)) continue;
                    if (detailIndex < 0 || detailIndex >= prototypeCount) continue;

                    if (ws.UniquePerNodeSet.Add(detailIndex))
                    {
                        nodeDetails.Add(detailIndex);
                    }

                    if (ws.UsedDetailSet.Add(detailIndex))
                    {
                        ws.UsedDetailIndices.Add(detailIndex);
                    }
                }
            }

            if (ws.UsedDetailIndices.Count == 0)
            {
                Debug.LogWarning("🌿 [TerrainDetailPainter] usedDetailIndices=0");
                return;
            }

            for (int i = 0; i < ws.UsedDetailIndices.Count; i++)
            {
                int detailIndex = ws.UsedDetailIndices[i];
                ws.GetOrClearLayerBuffer(detailIndex, detailHeight, detailWidth);
            }

            int xDenom = Mathf.Max(1, detailWidth - 1);
            int yDenom = Mathf.Max(1, detailHeight - 1);

            Stopwatch yieldStopwatch = ws.YieldStopwatch;
            yieldStopwatch.Restart();

            for (int y = 0; y < detailHeight; y++)
            {
                int localY = Mathf.Clamp(Mathf.RoundToInt((float)y / yDenom * (chunkSize - 1)), 0, chunkSize - 1);

                for (int x = 0; x < detailWidth; x++)
                {
                    int localX = Mathf.Clamp(Mathf.RoundToInt((float)x / xDenom * (chunkSize - 1)), 0, chunkSize - 1);

                    int nodeIndex = chunk.TerritoryMap[localX, localY];
                    if (nodeIndex < 0 || nodeIndex >= nodeCount) continue;

                    List<int> detailsOfNode = ws.NodeDetailIndices[nodeIndex];
                    if (detailsOfNode == null || detailsOfNode.Count == 0) continue;

                    for (int i = 0; i < detailsOfNode.Count; i++)
                    {
                        int detailIndex = detailsOfNode[i];
                        ws.LayerBuffers[detailIndex][y, x] = DETAIL_DENSITY_PER_CELL;
                    }
                }

                if (yieldStopwatch.ElapsedMilliseconds > 5)
                {
                    ct.ThrowIfCancellationRequested();
                    await UniTask.Yield(ct);
                    yieldStopwatch.Restart();
                }
            }

            for (int i = 0; i < ws.UsedDetailIndices.Count; i++)
            {
                int detailIndex = ws.UsedDetailIndices[i];
                terrainData.SetDetailLayer(0, 0, detailIndex, ws.LayerBuffers[detailIndex]);
            }
        }
        finally
        {
            ReturnWorkspace(ws);
        }
    }
}