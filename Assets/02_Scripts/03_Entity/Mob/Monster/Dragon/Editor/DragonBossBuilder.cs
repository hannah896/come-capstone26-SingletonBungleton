using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Animations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 최종 보스 월식룡 에셋을 만든다. 메뉴: Tools/최종 보스/월식룡 에셋 생성 (1페이즈 서리룡 + 2페이즈 망령룡)
///
/// 결과물 (이미 있으면 덮어쓴다. 스탯 SO는 이미 있으면 수치를 보존한다)
///  - Assets/04_Animations/Monster/Dragon/{PolarDragon,UndeadDragon}.controller
///    기존 몬스터 방식 (Bool 1개만 true, Entry에서 분기, 자기 Bool이 꺼지면 Exit). In Place 클립만 쓴다
///  - Assets/03_Prefabs/Monster/Dragon/*.prefab : 두 드래곤 · 예고 표식 · 장판(얼음/산성) · 탄(얼음/산성) · 착탄/폭발 이펙트
///  - Assets/05_Datas/SO/MonsterStatData/{PolarDragon,UndeadDragon}.asset : DragonStatData
///  - Assets/08_Materials/Dragon/*.mat : 피부 사본 · 브레스 파티클 · 장판 디스크
/// 프리팹은 Addressables(Prefabs_Monster 그룹)에 파일 이름을 키로 등록하고, 두 드래곤은 MonsterCatalog 끝에 추가한다.
/// </summary>
public static class DragonBossBuilder
{
    private const string DragonPackRoot = "Assets/00_Externals/PremiumAssets/Monster/HEROIC FANTASY CREATURES FULL PACK VOL3/Elemental Dragons Pack/";
    private const string PixPlaysRoot = "Assets/00_Externals/PremiumAssets/Effects/PixPlays/";
    private const string UniTexRoot = "Assets/00_Externals/PremiumAssets/Effects/UNI VFX/Common/Textures/";

    private const string ControllerFolder = "Assets/04_Animations/Monster/Dragon";
    private const string PrefabFolder = "Assets/03_Prefabs/Monster/Dragon";
    private const string MaterialFolder = "Assets/08_Materials/Dragon";
    private const string StatFolder = "Assets/05_Datas/SO/MonsterStatData";
    private const string LineMaterialPath = "Assets/03_Prefabs/Monster/Bat/MageStarLaser.mat";
    private const string CatalogPath = "Assets/Resources/ConfigData/MonsterCatalog.asset";
    private const string AddressableGroup = "Prefabs_Monster";
    private const string UiAddressableGroup = "Prefabs_UI";
    private const string HudFolder = "Assets/03_Prefabs/UI/Boss";
    private const string HudFontPath = "Assets/09_Font/NEXON Football Gothic B SDF.asset";

    /// <summary>드래곤 한 마리의 에셋 사양. 두 드래곤은 클립 이름·뼈 이름·원소만 다르다.</summary>
    private class DragonSpec
    {
        public string Name;            // 프리팹·컨트롤러·SO·Addressable 키
        public string PackFolder;      // 팩 안 폴더
        public string ClipPrefix;      // FBX 파일 접두사 (PolarDragon@ / HellDragon@)
        public string ModelPrefab;     // 원본 PBR 프리팹 파일 이름
        public string BonePrefix;      // 뼈 이름 접두사
        public string SpitClip, BreathClip, FlySpitClip, FlyBreathClip, FallStartClip;
        public Color SkinTint;         // 피부 사본 틴트 (원본이 광택 때문에 어둡게 나와서 밝힌다)
        public Color SkinEmission;
        public Material BreathMaterial;
        public string ImpactVfxKey;    // 추락 충돌·등장 충격파
        public string ExplosionVfxKey; // 서리 폭발·광폭화
    }

