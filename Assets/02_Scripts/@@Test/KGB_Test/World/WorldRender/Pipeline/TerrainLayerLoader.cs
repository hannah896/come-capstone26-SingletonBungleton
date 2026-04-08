using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

/// <summary>
/// 지형 페인팅에 필요한 TerrainLayer 에셋들을 비동기로 로드하고 캐싱하는 전담 클래스
/// </summary>
public class TerrainLayerLoader
{
    public async UniTask<TerrainLayerPalette> LoadLayersAsync(WorldGraphData graphData, CancellationToken ct)
    {
        Debug.Log("📦 [TerrainLayerLoader] 지형 텍스처 에셋 수집 및 로드 시작...");

        TerrainLayerPalette palette = new TerrainLayerPalette();

        // 1. 필요한 텍스처 Key 수집
        HashSet<string> uniqueTileKeys = new HashSet<string>();
        foreach (var node in graphData.Nodes)
        {
            if (node.RegionData != null)
            {
                if (!string.IsNullOrEmpty(node.BiomeData.TopKey))
                    uniqueTileKeys.Add(node.BiomeData.TopKey);
                if (!string.IsNullOrEmpty(node.BiomeData.AltKey))
                    uniqueTileKeys.Add(node.BiomeData.AltKey);
                if (!string.IsNullOrEmpty(node.BiomeData.CliffKey))
                    uniqueTileKeys.Add(node.BiomeData.CliffKey);
            }
        }

        palette.LoadedKeys = uniqueTileKeys.ToList();

        // 2. 비동기 병렬 로드
        List<UniTask<TerrainLayer>> loadTasks = new List<UniTask<TerrainLayer>>();
        foreach (var key in palette.LoadedKeys)
        {
            loadTasks.Add(Extensions.LoadAssetAsync<TerrainLayer>(key, AssetCacheType.NonRequired, ct));
        }

        palette.Layers = await UniTask.WhenAll(loadTasks);

        // 3. 인덱스 매핑 캐싱
        for (int i = 0; i < palette.Layers.Length; i++)
        {
            palette.IndexMap[palette.LoadedKeys[i]] = i;
        }

        Debug.Log($"📦 [TerrainLayerLoader] 총 {palette.Layers.Length}개의 텍스처 로드 완료!");

        return palette;
    }


}