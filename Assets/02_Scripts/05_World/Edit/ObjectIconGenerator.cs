#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class ObjectIconGenerator : EditorWindow
{
    private const string ResourceNodePrefabFolder = "Assets/03_Prefabs/Objects/ResourceNode";
    private const string SaveFolder = "Assets/Sprites/Icons/ResourceNode";

    private int iconSize = 128;
    private Color backgroundColor = new Color(0f, 0f, 0f, 0f);
    private Vector3 cameraOffset = new Vector3(0f, 0.5f, -2f);
    private Vector3 lightDirection = new Vector3(-30f, 45f, 0f);
    private float framingPadding = 1.1f;

    [MenuItem("Tools/오브젝트 아이콘 생성기")]
    public static void Open()
    {
        GetWindow<ObjectIconGenerator>("오브젝트 아이콘 생성기");
    }

    private void OnGUI()
    {
        GUILayout.Label("3D 오브젝트 아이콘 생성기", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Creates icons from selected prefabs. LOD prefabs are rendered with LOD0 only. **비어있는 씬에서 진행할 것**",
            MessageType.Info);

        iconSize = Mathf.Max(1, EditorGUILayout.IntField("Icon Size (px)", iconSize));
        backgroundColor = EditorGUILayout.ColorField("Background", backgroundColor);
        cameraOffset = EditorGUILayout.Vector3Field("Camera Offset", cameraOffset);
        framingPadding = Mathf.Max(0.1f, EditorGUILayout.FloatField("Framing Padding", framingPadding));

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate Selected Prefabs", GUILayout.Height(30f)))
            GenerateSelectedPrefabs();

        if (GUILayout.Button("Generate All ResourceNode Prefabs", GUILayout.Height(30f)))
            GenerateAllResourceNodePrefabs();
    }

    private void GenerateSelectedPrefabs()
    {
        int generatedCount = 0;

        foreach (Object selectedObject in Selection.objects)
        {
            if (!(selectedObject is GameObject prefab)) continue;

            if (GenerateIcon(prefab) != null)
                generatedCount++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"[Object Icon Generator] Generated {generatedCount} icon(s).");
    }

    private void GenerateAllResourceNodePrefabs()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { ResourceNodePrefabFolder });
        int generatedCount = 0;

        foreach (string prefabGuid in prefabGuids)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab != null && GenerateIcon(prefab) != null)
                generatedCount++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"[Object Icon Generator] Generated {generatedCount} ResourceNode icon(s).");
    }

    private Sprite GenerateIcon(GameObject prefab)
    {
        Directory.CreateDirectory(SaveFolder);

        GameObject instance = null;
        GameObject cameraObject = null;
        GameObject lightObject = null;
        RenderTexture renderTexture = null;
        Texture2D texture = null;
        RenderTexture previousRenderTexture = RenderTexture.active;

        try
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            EnableHighestDetailLodOnly(instance);

            if (!TryGetVisibleBounds(instance, out Bounds bounds))
            {
                Debug.LogWarning($"[Object Icon Generator] No visible renderer found: {prefab.name}");
                return null;
            }

            renderTexture = new RenderTexture(iconSize, iconSize, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };

            cameraObject = new GameObject("_ObjectIconCamera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Camera iconCamera = cameraObject.AddComponent<Camera>();
            iconCamera.backgroundColor = backgroundColor;
            iconCamera.clearFlags = CameraClearFlags.SolidColor;
            iconCamera.orthographic = true;
            iconCamera.orthographicSize = Mathf.Max(0.01f, bounds.extents.magnitude * framingPadding);
            iconCamera.targetTexture = renderTexture;

            Vector3 viewDirection = cameraOffset.sqrMagnitude > 0.0001f
                ? cameraOffset.normalized
                : Vector3.back;
            iconCamera.transform.position = bounds.center + viewDirection * bounds.extents.magnitude * 2f;
            iconCamera.transform.LookAt(bounds.center);

            lightObject = new GameObject("_ObjectIconLight")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Light iconLight = lightObject.AddComponent<Light>();
            iconLight.type = LightType.Directional;
            iconLight.transform.eulerAngles = lightDirection;
            iconLight.intensity = 1.2f;

            iconCamera.Render();

            RenderTexture.active = renderTexture;
            texture = new Texture2D(iconSize, iconSize, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, iconSize, iconSize), 0, 0);
            texture.Apply();

            string iconPath = $"{SaveFolder}/{prefab.name}_icon.png";
            File.WriteAllBytes(iconPath, texture.EncodeToPNG());

            AssetDatabase.ImportAsset(iconPath);
            ConfigureAsSprite(iconPath);
            return AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        }
        finally
        {
            RenderTexture.active = previousRenderTexture;

            if (texture != null) DestroyImmediate(texture);
            if (renderTexture != null) DestroyImmediate(renderTexture);
            if (lightObject != null) DestroyImmediate(lightObject);
            if (cameraObject != null) DestroyImmediate(cameraObject);
            if (instance != null) DestroyImmediate(instance);
        }
    }

    private static void EnableHighestDetailLodOnly(GameObject instance)
    {
        foreach (LODGroup lodGroup in instance.GetComponentsInChildren<LODGroup>(true))
        {
            LOD[] lods = lodGroup.GetLODs();
            if (lods.Length == 0) continue;

            var lodRenderers = new HashSet<Renderer>();
            foreach (LOD lod in lods)
            {
                foreach (Renderer renderer in lod.renderers)
                {
                    if (renderer != null)
                        lodRenderers.Add(renderer);
                }
            }

            foreach (Renderer renderer in lodRenderers)
                renderer.enabled = false;

            foreach (Renderer renderer in lods[0].renderers)
            {
                if (renderer != null)
                    renderer.enabled = true;
            }

            lodGroup.enabled = false;
        }
    }

    private static bool TryGetVisibleBounds(GameObject instance, out Bounds bounds)
    {
        bool hasVisibleRenderer = false;
        bounds = new Bounds(instance.transform.position, Vector3.one);

        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;

            if (!hasVisibleRenderer)
            {
                bounds = renderer.bounds;
                hasVisibleRenderer = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasVisibleRenderer;
    }

    private static void ConfigureAsSprite(string iconPath)
    {
        var importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }
}
#endif
