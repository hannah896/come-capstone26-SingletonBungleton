using System.IO;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SingletonBungleton.BossTraining.Editor
{
    /// <summary>
    /// 최종 보스 드래곤 판단층 학습 환경을 만들고, 학습된 모델을 본편 드래곤에 연결하는 에디터 도구.
    /// ML-Agents 패키지가 설치돼 있을 때만 나타나는 메뉴다.
    ///
    ///  - Tools/보스 학습/용 학습 씬 생성       : DragonTrainingArena 프리팹 + 병렬 학습 씬(4x4)
    ///  - Tools/보스 학습/용에 학습 모델 연결   : Assets/05_Datas/BossModel/DragonDecision.onnx 를 PolarDragon·UndeadDragon 프리팹에 붙인다
    ///  - Tools/보스 학습/용에서 학습 모델 해제 : 위에서 붙인 컴포넌트를 떼고 기존 규칙 기반 AI로 되돌린다
    /// </summary>
    public static class DragonTrainingSetup
    {
        private const string PrefabFolder = "Assets/03_Prefabs/Training";
        private const string SceneFolder = "Assets/01_Scenes/Training";
        private const string MaterialFolder = "Assets/08_Materials/Training";
        private const string ArenaPrefabPath = PrefabFolder + "/DragonTrainingArena.prefab";
        private const string ScenePath = SceneFolder + "/DragonTrainingScene.unity";
        private const string ModelPath = "Assets/05_Datas/BossModel/DragonDecision.onnx";

        private static readonly string[] DragonPrefabs =
        {
            "Assets/03_Prefabs/Monster/Dragon/PolarDragon.prefab",
            "Assets/03_Prefabs/Monster/Dragon/UndeadDragon.prefab",
        };

        // 학습 아레나 격자 (아레나 반경 32m → 한 변 64m, 간격 72m)
        private const int GridCount = 4;
        private const float GridSpacing = 72f;
        private const float ArenaRadius = 32f;

        #region 학습 씬

        [MenuItem("Tools/보스 학습/용 학습 씬 생성")]
        public static void BuildTrainingScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(PrefabFolder);
            EnsureFolder(SceneFolder);
            EnsureFolder(MaterialFolder);

            Material floorMat = GetMaterial("Training_Floor", new Color(0.32f, 0.34f, 0.36f));
            Material bossMat = GetMaterial("Training_Boss", new Color(0.85f, 0.15f, 0.12f));
            Material playerMat = GetMaterial("Training_Player", new Color(0.2f, 0.45f, 0.9f));
            Material markerMat = GetMaterial("Training_Marker", new Color(1f, 0.9f, 0.2f));

            BuildArenaPrefab(floorMat, bossMat, playerMat, markerMat);
            BuildScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[용 학습] 학습 아레나와 씬을 만들었습니다.\n{ArenaPrefabPath}\n{ScenePath}\n" +
                      "파이썬: mlagents-learn MLAgents/dragon_ppo.yaml --run-id=dragon-ppo-01 --time-scale=20 → 유니티에서 DragonTrainingScene Play");
        }

        private static void BuildArenaPrefab(Material floorMat, Material bossMat, Material playerMat, Material markerMat)
        {
            var root = new GameObject("DragonTrainingArena");
            try
            {
                // 원형 바닥 (반경 32m = 본편 ArenaRadius). 실린더 프리미티브의 지름은 스케일 x 이므로 반경 x 2.
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                floor.name = "Floor";
                floor.transform.SetParent(root.transform, false);
                floor.transform.localPosition = new Vector3(0f, -0.1f, 0f);
                floor.transform.localScale = new Vector3(ArenaRadius * 2f, 0.1f, ArenaRadius * 2f);
                floor.GetComponent<Renderer>().sharedMaterial = floorMat;
                Object.DestroyImmediate(floor.GetComponent<Collider>());

                TrainingCombatant boss = CreateCombatant("Boss", root.transform, new Vector3(0f, 0f, -8f), 3.2f, 4.2f, bossMat, markerMat);
                TrainingCombatant player = CreateCombatant("SparringPlayer", root.transform, new Vector3(0f, 0f, 8f), 1.0f, 2.0f, playerMat, markerMat);

                var arena = root.AddComponent<DragonTrainingArena>();
                var so = new SerializedObject(arena);
                so.FindProperty("boss").objectReferenceValue = boss;
                so.FindProperty("player").objectReferenceValue = player;
                so.FindProperty("arenaRadius").floatValue = ArenaRadius;
                so.ApplyModifiedPropertiesWithoutUndo();

                // BehaviorParameters가 Agent보다 먼저 있어야 Agent 초기화 시 에러가 나지 않는다
                var behavior = boss.gameObject.AddComponent<BehaviorParameters>();
                ConfigureBehavior(behavior, BehaviorType.Default);

                var agent = boss.gameObject.AddComponent<DragonDecisionAgent>();
                agent.MaxStep = 0;   // 시간 제한은 아레나가 EpisodeInterrupted로 처리한다
                var agentSo = new SerializedObject(agent);
                agentSo.FindProperty("arena").objectReferenceValue = arena;
                agentSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, ArenaPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureBehavior(BehaviorParameters behavior, BehaviorType type)
        {
            behavior.BehaviorName = DragonObs.BehaviorName;
            behavior.BehaviorType = type;
            behavior.BrainParameters.VectorObservationSize = DragonObs.Size;
            behavior.BrainParameters.NumStackedVectorObservations = 1;
            behavior.BrainParameters.ActionSpec = new ActionSpec(0, new[] { DragonObs.ActionCount });
        }

        private static TrainingCombatant CreateCombatant(string name, Transform parent, Vector3 localPos, float width, float height, Material body, Material marker)
        {
            // 루트는 바닥 높이(Y=0)에 두고 아레나가 위치/회전을 직접 갱신한다. 눈에 보이는 몸통은 자식으로 높이의 절반만큼 올린다.
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;

            GameObject bodyGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bodyGo.name = "Body";
            bodyGo.transform.SetParent(root.transform, false);
            bodyGo.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            bodyGo.transform.localScale = new Vector3(width, height * 0.5f, width);
            Object.DestroyImmediate(bodyGo.GetComponent<Collider>());
            bodyGo.GetComponent<Renderer>().sharedMaterial = body;

            // 정면 표시용 노란 막대 (회전/방향을 눈으로 확인하기 위함)
            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "FacingMarker";
            nose.transform.SetParent(root.transform, false);
            nose.transform.localPosition = new Vector3(0f, height * 0.75f, width * 0.55f);
            nose.transform.localScale = new Vector3(width * 0.3f, width * 0.25f, width * 0.5f);
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.GetComponent<Renderer>().sharedMaterial = marker;

            return root.AddComponent<TrainingCombatant>();
        }

        private static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var arenaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefabPath);
            float offset = (GridCount - 1) * GridSpacing * 0.5f;
            for (int x = 0; x < GridCount; x++)
            {
                for (int z = 0; z < GridCount; z++)
                {
                    var arena = (GameObject)PrefabUtility.InstantiatePrefab(arenaPrefab);
                    arena.name = $"DragonArena_{x}_{z}";
                    arena.transform.position = new Vector3(x * GridSpacing - offset, 0f, z * GridSpacing - offset);
                }
            }

            var hud = new GameObject("TrainingHud");
            hud.AddComponent<DragonTrainingHud>();

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(55f, 30f, 0f);

            // 위에서 내려다보는 직교 카메라: 격자 전체가 한 화면에 들어온다 (발표용 스크린샷)
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = GridCount * GridSpacing * 0.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.07f, 0.08f);
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 300f, 0f);
            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        #endregion

        #region 모델 연결

        // 학습 아레나에 모델을 꽂아 같은 조건(300판)으로 평가한다. 파이썬 트레이너 없이 Play하면 모델로 추론하고 CSV에 "trained"로 기록된다.
        [MenuItem("Tools/보스 학습/학습 아레나에 모델 연결 (평가용)")]
        public static void AssignModelToTrainingArena() => SetTrainingArenaModel(true);

        [MenuItem("Tools/보스 학습/학습 아레나에서 모델 해제 (규칙 기반/학습용)")]
        public static void ClearModelFromTrainingArena() => SetTrainingArenaModel(false);

        private static void SetTrainingArenaModel(bool assign)
        {
            var model = assign ? AssetDatabase.LoadAssetAtPath<Object>(ModelPath) : null;
            if (assign && model == null)
            {
                Debug.LogError($"[용 학습] 학습된 모델이 없습니다: {ModelPath}");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefabPath) == null)
            {
                Debug.LogError($"[용 학습] 학습 아레나 프리팹이 없습니다: {ArenaPrefabPath}");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(ArenaPrefabPath);
            try
            {
                var behavior = root.GetComponentInChildren<BehaviorParameters>();
                var behaviorSo = new SerializedObject(behavior);
                behaviorSo.FindProperty("m_Model").objectReferenceValue = model;
                behaviorSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, ArenaPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(assign
                ? "[용 학습] 학습 아레나에 모델을 연결했습니다. 파이썬 없이 DragonTrainingScene을 Play하면 모델로 평가합니다."
                : "[용 학습] 학습 아레나에서 모델을 뗐습니다. (파이썬 없이 Play하면 규칙 기반, mlagents-learn과 Play하면 학습)");
        }

        [MenuItem("Tools/보스 학습/용에 학습 모델 연결")]
        public static void AttachModel()
        {
            var model = AssetDatabase.LoadAssetAtPath<Object>(ModelPath);
            if (model == null)
            {
                Debug.LogError($"[용 학습] 학습된 모델이 없습니다: {ModelPath}\n" +
                               "results/<run-id>/DragonBoss.onnx 를 이 경로로 복사한 뒤 다시 실행하세요.");
                return;
            }

            System.Type providerType = System.Type.GetType("DragonSituationProvider, Assembly-CSharp");
            if (providerType == null)
            {
                Debug.LogError("[용 학습] DragonSituationProvider 타입을 찾지 못했습니다. 컴파일 에러가 없는지 확인하세요.");
                return;
            }

            int attached = 0;
            foreach (string path in DragonPrefabs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogWarning($"[용 학습] 프리팹이 없어 건너뜁니다: {path}");
                    continue;
                }

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent(providerType) == null) root.AddComponent(providerType);

                    var behavior = root.GetComponent<BehaviorParameters>();
                    if (behavior == null) behavior = root.AddComponent<BehaviorParameters>();
                    ConfigureBehavior(behavior, BehaviorType.InferenceOnly);
                    var behaviorSo = new SerializedObject(behavior);
                    behaviorSo.FindProperty("m_Model").objectReferenceValue = model;
                    behaviorSo.ApplyModifiedPropertiesWithoutUndo();

                    if (root.GetComponent<DragonDecisionAgent>() == null)
                        root.AddComponent<DragonDecisionAgent>().MaxStep = 0;

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    attached++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[용 학습] 학습 모델을 드래곤 프리팹 {attached}개에 연결했습니다. 호스트(싱글)에서만 모델이 패턴을 고르고, 모델이 없거나 실패하면 기존 규칙 AI로 돌아갑니다.");
        }

        [MenuItem("Tools/보스 학습/용에서 학습 모델 해제")]
        public static void DetachModel()
        {
            System.Type providerType = System.Type.GetType("DragonSituationProvider, Assembly-CSharp");

            foreach (string path in DragonPrefabs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var agent = root.GetComponent<DragonDecisionAgent>();
                    if (agent != null) Object.DestroyImmediate(agent);

                    var behavior = root.GetComponent<BehaviorParameters>();
                    if (behavior != null) Object.DestroyImmediate(behavior);

                    if (providerType != null)
                    {
                        var provider = root.GetComponent(providerType);
                        if (provider != null) Object.DestroyImmediate(provider);
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[용 학습] 드래곤 프리팹에서 학습 모델 관련 컴포넌트를 모두 뗐습니다. 기존 규칙 기반 AI를 씁니다.");
        }

        #endregion

        #region 유틸

        private static Material GetMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;

            string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetPath));
        }

        #endregion
    }
}