    [MenuItem("Tools/최종 보스/월식룡 에셋 생성")]
    public static void BuildAll()
    {
        EnsureFolder(ControllerFolder);
        EnsureFolder(PrefabFolder);
        EnsureFolder(MaterialFolder);

        // 연기는 알파가 있는 8x8 플립북(알파 블렌딩), 빛 핵은 검은 배경 텍스처라 가산 블렌딩
        // 연기 텍스처가 회색이라 HDR 틴트로 밝혀 원소 색을 낸다
        var frostBreathMat = BuildParticleMaterial("DragonFrostBreath", UniTexRoot + "WispySmoke03b_8x8.png", false, new Color(2.6f, 2.9f, 3.3f, 0.85f));
        var acidBreathMat = BuildParticleMaterial("DragonAcidBreath", UniTexRoot + "WispySmoke03b_8x8.png", false, new Color(1.5f, 2.9f, 0.7f, 0.85f));
        var glowMat = BuildParticleMaterial("DragonFrostGlow", UniTexRoot + "uni_glow.png", true, Color.white);
        var discMat = BuildUnlitTransparentMaterial("DragonHazardDisc");
        var lineMat = AssetDatabase.LoadAssetAtPath<Material>(LineMaterialPath);

        var built = new List<GameObject>
        {
            BuildMarkerPrefab(lineMat),
            BuildPatchPrefab("DragonIcePatch", discMat, lineMat,
                new Color(0.55f, 0.85f, 1.2f, 0.35f), new Color(0.7f, 1.1f, 1.8f, 0.9f)),
            BuildPatchPrefab("DragonAcidPool", discMat, lineMat,
                new Color(0.35f, 0.9f, 0.15f, 0.45f), new Color(0.6f, 1.6f, 0.3f, 0.9f)),
            BuildVfxPrefab("DragonFrostImpact",
                PixPlaysRoot + "ElementalProjectiles/Waterball/Version_BuiltIn/WaterballHit/WaterballHit.prefab", 2f),
            // WaterAoeVFX는 바닥 메시가 큰 사각형으로 보여서 쓰지 않는다. 착탄 이펙트를 크게 터뜨린다
            BuildVfxPrefab("DragonFrostExplosion",
                PixPlaysRoot + "ElementalProjectiles/Waterball/Version_BuiltIn/WaterballHit/WaterballHit.prefab", 2.5f),
            BuildBurstVfxPrefab("DragonAcidImpact", glowMat, new Color(0.55f, 1f, 0.25f, 1f)),
            BuildBallPrefab("DragonFrostBall", glowMat, new Color(0.7f, 0.9f, 1f, 0.9f), "DragonFrostImpact"),
            BuildBallPrefab("DragonAcidBall", glowMat, new Color(0.55f, 1f, 0.25f, 0.9f), "DragonAcidImpact"),
        };

        var polar = new DragonSpec
        {
            Name = "PolarDragon", PackFolder = "Polar Dragon/", ClipPrefix = "PolarDragon@", ModelPrefab = "PolarDragon_PBR",
            BonePrefix = "PolarDragon_ ",
            SpitClip = "SpitFrozenBall", BreathClip = "SpreadFrozenBreath",
            FlySpitClip = "FlyStationarySpitFrozenBall", FlyBreathClip = "FlyStationarySpreadFrozenBreath",
            FallStartClip = "FlyStationaryToFalling",
            SkinTint = new Color(1.6f, 1.75f, 1.95f, 1f), SkinEmission = new Color(1.5f, 1.8f, 2.2f, 1f),
            BreathMaterial = frostBreathMat, ImpactVfxKey = "DragonFrostImpact", ExplosionVfxKey = "DragonFrostExplosion",
        };
        var undead = new DragonSpec
        {
            Name = "UndeadDragon", PackFolder = "Undead Dragon/", ClipPrefix = "HellDragon@", ModelPrefab = "HellDragon_PBR",
            BonePrefix = "HellDragon_ ",
            SpitClip = "SpitAcid", BreathClip = "SpreadAcidBreath",
            FlySpitClip = "FlyStationarySpitAcid", FlyBreathClip = "FlyStationarySpreadAcidBreath",
            // 망령룡에는 FlyStationaryToFalling이 없고 "맞고 추락" 클립만 있다 (기획 5.5)
            FallStartClip = "FlyStationaryGetHitToFalling",
            SkinTint = new Color(1.35f, 1.3f, 1.3f, 1f), SkinEmission = new Color(1.6f, 2.4f, 1.2f, 1f),
            BreathMaterial = acidBreathMat, ImpactVfxKey = "DragonAcidImpact", ExplosionVfxKey = "DragonAcidImpact",
        };

        built.Add(BuildDragonPrefab(polar, BuildController(polar), BuildPolarStat()));
        built.Add(BuildDragonPrefab(undead, BuildController(undead), BuildUndeadStat()));

        foreach (var go in built)
            RegisterAddressable(go);

        RegisterAddressable(BuildBossHud(), UiAddressableGroup);

        AddToCatalog("PolarDragon", StatFolder + "/PolarDragon.asset");
        AddToCatalog("UndeadDragon", StatFolder + "/UndeadDragon.asset");

        AssetDatabase.SaveAssets();
        Debug.Log("[DragonBossBuilder] 월식룡 에셋 생성 완료");
    }

