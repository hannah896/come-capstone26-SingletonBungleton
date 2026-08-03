#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public class ItemIconGenerator : EditorWindow
{
    [MenuItem("Tools/아이템 아이콘 생성기")]
    public static void Open() => GetWindow<ItemIconGenerator>("아이콘 생성기");

    private int iconSize = 128;
    private Color bgColor = new Color(0, 0, 0, 0); // 투명 배경
    private Vector3 camOffset = new Vector3(0, 0.5f, -2f);
    private Vector3 lightDir = new Vector3(-30, 45, 0);

    // 씬에 있는 바닥/다른 오브젝트가 같이 찍히지 않도록, 미리보기 전용 레이어만 렌더링한다.
    private const int PreviewLayer = 6;

    private void OnGUI()
    {
        GUILayout.Label("아이템 아이콘 자동 생성", EditorStyles.boldLabel);
        iconSize = EditorGUILayout.IntField("아이콘 크기 (px)", iconSize);
        bgColor = EditorGUILayout.ColorField("배경색 (투명 = alpha 0)", bgColor);
        camOffset = EditorGUILayout.Vector3Field("카메라 오프셋", camOffset);

        EditorGUILayout.Space();

        if (GUILayout.Button("선택된 프리팹으로 아이콘 생성", GUILayout.Height(30)))
            GenerateFromSelection();

        if (GUILayout.Button("모든 ItemDataSO 아이콘 일괄 생성", GUILayout.Height(30)))
            GenerateAll();
    }

    private void GenerateFromSelection()
    {
        foreach (var obj in Selection.objects)
        {
            if (obj is GameObject prefab)
                GenerateIcon(prefab, "Assets/Sprites/Icons");
        }
        AssetDatabase.Refresh();
    }

    private void GenerateAll()
    {
        string[] guids = AssetDatabase.FindAssets("t:ItemDataSO");
        int count = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<ItemDataSO>(path);
            if (so == null || so.prefab == null) continue;
            if (so.icon != null) continue; // 이미 있으면 스킵

            Sprite icon = GenerateIcon(so.prefab, "Assets/Sprites/Icons");
            if (icon != null)
            {
                so.icon = icon;
                EditorUtility.SetDirty(so);
                count++;
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[아이콘 생성] {count}개 완료");
    }

    private Sprite GenerateIcon(GameObject prefab, string saveFolder)
    {
        // 저장 폴더 생성
        if (!Directory.Exists(saveFolder))
            Directory.CreateDirectory(saveFolder);

        // RenderTexture 세팅
        var rt = new RenderTexture(iconSize, iconSize, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;

        // 임시 씬에 프리팹 스폰
        var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        obj.hideFlags = HideFlags.HideAndDontSave;
        SetLayerRecursively(obj, PreviewLayer);

        // 바운드 계산 → 카메라 자동 맞춤
        Bounds bounds = GetBounds(obj);
        Vector3 center = bounds.center;

        // 카메라 생성 — PreviewLayer만 비춰서 씬의 바닥/다른 오브젝트가 같이 찍히는 것을 막는다.
        var camGO = new GameObject("_IconCamera");
        var cam = camGO.AddComponent<Camera>();
        cam.backgroundColor = bgColor;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.cullingMask = 1 << PreviewLayer;
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(bounds.extents.magnitude, 0.01f) * 1.2f;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = Mathf.Max(bounds.extents.magnitude * 10f, 100f);
        cam.targetTexture = rt;
        cam.transform.position = center + camOffset.normalized * bounds.extents.magnitude * 2f;
        cam.transform.LookAt(center);

        // 조명
        var lightGO = new GameObject("_IconLight");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.eulerAngles = lightDir;
        light.intensity = 1.2f;

        // 렌더
        cam.Render();

        // Texture2D로 저장
        RenderTexture.active = rt;
        var tex = new Texture2D(iconSize, iconSize, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, iconSize, iconSize), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        // PNG 저장
        string fileName = $"{saveFolder}/{prefab.name}_icon.png";
        File.WriteAllBytes(fileName, tex.EncodeToPNG());

        // 정리
        DestroyImmediate(obj);
        DestroyImmediate(camGO);
        DestroyImmediate(lightGO);
        DestroyImmediate(rt);
        DestroyImmediate(tex);

        // Import 설정 → Sprite로 변경
        AssetDatabase.ImportAsset(fileName);
        var importer = (TextureImporter)AssetImporter.GetAtPath(fileName);
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(fileName);
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private static Bounds GetBounds(GameObject obj)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(obj.transform.position, Vector3.one);

        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }
}
#endif