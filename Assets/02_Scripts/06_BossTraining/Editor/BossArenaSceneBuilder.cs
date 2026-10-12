using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SingletonBungleton.BossTraining.Editor
{
    /// <summary>
    /// 보스 강화학습용 아레나 프리팹과 학습 씬을 만든다. (게임 제작용 공간 — 실제 플레이어용이 아님)
    /// 메뉴: Tools/보스 학습/아레나 씬 생성
    ///
    /// 결과물
    ///  - Assets/03_Prefabs/Training/BossTrainingArena.prefab : 바닥 + 벽 + 보스 + 스파링 플레이어 + BossTrainingArena
    ///  - Assets/01_Scenes/Training/BossTrainingScene.unity   : 아레나 프리팹을 격자로 여러 개 깔아 병렬 학습하는 씬
    ///  - Assets/08_Materials/Training/*.mat                  : 구분용 단색 머티리얼
    /// 이미 있으면 덮어쓴다.
    /// </summary>
    public static class BossArenaSceneBuilder
    {
        private const string PrefabFolder = "Assets/03_Prefabs/Training";
        private const string SceneFolder = "Assets/01_Scenes/Training";
        private const string MaterialFolder = "Assets/08_Materials/Training";
        private const string ArenaPrefabPath = PrefabFolder + "/BossTrainingArena.prefab";
        private const string ScenePath = SceneFolder + "/BossTrainingScene.unity";

        // 아레나 한 칸 크기(바닥 한 변)와 병렬 학습용 격자 설정
        private const float ArenaSize = 22f;
        private const float WallHeight = 2.5f;
        private const int GridCount = 2;          // 2 x 2 = 4칸
        private const float GridSpacing = 30f;

        [MenuItem("Tools/보스 학습/아레나 씬 생성")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(PrefabFolder);
            EnsureFolder(SceneFolder);
            EnsureFolder(MaterialFolder);

            Material floorMat = GetMaterial("Training_Floor", new Color(0.32f, 0.34f, 0.36f));
            Material wallMat = GetMaterial("Training_Wall", new Color(0.18f, 0.18f, 0.2f));
            Material bossMat = GetMaterial("Training_Boss", new Color(0.85f, 0.15f, 0.12f));
            Material playerMat = GetMaterial("Training_Player", new Color(0.2f, 0.45f, 0.9f));
            Material markerMat = GetMaterial("Training_Marker", new Color(1f, 0.9f, 0.2f));

            BuildArenaPrefab(floorMat, wallMat, bossMat, playerMat, markerMat);
            BuildScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[보스 학습] 아레나 프리팹과 씬을 만들었습니다.\n{ArenaPrefabPath}\n{ScenePath}\n" +
                      "ML-Agents 패키지를 설치했다면 Tools/보스 학습/보스 에이전트 연결을 실행하세요.");
        }

        #region 프리팹

        private static void BuildArenaPrefab(Material floorMat, Material wallMat, Material bossMat, Material playerMat, Material markerMat)
        {
            var root = new GameObject("BossTrainingArena");
            try
            {
                float half = ArenaSize * 0.5f;

                CreateBox("Floor", root.transform, new Vector3(0f, -0.1f, 0f), new Vector3(ArenaSize, 0.2f, ArenaSize), floorMat, keepCollider: true);

                float wallThick = 0.6f;
                CreateBox("Wall_N", root.transform, new Vector3(0f, WallHeight * 0.5f, half), new Vector3(ArenaSize + wallThick * 2f, WallHeight, wallThick), wallMat, true);
                CreateBox("Wall_S", root.transform, new Vector3(0f, WallHeight * 0.5f, -half), new Vector3(ArenaSize + wallThick * 2f, WallHeight, wallThick), wallMat, true);
                CreateBox("Wall_E", root.transform, new Vector3(half, WallHeight * 0.5f, 0f), new Vector3(wallThick, WallHeight, ArenaSize), wallMat, true);
                CreateBox("Wall_W", root.transform, new Vector3(-half, WallHeight * 0.5f, 0f), new Vector3(wallThick, WallHeight, ArenaSize), wallMat, true);

                TrainingCombatant boss = CreateCombatant("Boss", root.transform, new Vector3(0f, 0f, -5f), 1.6f, 2.2f, bossMat, markerMat);
                TrainingCombatant player = CreateCombatant("SparringPlayer", root.transform, new Vector3(0f, 0f, 5f), 1.0f, 2.0f, playerMat, markerMat);

                var arena = root.AddComponent<BossTrainingArena>();
                var so = new SerializedObject(arena);
                so.FindProperty("boss").objectReferenceValue = boss;
                so.FindProperty("player").objectReferenceValue = player;
                so.FindProperty("halfExtents").vector2Value = new Vector2(half, half);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, ArenaPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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

        private static GameObject CreateBox(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        #endregion

        #region 씬

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
                    arena.name = $"Arena_{x}_{z}";
                    arena.transform.position = new Vector3(x * GridSpacing - offset, 0f, z * GridSpacing - offset);
                }
            }

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(55f, 30f, 0f);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.1f);
            camGo.AddComponent<AudioListener>();
            float height = GridCount * GridSpacing * 0.9f;
            camGo.transform.position = new Vector3(0f, height, -height * 0.35f);
            camGo.transform.rotation = Quaternion.Euler(70f, 0f, 0f);

            EditorSceneManager.SaveScene(scene, ScenePath);
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
