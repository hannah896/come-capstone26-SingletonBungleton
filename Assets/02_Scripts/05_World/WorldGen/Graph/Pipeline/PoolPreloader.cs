using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class PoolPreloader : IGraphPipelineStage
{
    private WorldDisposeData _disposeData;
    private CancellationToken _ct;

    public void Initialize(WorldSettings settings)
    {
    }

    public async UniTask ExecuteAsync(WorldGenContext ctx, CancellationToken ct)
    {
        _disposeData = ctx.DisposeData;
        _ct = ct;

        if (_disposeData == null || _disposeData.PlacementDatas == null || _disposeData.PlacementDatas.Count == 0) return;
        if (Main.Pool == null) return;

        HashSet<string> uniqueKeys = new HashSet<string>();

        List<PlacementData> placements = _disposeData.PlacementDatas;
        for (int i = 0; i < placements.Count; i++)
        {
            PlacementData placement = placements[i];
            if (placement == null) continue;

            string key = placement.prefabName;
            if (string.IsNullOrEmpty(key)) continue;

            uniqueKeys.Add(key);
        }

        int index = 0;
        foreach (string key in uniqueKeys)
        {
            _ct.ThrowIfCancellationRequested();

            GameObject instance = await Main.Pool.SpawnAsync(key, null, _ct);
            if (instance != null)
            {
                Main.Pool.Despawn(instance);
            }

            index++;
            if ((index % 8) == 0)
            {
                await UniTask.Yield(_ct);
            }
        }
    }
}