    #region Controller
    // (Bool 이름, 상태(=클립) 이름) — 상태 이름은 DragonBoss의 상태 이름 필드와 맞춘다
    private static (string param, string clip)[] StatesOf(DragonSpec spec) => new[]
    {
        ("Idle", "IdleBreathe"),
        ("Walk", "Walk"),
        ("Run", "Run"),
        ("Bite", "BiteAttack"),
        ("Spit", spec.SpitClip),
        ("Breath", spec.BreathClip),
        ("Hit", "GetHit1"),
        ("HitHeavy", "GetHit2"),
        ("Die", "Death"),
        ("TakeOff", "TakeOff"),
        ("FlyIdle", "FlyStationary"),
        ("FlySpit", spec.FlySpitClip),
        ("FlyBreath", spec.FlyBreathClip),
        ("Land", "FlyStationaryToLanding"),
        ("Glide", "Glide"),
        ("GlideLand", "GlideToLanding"),
        ("Crash", "DeathHitTheGround"),
    };

    private static AnimatorController BuildController(DragonSpec spec)
    {
        string path = $"{ControllerFolder}/{spec.Name}.controller";
        string clipPrefix = DragonPackRoot + spec.PackFolder + "FBX Files/" + spec.ClipPrefix;
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);

        var states = StatesOf(spec);
        foreach (var (param, _) in states)
            ctrl.AddParameter(param, AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Fall", AnimatorControllerParameterType.Bool);

        var sm = ctrl.layers[0].stateMachine;
        AnimatorState idle = null;
        int row = 0;

        foreach (var (param, clipName) in states)
        {
            var state = sm.AddState(clipName, new Vector3(450f, row++ * 55f, 0f));
            state.motion = LoadClip(clipPrefix + clipName + ".FBX");
            AddEntryAndExit(sm, state, param);
            if (param == "Idle") idle = state;
        }

        // 격추: 추락 시작 클립 → (클립 끝) Falling 루프. 둘 다 Fall Bool이 꺼지면 빠진다
        var toFall = sm.AddState(spec.FallStartClip, new Vector3(750f, 0f, 0f));
        toFall.motion = LoadClip(clipPrefix + spec.FallStartClip + ".FBX");
        var falling = sm.AddState("Falling", new Vector3(750f, 55f, 0f));
        falling.motion = LoadClip(clipPrefix + "Falling.FBX");
        AddEntryAndExit(sm, toFall, "Fall");
        var chain = toFall.AddTransition(falling);
        chain.hasExitTime = true;
        chain.exitTime = 1f;
        chain.hasFixedDuration = true;
        chain.duration = 0.1f;
        AddExit(falling, "Fall");

        sm.defaultState = idle;
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    private static void AddEntryAndExit(AnimatorStateMachine sm, AnimatorState state, string param)
    {
        var entry = sm.AddEntryTransition(state);
        entry.AddCondition(AnimatorConditionMode.If, 0f, param);
        AddExit(state, param);
    }

    private static void AddExit(AnimatorState state, string param)
    {
        var exit = state.AddExitTransition();
        exit.hasExitTime = false;
        exit.hasFixedDuration = true;
        exit.duration = param == "Crash" || param == "Fall" ? 0.1f : 0.25f;
        exit.AddCondition(AnimatorConditionMode.IfNot, 0f, param);
    }

    private static AnimationClip LoadClip(string fbxPath)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
        {
            if (o is AnimationClip c && !c.name.StartsWith("__preview"))
                return c;
        }
        Debug.LogError($"[DragonBossBuilder] 클립 없음: {fbxPath}");
        return null;
    }
    #endregion

