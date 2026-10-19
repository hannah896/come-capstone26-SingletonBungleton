using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>
/// 데빌 진짜 찾기 패턴의 연기 이펙트를 만든다. 메뉴: Tools/몬스터/데빌 분신 연기 생성
///  - Assets/08_Materials/Devil/DevilSmoke.mat : 보라색 연기 (WispySmoke 8x8 플립북, 알파 블렌딩)
///  - Assets/03_Prefabs/Monster/IMP/DevilSmokePoof.prefab : DragonVfx(풀링 파티클 재생) + 연기 뭉치 파티클
/// 프리팹은 Addressables(Prefabs_Monster)에 "DevilSmokePoof" 키로 등록한다. 이미 있으면 덮어쓴다.
/// </summary>
public static class DevilCloneBuilder
{
    private const string SmokeTexture = "Assets/00_Externals/PremiumAssets/Effects/UNI VFX/Common/Textures/WispySmoke03b_8x8.png";
    private const string MaterialFolder = "Assets/08_Materials/Devil";
    private const string PrefabPath = "Assets/03_Prefabs/Monster/IMP/DevilSmokePoof.prefab";

    [MenuItem("Tools/몬스터/데빌 분신 연기 생성")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/08_Materials", "Devil");

        var mat = BuildMaterial();

        var go = new GameObject("DevilSmokePoof");
        var vfx = go.AddComponent<DragonVfx>();
        var so = new SerializedObject(vfx);
        so.FindProperty("lifeTime").floatValue = 1.6f;
        so.ApplyModifiedPropertiesWithoutUndo();

        // 몸을 감쌀 만큼 큰 연기 뭉치가 한 번에 터졌다가 퍼지며 옅어진다
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.3f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.gravityModifier = -0.15f;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.7f;

        var sheet = ps.textureSheetAnimation;
        sheet.enabled = true;
        sheet.numTilesX = 8;
        sheet.numTilesY = 8;
        sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 63f);
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.6f));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.7f, 0.4f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings != null)
        {
            var group = settings.FindGroup("Prefabs_Monster") ?? settings.DefaultGroup;
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(PrefabPath), group);
            entry.address = "DevilSmokePoof";
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[DevilCloneBuilder] 생성 완료: {AssetDatabase.GetAssetPath(prefab)}");
    }

    private static Material BuildMaterial()
    {
        string path = MaterialFolder + "/DevilSmoke.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = 3100;
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(SmokeTexture));
        // 연기 텍스처가 회색이라 HDR 틴트로 보랏빛을 낸다
        mat.SetColor("_BaseColor", new Color(1.5f, 0.7f, 2.1f, 0.9f));
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
