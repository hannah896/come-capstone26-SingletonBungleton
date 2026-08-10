#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// TerrainLayer의 diffuse texture를 미니맵용 타일 Sprite로 내보낸다.
/// 원본 텍스처의 Read/Write 설정과 무관하게 GPU 복사로 PNG를 생성한다.
/// </summary>
public class MiniMapSpriteGenerator : EditorWindow
{
    private const string DefaultSaveFolder = "Assets/07_Sprites/MiniMapTiles";

    private enum GenerationMode
    {
        DiffuseCopy,
        TerrainRenderCapture
    }

    [SerializeField] private int _spriteSize = 128;
    [SerializeField] private string _saveFolder = DefaultSaveFolder;
    [SerializeField] private GenerationMode _generationMode = GenerationMode.DiffuseCopy;
    [SerializeField] private Color _tint = Color.white;
    [SerializeField] private FilterMode _filterMode = FilterMode.Bilinear;
    [SerializeField] private bool _overwriteExisting;

    [Header("Terrain Render Capture")]
    [SerializeField] private float _previewTerrainSize = 16f;
    [SerializeField] private Color _previewLightColor = Color.white;
    [SerializeField] private float _previewLightIntensity = 1.4f;
    [SerializeField] private Vector3 _previewLightEulerAngles = new Vector3(50f, -30f, 0f);

    private struct TerrainLayerRequest
    {
        public TerrainLayer TerrainLayer;
        public string OutputName;

        public TerrainLayerRequest(TerrainLayer terrainLayer, string outputName)
        {
            TerrainLayer = terrainLayer;
            OutputName = outputName;
        }
    }

    [MenuItem("Tools/World/MiniMap Sprite Generator")]
    public static void Open()
    {
        GetWindow<MiniMapSpriteGenerator>("MiniMap Sprite Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("TerrainLayer MiniMap Sprite Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Terrain Render Capture renders a temporary Terrain in an isolated preview scene, then saves it as a Sprite. " +
            "Diffuse Copy is the previous direct texture-copy mode. Neither mode changes the source TerrainLayer.",
            MessageType.Info);

        _spriteSize = Mathf.ClosestPowerOfTwo(Mathf.Clamp(
            EditorGUILayout.IntField("Sprite Size (px)", _spriteSize), 16, 1024));
        _saveFolder = EditorGUILayout.TextField("Save Folder", _saveFolder);
        _generationMode = (GenerationMode)EditorGUILayout.EnumPopup("Generation Mode", _generationMode);
        _tint = EditorGUILayout.ColorField("Tint", _tint);
        _filterMode = (FilterMode)EditorGUILayout.EnumPopup("Filter Mode", _filterMode);
        _overwriteExisting = EditorGUILayout.Toggle("Overwrite Existing", _overwriteExisting);

        if (_generationMode == GenerationMode.TerrainRenderCapture)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Terrain Render Capture", EditorStyles.boldLabel);
            _previewTerrainSize = Mathf.Max(0.1f,
                EditorGUILayout.FloatField("Preview Terrain Size", _previewTerrainSize));
            _previewLightColor = EditorGUILayout.ColorField("Light Color", _previewLightColor);
            _previewLightIntensity = Mathf.Max(0f,
                EditorGUILayout.FloatField("Light Intensity", _previewLightIntensity));
            _previewLightEulerAngles = EditorGUILayout.Vector3Field(
                "Light Euler Angles",
                _previewLightEulerAngles);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Selected TerrainLayers: {GetSelectedTerrainLayers().Count}");

        using (new EditorGUI.DisabledScope(GetSelectedTerrainLayers().Count == 0))
        {
            if (GUILayout.Button("Generate Selected TerrainLayers", GUILayout.Height(30f)))
            {
                GenerateSelectedTerrainLayers();
            }
        }

        if (GUILayout.Button("Generate All Biome TopKeys", GUILayout.Height(30f)))
        {
            GenerateAllBiomeTopKeys();
        }
    }

    private void GenerateSelectedTerrainLayers()
    {
        List<TerrainLayer> terrainLayers = GetSelectedTerrainLayers();
        var requests = terrainLayers
            .Select(terrainLayer => new TerrainLayerRequest(terrainLayer, terrainLayer.name))
            .ToList();

        GenerateTerrainLayers(requests, "selected TerrainLayers");
    }