    #region Materials
    private static Material BuildParticleMaterial(string name, string texturePath, bool additive, Color tint)
    {
        var mat = LoadOrCreateMaterial(name, "Universal Render Pipeline/Particles/Unlit");
        MakeTransparent(mat);
        if (additive)
        {
            mat.SetFloat("_Blend", 2f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (tex != null) mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", tint);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>
    /// 드래곤 피부 사본. 원본(M_PolarDragon / M_HellDragon_*)은 광택 1.0이라 이 프로젝트 조명에서 거의 검게 나온다.
    /// 외부 에셋은 고치지 않고, 같은 텍스처로 밝히고 광택을 낮춘 사본을 만든다.
    /// </summary>
    private static Material BuildSkinMaterial(Material source, Color tint, Color emission)
    {
        var mat = LoadOrCreateMaterial((source != null ? source.name : "M_Dragon") + "_Boss", "Universal Render Pipeline/Lit");
        Texture emissionMap = null;
        bool alphaClip = false;
        if (source != null)
        {
            mat.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
            mat.SetTexture("_BumpMap", source.GetTexture("_BumpMap"));
            mat.SetTexture("_OcclusionMap", source.GetTexture("_OcclusionMap"));
            emissionMap = source.GetTexture("_EmissionMap");
            mat.SetTexture("_EmissionMap", emissionMap);

            // 날개막처럼 구멍이 뚫린 부위는 알파 클리핑을 그대로 가져온다
            alphaClip = source.HasProperty("_AlphaClip") && source.GetFloat("_AlphaClip") > 0.5f;
            mat.SetFloat("_Cull", source.HasProperty("_Cull") ? source.GetFloat("_Cull") : 2f);
            if (source.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", source.GetFloat("_Cutoff"));
        }
        mat.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
        mat.SetFloat("_AlphaToMask", alphaClip ? 1f : 0f);
        if (alphaClip) mat.EnableKeyword("_ALPHATEST_ON");
        else mat.DisableKeyword("_ALPHATEST_ON");

        mat.EnableKeyword("_NORMALMAP");
        mat.EnableKeyword("_OCCLUSIONMAP");
        mat.SetColor("_BaseColor", tint);
        // 발광 텍스처가 없는 부위(날개)에 발광을 켜면 통째로 하얗게 빛난다
        if (emissionMap != null)
        {
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", emission);
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            mat.SetColor("_EmissionColor", Color.black);
        }
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.45f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material BuildUnlitTransparentMaterial(string name)
    {
        var mat = LoadOrCreateMaterial(name, "Universal Render Pipeline/Unlit");
        MakeTransparent(mat);
        mat.SetColor("_BaseColor", new Color(0.55f, 0.85f, 1f, 0.35f));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material LoadOrCreateMaterial(string name, string shaderName)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find(shaderName));
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = Shader.Find(shaderName);
        }
        return mat;
    }

    // URP 알파 블렌딩 투명 설정
    private static void MakeTransparent(Material mat)
    {
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        // 물 같은 투명 바닥에 가려지지 않도록 투명 큐보다 뒤에 그린다 (MageStarLaser와 같은 이유)
        mat.renderQueue = 3100;
    }
    #endregion

    #region Stat
    private static DragonStatData LoadOrCreateStat(string name, out bool created)
    {
        string path = $"{StatFolder}/{name}.asset";
        var stat = AssetDatabase.LoadAssetAtPath<DragonStatData>(path);
        created = stat == null;
        if (!created) return stat; // 조정한 수치를 덮어쓰지 않는다

        stat = ScriptableObject.CreateInstance<DragonStatData>();
        stat.MinAttackPeriod = 1f;
        stat.FOV = 360f;
        stat.DetectRange = 60f;
        stat.AttackRange = 40f;
        stat.TurnSpeed = 70f;
        AssetDatabase.CreateAsset(stat, path);
        return stat;
    }

    // 1페이즈 서리룡: 클래스 기본값이 곧 서리룡 수치다
    private static DragonStatData BuildPolarStat()
    {
        var stat = LoadOrCreateStat("PolarDragon", out bool created);
        if (!created) return stat;
        stat.MaxHP = stat.CurHP = 6000f;
        stat.MoveSpeed = 2.5f;
        stat.AttackDamage = 30f;
        stat.Defense = 10f;
        EditorUtility.SetDirty(stat);
        return stat;
    }

    // 2페이즈 망령룡 (기획 5장 · 6장)
    private static DragonStatData BuildUndeadStat()
    {
        var stat = LoadOrCreateStat("UndeadDragon", out bool created);

        // 체력바 표시값: 아직 서리룡 기본값이면 망령룡 것으로 채운다 (SO가 먼저 만들어진 경우 포함)
        var defaults = ScriptableObject.CreateInstance<DragonStatData>();
        bool isDefaultName = stat.BossName == defaults.BossName;
        Object.DestroyImmediate(defaults);
        if (isDefaultName)
        {
            stat.BossName = "월식의 망령룡";
            stat.HpLineCount = 40;
            stat.HpLineColors = new[]
            {
                new Color(0.85f, 0.15f, 0.2f),
                new Color(0.7f, 0.25f, 0.65f),
                new Color(1f, 0.45f, 0.15f),
                new Color(0.55f, 0.85f, 0.2f),
            };
            EditorUtility.SetDirty(stat);
        }

        // 5단계 기믹(어둠·응시·각인): 아직 비어 있으면 채운다 (이미 만든 SO 포함)
        if (stat.EclipseEgoDrainPerSecond <= 0f && (stat.GazeHpRatios == null || stat.GazeHpRatios.Length == 0))
        {
            stat.EclipseEgoDrainPerSecond = 1f;
            stat.GazeHpRatios = new[] { 0.75f, 0.375f }; // ×40줄 중 ×30, ×15
            stat.MarkHpRatios = new[] { 0.625f, 0.2f };  // ×25, ×8
            EditorUtility.SetDirty(stat);
        }
        // 광폭화: 2페이즈는 10분 (클래스 기본값 8분은 1페이즈 것)
        if (Mathf.Approximately(stat.EnrageTime, 480f))
        {
            stat.EnrageTime = 600f;
            EditorUtility.SetDirty(stat);
        }
        if (!created) return stat;
        stat.MaxHP = stat.CurHP = 8000f;
        stat.MoveSpeed = 2.8f;
        stat.AttackDamage = 35f;
        stat.Defense = 12f;

        stat.PoiseMax = 800f;
        stat.BiteDamage = 50f;
        stat.BiteCorrosionMultiplier = 1.2f;   // 부식: 받는 피해 +20%, 10초
        stat.BiteCorrosionDuration = 10f;

        // 산성 침: 착탄 지점에 사라지지 않는 웅덩이 (도트, 약한 둔화)
        stat.FrostBallKey = "DragonAcidBall";
        stat.IcePatchKey = "DragonAcidPool";
        stat.IcePatchDuration = 0f;
        stat.IcePatchSlow = 0.85f;
        stat.PatchDotPerSecond = 8f;
        stat.BombardPatchDuration = 12f;
        stat.SpitReleaseTime = 0.45f;

        // 부식 브레스: 빙결 대신 피해가 조금 세고, 기둥을 부식시킨다
        stat.BreathStartTime = 1.0f;
        stat.BreathEndTime = 2.9f;
        stat.BreathTickDamage = 9f;
        stat.FreezePerTick = 0f;
        stat.BreathCorrodesCover = true;
        stat.FlyBreathStartTime = 0.6f;
        stat.FlyBreathEndTime = 3.0f;
        stat.FlySpitReleaseTime = 0.38f;

        // 망령 돌진: 경로에 산성 웅덩이
        stat.ChargeSpeed = 10f;
        stat.ChargeTrailSpacing = 4f;

        // 공중: ×20줄(체력 50%)에서 한 번. 실패하면 월식 폭우
        stat.TakeOffHpRatios = new[] { 0.5f };
        stat.ShootdownMax = 350f;
        stat.FallStartDrop = 5.4f; // FlyStationaryGetHitToFalling 동안 몸이 5.4m 솟는다
        stat.ShootdownFailMode = DragonShootdownFail.AcidRain;

        // 최종 발악: ×5줄 이하
        stat.FrenzyHpRatio = 0.125f;
        EditorUtility.SetDirty(stat);
        return stat;
    }
    #endregion

    #region Prefabs
    private static GameObject BuildMarkerPrefab(Material lineMat)
    {
        var go = new GameObject("DragonGroundMarker");
        var marker = go.AddComponent<DragonGroundMarker>();
        SetField(marker, "lineMaterial", lineMat);
        return SavePrefab(go);
    }

    private static GameObject BuildPatchPrefab(string name, Material discMat, Material lineMat, Color fill, Color line)
    {
        var go = new GameObject(name);
        var zone = go.AddComponent<DragonHazardZone>();
        SetField(zone, "fillMaterial", discMat);
        SetField(zone, "lineMaterial", lineMat);
        SetField(zone, "fillColor", fill);
        SetField(zone, "lineColor", line);
        return SavePrefab(go);
    }

    private static GameObject BuildVfxPrefab(string name, string sourcePrefabPath, float lifeTime)
    {
        var go = new GameObject(name);
        var vfx = go.AddComponent<DragonVfx>();
        SetField(vfx, "lifeTime", lifeTime);

        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);
        if (source != null)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(source, go.transform);
            inst.transform.localPosition = Vector3.zero;
        }
        else
        {
            Debug.LogError($"[DragonBossBuilder] 이펙트 원본 없음: {sourcePrefabPath}");
        }
        return SavePrefab(go);
    }

    // 원소 색 빛 알갱이가 사방으로 튀는 간단한 착탄 이펙트 (산성용 — PixPlays 물 이펙트는 파란색이라)
    private static GameObject BuildBurstVfxPrefab(string name, Material glowMat, Color color)
    {
        var go = new GameObject(name);
        var vfx = go.AddComponent<DragonVfx>();
        SetField(vfx, "lifeTime", 1.5f);

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.3f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
        main.startColor = color;
        main.gravityModifier = 1f;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.6f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = FadeGradient(color);
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = glowMat;
        return SavePrefab(go);
    }

    private static GameObject BuildBallPrefab(string name, Material glowMat, Color color, string impactKey)
    {
        var go = new GameObject(name);
        var ball = go.AddComponent<DragonFrostBall>();

        var fx = new GameObject("FlightFx");
        fx.transform.SetParent(go.transform, false);

        // 빛나는 핵
        var core = fx.AddComponent<ParticleSystem>();
        var main = core.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = 0.25f;
        main.startSpeed = 0f;
        main.startSize = 1.6f;
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = core.emission;
        emission.rateOverTime = 40f;
        var shape = core.shape;
        shape.enabled = false;
        core.GetComponent<ParticleSystemRenderer>().sharedMaterial = glowMat;

        // 꼬리 (플립북 연기는 시트 설정이 필요해 빛 텍스처로 만든다)
        var trailGo = new GameObject("Trail");
        trailGo.transform.SetParent(fx.transform, false);
        var trail = trailGo.AddComponent<ParticleSystem>();
        var tmain = trail.main;
        tmain.loop = true;
        tmain.playOnAwake = false;
        tmain.startLifetime = 0.5f;
        tmain.startSpeed = 0.5f;
        tmain.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        tmain.startColor = new Color(color.r, color.g, color.b, 0.6f);
        tmain.simulationSpace = ParticleSystemSimulationSpace.World;
        var temission = trail.emission;
        temission.rateOverTime = 60f;
        var tshape = trail.shape;
        tshape.shapeType = ParticleSystemShapeType.Sphere;
        tshape.radius = 0.3f;
        var tcol = trail.colorOverLifetime;
        tcol.enabled = true;
        tcol.color = FadeGradient(color);
        trail.GetComponent<ParticleSystemRenderer>().sharedMaterial = glowMat;

        SetField(ball, "flightFx", fx.transform);
        SetField(ball, "impactVfxKey", impactKey);
        return SavePrefab(go);
    }

    private static GameObject BuildDragonPrefab(DragonSpec spec, AnimatorController controller, DragonStatData stat)
    {
        var root = new GameObject(spec.Name);

        // 몸통 콜라이더 (루트, 제자리 기준). 머리·목은 뼈에 붙여 애니메이션을 따라가게 한다
        var body = root.AddComponent<CapsuleCollider>();
        body.direction = 2;
        body.center = new Vector3(0f, 2.3f, -1f);
        body.radius = 1.8f;
        body.height = 9f;

        var modelSource = AssetDatabase.LoadAssetAtPath<GameObject>(DragonPackRoot + spec.PackFolder + "Prefabs/" + spec.ModelPrefab + ".prefab");
        var model = (GameObject)PrefabUtility.InstantiatePrefab(modelSource, root.transform);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        var smr = model.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr != null)
        {
            var mats = smr.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = BuildSkinMaterial(mats[i], spec.SkinTint, spec.SkinEmission);
            smr.sharedMaterials = mats;
        }

        var animator = model.GetComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        // 호스트 화면 밖에서도 뼈가 움직여야 머리 판정·브레스 방향이 맞는다
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        Transform head = FindDeep(model.transform, spec.BonePrefix + "Head");
        Transform neck = FindDeep(model.transform, spec.BonePrefix + "Neck1");
        AddBoneSphere(head, "HeadCollider", 1.1f);
        AddBoneSphere(neck, "NeckCollider", 1.0f);

        // 브레스 파티클 (DragonBreathFx가 매 프레임 입 위치로 옮긴다)
        var breathGo = new GameObject("BreathFx");
        breathGo.transform.SetParent(root.transform, false);
        var breathPs = BuildBreathParticles(breathGo, spec.BreathMaterial);

        var boss = root.AddComponent<DragonBoss>();
        var breathFx = breathGo.AddComponent<DragonBreathFx>();
        SetField(breathFx, "boss", boss);
        SetField(breathFx, "systems", new Object[] { breathPs });

        var so = new SerializedObject(boss);
        so.FindProperty("statData").objectReferenceValue = stat;
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("deathFallbackTimeout").floatValue = 4f;
        so.FindProperty("targetMask").intValue = unchecked((int)0x80000000); // 기존 몬스터와 같은 플레이어 레이어
        so.FindProperty("idleBool").stringValue = "Idle";
        so.FindProperty("moveBool").stringValue = "Walk";
        so.FindProperty("chaseBool").stringValue = "";
        so.FindProperty("attackBool").stringValue = "Bite";
        so.FindProperty("deadBool").stringValue = "Die";
        so.FindProperty("hitBool").stringValue = "Hit";
        so.FindProperty("useSpawnAnim").boolValue = false;
        so.FindProperty("wanderRadius").floatValue = 0f;
        so.FindProperty("knockbackSpeed").floatValue = 0f;
        so.FindProperty("netSnapDistance").floatValue = 12f;
        so.FindProperty("spitStateName").stringValue = spec.SpitClip;
        so.FindProperty("breathStateName").stringValue = spec.BreathClip;
        so.FindProperty("flySpitStateName").stringValue = spec.FlySpitClip;
        so.FindProperty("flyBreathStateName").stringValue = spec.FlyBreathClip;
        so.FindProperty("headBone").objectReferenceValue = head;
        so.FindProperty("neckBone").objectReferenceValue = neck;
        so.FindProperty("breathFx").objectReferenceValue = breathFx;
        so.FindProperty("crashVfxKey").stringValue = spec.ImpactVfxKey;
        so.FindProperty("explosionVfxKey").stringValue = spec.ExplosionVfxKey;
        so.ApplyModifiedPropertiesWithoutUndo();

        return SavePrefab(root);
    }

