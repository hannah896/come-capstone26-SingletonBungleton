using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

public class TerrainDetailLoader
{
    public async UniTask<TerrainDetailPalette> LoadDetailsAsync(WorldGraphData graphData, CancellationToken ct)
    {
        TerrainDetailPalette palette = new TerrainDetailPalette();
        HashSet<string> uniqueDetailKeys = new HashSet<string>();

        foreach (var node in graphData.Nodes)
        {
            if (node.BiomeData?.DetailKeys == null) continue;

            foreach (var key in node.BiomeData.DetailKeys)
            {
                uniqueDetailKeys.Add(key);
            }
        }

        List<string> collectedKeys = uniqueDetailKeys.ToList();
        List<string> validKeys = new List<string>();
        HashSet<string> normalizedValidKeys = new HashSet<string>();

        int invalidKeyCount = 0;
        int normalizedDuplicateCount = 0;

        foreach (var key in collectedKeys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                invalidKeyCount++;
                continue;
            }

            string normalizedKey = key.Trim();
            if (!normalizedValidKeys.Add(normalizedKey))
            {
                normalizedDuplicateCount++;
                continue;
            }

            validKeys.Add(normalizedKey);
        }

        validKeys.Sort(System.StringComparer.Ordinal);

        if (validKeys.Count == 0)
        {
            palette.LoadedKeys = new List<string>();
            palette.Prototypes = new DetailPrototype[0];
            Debug.LogWarning($"🌿 [DetailLoader] 유효 키 없음 (무효:{invalidKeyCount})");
            return palette;
        }

        List<DetailPrototype> prototypeList = new List<DetailPrototype>(validKeys.Count);
        List<string> loadedKeys = new List<string>(validKeys.Count);

        int nullCount = 0;
        int meshCount = 0;
        int textureCount = 0;

        for (int i = 0; i < validKeys.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            string key = validKeys[i];

            // 1) Mesh Detail 시도 (GameObject)
            GameObject prefab = await Extensions.LoadAssetAsync<GameObject>(key, AssetCacheType.NonRequired, ct);
            if (prefab == null)
            {
                nullCount++;
                Debug.LogWarning($"🌿 [DetailLoader] 프리팹 로드 실패: key='{key}'");
                continue;
            }
            MeshRenderer renderer = prefab.GetComponentInChildren<MeshRenderer>();
            MeshFilter filter = prefab.GetComponentInChildren<MeshFilter>();
            if (renderer == null || filter == null || renderer.sharedMaterial == null)
            {
                nullCount++;
                Debug.LogWarning($"🌿 [DetailLoader] 디테일 프리팹 형식 불일치(렌더러/메시/머티리얼 누락): key='{key}'");
                continue;
            }
            DetailPrototype prototype = new DetailPrototype
            {
                prototype = prefab,
                usePrototypeMesh = true,
                renderMode = DetailRenderMode.VertexLit,
                minWidth = 1.0f,
                maxWidth = 1.0f,
                minHeight = 0.5f,
                maxHeight = 0.5f,
                healthyColor = Color.white,
                dryColor = Color.white,
                useInstancing = true
            };

            prototypeList.Add(prototype);
            palette.IndexMap[key] = prototypeList.Count - 1;
            loadedKeys.Add(key);
            meshCount++;

            Debug.LogWarning($"🌿 [DetailLoader] 로드 실패 (GameObject/Texture2D 모두 null): key='{key}'");
        }

        palette.Prototypes = prototypeList.ToArray();
        palette.LoadedKeys = loadedKeys;

        Debug.Log($"🌿 [DetailLoader] 완료 수집:{collectedKeys.Count} 유효:{validKeys.Count} 성공:{palette.Prototypes.Length} mesh:{meshCount} tex:{textureCount} 실패:{nullCount} 무효:{invalidKeyCount} 중복:{normalizedDuplicateCount}");

        return palette;
    }
}