    private void GenerateAllBiomeTopKeys()
    {
        if (!TryGetBiomeTopLayerRequests(out List<TerrainLayerRequest> requests, out List<string> unresolvedKeys))
        {
            return;
        }

        if (unresolvedKeys.Count > 0)
        {
            Debug.LogWarning(
                "[MiniMap Sprite Generator] Could not resolve these BiomeData TopKeys to TerrainLayer Addressables: " +
                string.Join(", ", unresolvedKeys));
        }

        GenerateTerrainLayers(requests, "BiomeData TopKeys");
    }

    private void GenerateTerrainLayers(IReadOnlyList<TerrainLayerRequest> requests, string sourceDescription)
    {
        if (!IsValidAssetFolder(_saveFolder))
        {
            EditorUtility.DisplayDialog(
                "Invalid Save Folder",
                "Save Folder must be inside the project's Assets folder.",
                "OK");
            return;
        }

        if (requests.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "No TerrainLayers Found",
                $"No TerrainLayers could be found from {sourceDescription}.",
                "OK");
            return;
        }

        Directory.CreateDirectory(_saveFolder);

        var generatedSprites = new List<Object>();
        int generatedCount = 0;

        try
        {
            for (int i = 0; i < requests.Count; i++)
            {
                TerrainLayerRequest request = requests[i];
                EditorUtility.DisplayProgressBar(
                    "Generating MiniMap Sprites",
                    request.OutputName,
                    (float)i / requests.Count);

                Sprite sprite = GenerateSprite(request.TerrainLayer, request.OutputName);
                if (sprite == null) continue;

                generatedSprites.Add(sprite);
                generatedCount++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();

        if (generatedSprites.Count > 0)
        {
            Selection.objects = generatedSprites.ToArray();
        }

        Debug.Log(
            $"[MiniMap Sprite Generator] Generated or reused {generatedCount} sprite(s) from {sourceDescription} " +
            $"in '{_saveFolder}'.");
    }

    private Sprite GenerateSprite(TerrainLayer terrainLayer, string outputName)
    {
        if (terrainLayer == null)
        {
            return null;
        }

        Texture2D sourceTexture = terrainLayer.diffuseTexture;
        if (sourceTexture == null)
        {
            Debug.LogWarning($"[MiniMap Sprite Generator] '{terrainLayer.name}' has no diffuse texture.", terrainLayer);
            return null;
        }

        string fileName = $"MiniMap_{SanitizeFileName(outputName)}.png";
        string assetPath = $"{_saveFolder}/{fileName}";

        if (File.Exists(assetPath) && !_overwriteExisting)
        {
            Sprite existingSprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (existingSprite != null)
            {
                Debug.Log($"[MiniMap Sprite Generator] Skipped existing sprite: {assetPath}", existingSprite);
                return existingSprite;
            }
        }

        Texture2D outputTexture = _generationMode == GenerationMode.TerrainRenderCapture
            ? CaptureTerrainLayer(terrainLayer)
            : CopyDiffuseTexture(terrainLayer);

        if (outputTexture == null) return null;

        try
        {
            ApplyTint(outputTexture, _tint);
            outputTexture.Apply(false, false);

            File.WriteAllBytes(assetPath, outputTexture.EncodeToPNG());
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ConfigureAsMiniMapSprite(assetPath);

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }
        finally
        {
            DestroyImmediate(outputTexture);
        }
    }

    private Texture2D CopyDiffuseTexture(TerrainLayer terrainLayer)
    {
        //RenderTexture renderTexture = null;
        //RenderTexture previousRenderTexture = RenderTexture.active;

        //try
        //{
        //    renderTexture = CreateRenderTexture();
        //    Graphics.Blit(sourceTexture, renderTexture);
        //    return ReadRenderTexture(renderTexture);
        //}
        //finally
        //{
        //    RenderTexture.active = previousRenderTexture;
        //    if (renderTexture != null)
        //    {
        //        RenderTexture.ReleaseTemporary(renderTexture);
        //    }
        //}
        Texture2D sourceTexture = terrainLayer.diffuseTexture;
        Texture2D normalTexture = terrainLayer.normalMapTexture;

        RenderTexture renderTexture = null;
        RenderTexture previousRenderTexture = RenderTexture.active;
        Material bakeMaterial = null;

        try
        {
            renderTexture = CreateRenderTexture();

            if (normalTexture != null)
            {
                Shader shader = Shader.Find("Hidden/MiniMapNormalBaker"); //Hidden/MiniMapNormalBaker //Raygeas/AZURE Nature/Surface 
                if (shader != null)
                {
                    bakeMaterial = new Material(shader);
                    bakeMaterial.SetTexture("_NormalMap", normalTexture);

                    bakeMaterial.SetFloat("_BumpScale", 1.5f);

                    // 주광 설정 (프리뷰 씬과 동일한 각도 적용)
                    Quaternion lightRot = Quaternion.Euler(_previewLightEulerAngles);
                    Vector3 worldLightDir = -(lightRot * Vector3.forward);

                    Vector3 tangentLightDir = new Vector3(worldLightDir.x, worldLightDir.z, worldLightDir.y).normalized;
                    bakeMaterial.SetVector("_LightDir", tangentLightDir);
                    bakeMaterial.SetColor("_LightColor", _previewLightColor * _previewLightIntensity);

                    // 보조광 설정 (주광의 반대편 180도)
                    Quaternion fillRot = Quaternion.Euler(-_previewLightEulerAngles.x, _previewLightEulerAngles.y + 180f, 0f);
                    Vector3 worldFillDir = -(fillRot * Vector3.forward);
                    Vector3 tangentFillDir = new Vector3(worldFillDir.x, worldFillDir.z, worldFillDir.y).normalized;

                    bakeMaterial.SetVector("_FillLightDir", tangentFillDir);
                    bakeMaterial.SetColor("_FillLightColor", new Color(0.7f, 0.8f, 0.9f) * (_previewLightIntensity * 0.5f));

                    // 노말 연산이 포함된 Material을 사용하여 복사
                    Graphics.Blit(sourceTexture, renderTexture, bakeMaterial);
                }
                else
                {
                    Debug.LogWarning("[MiniMap Sprite Generator] 셰이더를 찾을 수 없어 기본 복사를 수행합니다.");
                    Graphics.Blit(sourceTexture, renderTexture);
                }
            }
            else
            {
                // 노말맵이 없으면 그냥 단순 복사
                Graphics.Blit(sourceTexture, renderTexture);
            }

            return ReadRenderTexture(renderTexture);
        }
        finally
        {
            RenderTexture.active = previousRenderTexture;
            if (renderTexture != null) RenderTexture.ReleaseTemporary(renderTexture);
            if (bakeMaterial != null) DestroyImmediate(bakeMaterial);
        }
    }

    private Texture2D CaptureTerrainLayer(TerrainLayer terrainLayer)
    {
        Scene previewScene = EditorSceneManager.NewPreviewScene();
        GameObject terrainObject = null;
        GameObject lightObject = null;
        GameObject cameraObject = null;
        TerrainData terrainData = null;
        RenderTexture renderTexture = null;
        RenderTexture previousRenderTexture = RenderTexture.active;

        try
        {
            terrainData = new TerrainData
            {
                heightmapResolution = 33,
                alphamapResolution = 32,
                size = new Vector3(_previewTerrainSize, 1f, _previewTerrainSize),
                terrainLayers = new[] { terrainLayer }
            };

            float[,,] alphamaps = new float[32, 32, 1];
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    alphamaps[y, x, 0] = 1f;
                }
            }
            terrainData.SetAlphamaps(0, 0, alphamaps);

            terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = "_MiniMapPreviewTerrain";
            terrainObject.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(terrainObject, previewScene);

            Terrain terrain = terrainObject.GetComponent<Terrain>();
            terrain.drawTreesAndFoliage = false;
            terrain.Flush();

            lightObject = new GameObject("_MiniMapPreviewLight")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            SceneManager.MoveGameObjectToScene(lightObject, previewScene);

            Light previewLight = lightObject.AddComponent<Light>();
            previewLight.type = LightType.Directional;
            previewLight.color = _previewLightColor;
            previewLight.intensity = _previewLightIntensity;
            previewLight.shadows = LightShadows.None;
            previewLight.transform.rotation = Quaternion.Euler(_previewLightEulerAngles);

            cameraObject = new GameObject("_MiniMapPreviewCamera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            SceneManager.MoveGameObjectToScene(cameraObject, previewScene);

            Camera previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.cameraType = CameraType.Game;
            previewCamera.scene = previewScene;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = Color.black;
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = _previewTerrainSize * 0.5f;
            previewCamera.nearClipPlane = 0.01f;
            previewCamera.farClipPlane = 100f;
            previewCamera.allowHDR = false;
            previewCamera.transform.position = new Vector3(
                _previewTerrainSize * 0.5f,
                10f,
                _previewTerrainSize * 0.5f);
            previewCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            renderTexture = CreateRenderTexture(depthBufferBits: 24);
            previewCamera.targetTexture = renderTexture;
            previewCamera.Render();

            return ReadRenderTexture(renderTexture);
        }
        finally
        {
            RenderTexture.active = previousRenderTexture;

            if (renderTexture != null)
            {
                RenderTexture.ReleaseTemporary(renderTexture);
            }

            if (cameraObject != null)
            {
                DestroyImmediate(cameraObject);
            }

            if (lightObject != null)
            {
                DestroyImmediate(lightObject);
            }

            if (terrainObject != null)
            {
                DestroyImmediate(terrainObject);
            }

            if (terrainData != null)
            {
                DestroyImmediate(terrainData);
            }

            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    private RenderTexture CreateRenderTexture(int depthBufferBits = 0)
    {
        RenderTexture renderTexture = RenderTexture.GetTemporary(
            _spriteSize,
            _spriteSize,
            depthBufferBits,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear);
        renderTexture.filterMode = _filterMode;
        renderTexture.wrapMode = TextureWrapMode.Repeat;
        return renderTexture;
    }

    private Texture2D ReadRenderTexture(RenderTexture renderTexture)
    {
        RenderTexture.active = renderTexture;
        var texture = new Texture2D(_spriteSize, _spriteSize, TextureFormat.RGBA32, false)
        {
            filterMode = _filterMode,
            wrapMode = TextureWrapMode.Repeat
        };
        texture.ReadPixels(new Rect(0f, 0f, _spriteSize, _spriteSize), 0, 0);
        return texture;
    }

    private void ConfigureAsMiniMapSprite(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = _spriteSize;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.filterMode = _filterMode;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static List<TerrainLayer> GetSelectedTerrainLayers()
    {
        var terrainLayers = new List<TerrainLayer>();

        foreach (Object selectedObject in Selection.objects)
        {
            if (selectedObject is TerrainLayer terrainLayer)
            {
                terrainLayers.Add(terrainLayer);
            }
        }

        return terrainLayers;
    }

    private static bool TryGetBiomeTopLayerRequests(
        out List<TerrainLayerRequest> requests,
        out List<string> unresolvedKeys)
    {
        requests = new List<TerrainLayerRequest>();
        unresolvedKeys = new List<string>();

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            EditorUtility.DisplayDialog(
                "Addressables Settings Not Found",
                "AddressableAssetSettings could not be found. Create or restore the project Addressables settings first.",
                "OK");
            return false;
        }

        string[] biomeGuids = AssetDatabase.FindAssets("t:BiomeData");
        var topKeys = new HashSet<string>(System.StringComparer.Ordinal);

        foreach (string biomeGuid in biomeGuids)
        {
            string biomePath = AssetDatabase.GUIDToAssetPath(biomeGuid);
            BiomeData biomeData = AssetDatabase.LoadAssetAtPath<BiomeData>(biomePath);

            if (biomeData != null && !string.IsNullOrWhiteSpace(biomeData.TopKey))
            {
                topKeys.Add(biomeData.TopKey);
            }
        }

        foreach (string topKey in topKeys.OrderBy(key => key))
        {
            AddressableAssetEntry entry = FindAddressableEntry(settings, topKey);
            if (entry == null)
            {
                unresolvedKeys.Add(topKey);
                continue;
            }

            TerrainLayer terrainLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(entry.AssetPath);
            if (terrainLayer == null)
            {
                unresolvedKeys.Add($"{topKey} (not a TerrainLayer)");
                continue;
            }

            requests.Add(new TerrainLayerRequest(terrainLayer, topKey));
        }

        return true;
    }

    private static AddressableAssetEntry FindAddressableEntry(
        AddressableAssetSettings settings,
        string address)
    {
        foreach (AddressableAssetGroup group in settings.groups)
        {
            if (group == null) continue;

            AddressableAssetEntry entry = group.entries.FirstOrDefault(candidate => candidate.address == address);
            if (entry != null)
            {
                return entry;
            }
        }

        return null;
    }

    private static bool IsValidAssetFolder(string folder)
    {
        return !string.IsNullOrWhiteSpace(folder)
            && (folder == "Assets" || folder.StartsWith("Assets/"));
    }

    private static void ApplyTint(Texture2D texture, Color tint)
    {
        if (tint == Color.white) return;

        Color32[] pixels = texture.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color pixel = pixels[i];
            pixel *= tint;
            pixels[i] = pixel;
        }

        texture.SetPixels32(pixels);
    }

    private static string SanitizeFileName(string value)
    {
        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalidCharacter, '_');
        }

        return value;
    }
}
#endif