    private static ParticleSystem BuildBreathParticles(GameObject go, Material mat)
    {
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(14f, 18f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white; // 색은 머티리얼 틴트(얼음/산성)가 정한다
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 800;

        var emission = ps.emission;
        emission.rateOverTime = 160f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.35f;

        // WispySmoke03b_8x8 플립북
        var sheet = ps.textureSheetAnimation;
        sheet.enabled = true;
        sheet.numTilesX = 8;
        sheet.numTilesY = 8;
        sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 63f);
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 4.5f));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = FadeGradient(Color.white, fadeIn: 0.03f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }

    private static Gradient FadeGradient(Color color, float fadeIn = 0.12f)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(0.3f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    private static void AddBoneSphere(Transform bone, string name, float radius)
    {
        if (bone == null)
        {
            Debug.LogError($"[DragonBossBuilder] 뼈 없음: {name}");
            return;
        }
        var go = new GameObject(name);
        go.transform.SetParent(bone, false);
        var sphere = go.AddComponent<SphereCollider>();
        sphere.radius = radius;
    }
    #endregion

    #region Boss HUD
    [MenuItem("Tools/최종 보스/보스 HUD만 다시 만들기")]
    public static void RebuildHudOnly()
    {
        RegisterAddressable(BuildBossHud(), UiAddressableGroup);
        AssetDatabase.SaveAssets();
        Debug.Log("[DragonBossBuilder] 보스 HUD 재생성 완료");
    }

    /// <summary>
    /// 보스 체력바 HUD 프리팹 (1920x1080 기준, 화면 상단 중앙).
    /// 이름(왼쪽) · xN(오른쪽) / 체력바(아래 줄 색 · 잔상 · 현재 줄) / 무력화 게이지 / 상태 문구(그로기·격추) / 어둠 안내
    /// + 화면 가운데 위쪽 기믹 경고 문구(응시·각인)
    /// </summary>
    private static GameObject BuildBossHud()
    {
        EnsureFolder(HudFolder);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HudFontPath);

        var root = new GameObject("UI_Hud_BossHealth", typeof(RectTransform), typeof(CanvasGroup));
        var rootRt = (RectTransform)root.transform;
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;
        var group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        // 화면 효과는 문구·체력바보다 먼저 만들어 뒤에 깔린다 (비네트 스프라이트는 런타임에 만든다)
        var vignette = NewImage("Vignette", root.transform, Color.clear);
        Stretch(vignette.rectTransform, 0f);
        var flash = NewImage("Flash", root.transform, Color.clear);
        Stretch(flash.rectTransform, 0f);

        var panel = NewRect("Panel", root.transform);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.anchoredPosition = new Vector2(0f, -28f);
        panel.sizeDelta = new Vector2(960f, 100f);

        var nameText = NewText("Name", panel, font, 28f, TextAlignmentOptions.BottomLeft);
        Place(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(640f, 36f));
        var lineText = NewText("Lines", panel, font, 28f, TextAlignmentOptions.BottomRight);
        Place(lineText.rectTransform, new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(200f, 36f));

        // 체력바: 테두리 -> 배경 -> 아래 줄 -> 잔상 -> 현재 줄 (뒤에 만든 것이 위에 그려진다)
        var hpFrame = NewImage("HpFrame", panel, new Color(0f, 0f, 0f, 0.85f));
        Place(hpFrame.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(964f, 30f));
        var hpBack = NewImage("HpBack", hpFrame.rectTransform, new Color(0.08f, 0.08f, 0.1f, 1f));
        Stretch(hpBack.rectTransform, 2f);
        var under = NewImage("HpUnder", hpBack.rectTransform, Color.gray);
        Stretch(under.rectTransform, 0f);
        var ghost = NewImage("HpGhost", hpBack.rectTransform, Color.white);
        Stretch(ghost.rectTransform, 0f);
        var fill = NewImage("HpFill", hpBack.rectTransform, Color.red);
        Stretch(fill.rectTransform, 0f);

        // 무력화(격추) 게이지
        var gaugeFrame = NewImage("GaugeFrame", panel, new Color(0f, 0f, 0f, 0.85f));
        Place(gaugeFrame.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(964f, 12f));
        var gaugeBack = NewImage("GaugeBack", gaugeFrame.rectTransform, new Color(0.08f, 0.08f, 0.1f, 1f));
        Stretch(gaugeBack.rectTransform, 2f);
        var gaugeFill = NewImage("GaugeFill", gaugeBack.rectTransform, new Color(1f, 0.72f, 0.2f, 1f));
        Stretch(gaugeFill.rectTransform, 0f);

        var stateText = NewText("State", panel, font, 26f, TextAlignmentOptions.Top);
        Place(stateText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(400f, 34f));

        var timerText = NewText("EnrageTimer", panel, font, 24f, TextAlignmentOptions.TopRight);
        Place(timerText.rectTransform, new Vector2(1f, 1f), new Vector2(0f, -90f), new Vector2(200f, 32f));

        var hintText = NewText("DarknessHint", panel, font, 22f, TextAlignmentOptions.Top);
        Place(hintText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(900f, 30f));

        // 기믹 경고 문구: 화면 가운데보다 조금 위 (조준점을 가리지 않게)
        var alertText = NewText("Alert", root.transform, font, 44f, TextAlignmentOptions.Center);
        alertText.rectTransform.anchorMin = alertText.rectTransform.anchorMax = new Vector2(0.5f, 0.68f);
        alertText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        alertText.rectTransform.anchoredPosition = Vector2.zero;
        alertText.rectTransform.sizeDelta = new Vector2(1400f, 70f);

        var hud = root.AddComponent<UI_Hud_BossHealth>();
        var so = new SerializedObject(hud);
        so.FindProperty("_group").objectReferenceValue = group;
        so.FindProperty("_nameText").objectReferenceValue = nameText;
        so.FindProperty("_lineText").objectReferenceValue = lineText;
        so.FindProperty("_stateText").objectReferenceValue = stateText;
        so.FindProperty("_hpUnder").objectReferenceValue = under;
        so.FindProperty("_hpGhost").objectReferenceValue = ghost;
        so.FindProperty("_hpFill").objectReferenceValue = fill;
        so.FindProperty("_gaugeFill").objectReferenceValue = gaugeFill;
        so.FindProperty("_alertText").objectReferenceValue = alertText;
        so.FindProperty("_hintText").objectReferenceValue = hintText;
        so.FindProperty("_timerText").objectReferenceValue = timerText;
        so.FindProperty("_vignette").objectReferenceValue = vignette;
        so.FindProperty("_flash").objectReferenceValue = flash;
        so.ApplyModifiedPropertiesWithoutUndo();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, HudFolder + "/UI_Hud_BossHealth.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        var rt = NewRect(name, parent);
        var image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions align)
    {
        var rt = NewRect(name, parent);
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.alignment = align;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(0, 0, 0, 255);
        return text;
    }

    // 부모 위쪽 기준으로 배치 (anchor = pivot)
    private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    // 부모를 inset만큼 안쪽으로 꽉 채운다 (막대는 HUD 스크립트가 anchorMax.x로 길이를 바꾼다)
    private static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0f, 0.5f);
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }
    #endregion

    #region Helpers
    private static GameObject SavePrefab(GameObject go)
    {
        string path = $"{PrefabFolder}/{go.name}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static void SetField(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetField(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetField(Object target, string field, float value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetField(Object target, string field, string value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetField(Object target, string field, Color value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).colorValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            var found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static void RegisterAddressable(GameObject prefab, string groupName = AddressableGroup)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null || prefab == null) return;

        var group = settings.FindGroup(groupName) ?? settings.DefaultGroup;
        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
        var entry = settings.CreateOrMoveEntry(guid, group);
        entry.address = prefab.name;
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
    }

    /// <summary>MonsterCatalog 끝에 추가한다 (이미 있으면 그대로). CatalogId가 순서라 중간에 넣지 않는다.</summary>
    private static void AddToCatalog(string key, string statPath)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogError($"[DragonBossBuilder] MonsterCatalog 없음: {CatalogPath}");
            return;
        }
        if (catalog.GetCatalogId(key) != 0) return;

        var so = new SerializedObject(catalog);
        var entries = so.FindProperty("entries");
        int index = entries.arraySize;
        entries.InsertArrayElementAtIndex(index);
        var e = entries.GetArrayElementAtIndex(index);
        e.FindPropertyRelative("addressableKey").stringValue = key;
        e.FindPropertyRelative("statData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MonsterStatData>(statPath);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    #endregion
}
