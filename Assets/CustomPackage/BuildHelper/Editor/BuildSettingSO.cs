#if UNITY_EDITOR

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

#if UNITY_ANDROID && ENABLE_GPGS
using GooglePlayGames.Editor;

#endif

namespace Project.Scripts.Build.Editor

{
    [CreateAssetMenu(fileName = "BuildSettings", menuName = "Build/BuildSettings")]
    public class BuildSettingsSO : ScriptableObject

    {
        [Header("Build Settings")]

#if UNITY_ANDROID
        public string buildFileName = "[Enter Build Name]";
#endif

        public string companyName = "ActionFit";

        public string productName = "[Enter App Name]";

        [Header("Build Paths")]
        public bool saveFileInProject = false;

#if UNITY_ANDROID
        public string androidBuildPath = "Assets/../build/android";

#elif UNITY_IOS
        public string iosBuildPath = "Assets/../build/ios/";

#endif

        [Header("Package Names")]

#if UNITY_ANDROID
        public string androidPackageName = "[Enter PackageName]";
#elif UNITY_IOS
        public string iosPackageName = "com.actionfit.catmerge.ios";

#endif

        [Header("Version Info")]
        public string buildVersion = "0.0.1";

        public string bundleNo = "1";

#if UNITY_ANDROID
        [Header("KeyStore Info")]
        public bool autoSearchKeystore = true;

        public string keyStorePath = "Assets/../application.keystore";

        public const string KeyStorePassword = "ActFit0304!";

        public bool autoSearchAlias = true;

        public string keyStoreAlias = "[Enter Alias]";

        public const string AliasPassword = "ActFit0304!";

// GPGS를 세팅할 경우 적는 부분. GPGS App_Id값 초기화 예방(미구현)

        [Header("Gpgs Info")]
        public bool settingGpgs = false;

        public const string SaveConstants = "Assets";

        public const string ConstantsName = "GPGSIds";

        public string resourcesDefinition;

        public string clientID;

#endif

        [Header("Build Options")]
        public bool isDevMode = false;

        public List<string> defineSymbol = new();

#if UNITY_IOS
        [Header("Add Frameworks")]
        public List<string> addFrameworks = new();

#endif

        public Dictionary<string, string> buildSetting = new();
    }

    public class BuildSettingsWindow : EditorWindow

    {
        [SerializeField]
        private BuildSettingsSO settings;

        public BuildSettingsSO Settings => settings;

        private Vector2 _scrollPosition = Vector2.zero; // Initialize with Vector2.zero

        private SerializedObject _serializedSettings;

        [MenuItem("Build/Build Settings")]
        public static void ShowWindow()

        {
            BuildSettingsWindow window = GetWindow<BuildSettingsWindow>("Build Settings");
            window.Show();
        }

        private void OnEnable()

        {
// Initialize scroll position on enable
            _scrollPosition = Vector2.zero;

// Try to load last used settings
            string lastUsedPath = EditorPrefs.GetString("LastUsedBuildSettings", "");
            if (!string.IsNullOrEmpty(lastUsedPath))
            {
                settings = AssetDatabase.LoadAssetAtPath<BuildSettingsSO>(lastUsedPath);
                if (settings != null)
                {
                    _serializedSettings = new SerializedObject(settings);
                }
            }
        }

        private void OnGUI()

        {
            EditorGUILayout.Space(10);

// BuildSettings SO 필드를 최상단에 배치
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Build Settings Asset");
            EditorGUI.BeginChangeCheck();
            settings = (BuildSettingsSO)EditorGUILayout.ObjectField(
                settings,
                typeof(BuildSettingsSO),
                false
            );
            if (EditorGUI.EndChangeCheck() && settings != null)
            {
                _serializedSettings = new SerializedObject(settings);
                EditorPrefs.SetString("LastUsedBuildSettings", AssetDatabase.GetAssetPath(settings));
            }

// Create New SO 버튼 추가
            if (GUILayout.Button("Create New", GUILayout.Width(80)))
            {
                string path = EditorUtility.SaveFilePanelInProject(
                    "Create Build Settings",
                    "BuildSettings",
                    "asset",
                    "Please enter a file name to save the build settings to"
                );
                if (!string.IsNullOrEmpty(path))
                {
                    var newSettings = CreateInstance<BuildSettingsSO>();
                    AssetDatabase.CreateAsset(newSettings, path);
                    AssetDatabase.SaveAssets();
                    settings = newSettings;
                    _serializedSettings = new SerializedObject(settings);
                    EditorPrefs.SetString("LastUsedBuildSettings", path);
                }
            }
            EditorGUILayout.EndHorizontal();
            if (settings == null)
            {
                EditorGUILayout.HelpBox("Please assign or create a Build Settings asset to configure build options.",
                    MessageType.Warning);

// Try to load last used settings
                string lastUsedPath = EditorPrefs.GetString("LastUsedBuildSettings", "");
                if (!string.IsNullOrEmpty(lastUsedPath))
                {
                    settings = AssetDatabase.LoadAssetAtPath<BuildSettingsSO>(lastUsedPath);
                    if (settings != null)
                    {
                        _serializedSettings = new SerializedObject(settings);
                    }
                }
                return;
            }
            _serializedSettings?.Update();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            EditorGUILayout.Space(10);
#if UNITY_ANDROID
            EditorGUILayout.PropertyField(_serializedSettings?.FindProperty("buildFileName"));
#endif
            EditorGUILayout.PropertyField(_serializedSettings?.FindProperty("companyName"));
            EditorGUILayout.PropertyField(_serializedSettings?.FindProperty("productName"));
            DrawPathSettings();
            DrawPackageSettings();
            DrawVersionSettings();
#if UNITY_ANDROID
            DrawGpgsSetting();
            DrawKeyStoreSettings();
#endif
            DrawBuildOptions();
            DrawBuildButtons();
            EditorGUILayout.EndScrollView();
            if (_serializedSettings?.hasModifiedProperties ?? false)
            {
                _serializedSettings.ApplyModifiedProperties();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }

        private void DrawPathSettings()

        {
#if UNITY_ANDROID
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("saveFileInProject"));
            bool saveFileInProject = _serializedSettings.FindProperty("saveFileInProject").boolValue;
            if (!saveFileInProject)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(_serializedSettings.FindProperty("androidBuildPath"));
                if (GUILayout.Button("Browse", GUILayout.Width(60)))
                {
                    string path =
                        EditorUtility.OpenFolderPanel("Choose Android Build Folder", Settings.androidBuildPath, "");
                    if (!string.IsNullOrEmpty(path)) _serializedSettings.FindProperty("androidBuildPath").stringValue = path;
                }
                EditorGUILayout.EndHorizontal();
            }
#elif UNITY_IOS
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("iosBuildPath"));
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Choose iOS Build Folder", settings.iosBuildPath, "");
                if (!string.IsNullOrEmpty(path)) _serializedSettings.FindProperty("iosBuildPath").stringValue = path;
            }
            EditorGUILayout.EndHorizontal();
#endif
        }

        private void DrawPackageSettings()
        {
#if UNITY_ANDROID
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("androidPackageName"));
#elif UNITY_IOS
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("iosPackageName"));
#endif
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Firebase Sync", EditorStyles.label);

            // 버튼 클릭 시 현재 타겟 플랫폼에 맞는 파일에서 가져오기
            if (GUILayout.Button("Sync From Firebase", GUILayout.Width(150)))
            {
                string detectedId = null;
#if UNITY_ANDROID
                detectedId = AOSBuildProcess.GetPackageNameFromFirebaseJson(); // 이전에 만든 JSON 파싱 함수
#elif UNITY_IOS
                // detectedId = FBFrameworkPostProcess.GetBundleIdFromFirebasePlist(); // 새로 만든 Plist 파싱 함수
#endif
                if (!string.IsNullOrEmpty(detectedId))
                {
#if UNITY_ANDROID
                    _serializedSettings.FindProperty("androidPackageName").stringValue = detectedId;
#elif UNITY_IOS
                    _serializedSettings.FindProperty("iosPackageName").stringValue = detectedId;
#endif
                    UnityEngine.Debug.Log($"<color=cyan><b>[Firebase Sync]</b></color> ID updated: {detectedId}");
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawVersionSettings()

        {
            GUI.enabled = true;
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("buildVersion"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("bundleNo"));
        }

#if UNITY_ANDROID
        private void DrawGpgsSetting()

        {
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("settingGpgs"),
                new GUIContent("Enable GPGS Setup on Build"));
            if (settings.settingGpgs)
            {
                GUI.enabled = false;
                EditorGUILayout.TextField("Save Constants", BuildSettingsSO.SaveConstants);
                GUI.enabled = false;
                EditorGUILayout.TextField("Constants Name", BuildSettingsSO.ConstantsName);

// Resources Definition 입력창 (여러 줄 입력 가능하게)
                GUI.enabled = true;
                EditorGUILayout.LabelField("Resources Definition");
                settings.resourcesDefinition =
                    EditorGUILayout.TextArea(settings.resourcesDefinition, GUILayout.Height(100));

// Client ID 입력창
                EditorGUILayout.PropertyField(_serializedSettings.FindProperty("clientID"),
                    new GUIContent("Client ID"));
            }
        }

#endif

#if UNITY_ANDROID
        private void DrawKeyStoreSettings()

        {
// KeyStore Setting
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("autoSearchKeystore"),
                new GUIContent("Auto Search Keystore"));
            bool isAutoKeystore = _serializedSettings.FindProperty("autoSearchKeystore").boolValue;

// 자동 결로 설정을 할 경우 경로 GUI 숨김
            if (!isAutoKeystore)
            {
                EditorGUILayout.BeginHorizontal();
                GUI.enabled = true;
                EditorGUILayout.PropertyField(_serializedSettings.FindProperty("keyStorePath"));
                if (GUILayout.Button("Browse", GUILayout.Width(60)))
                {
                    string path =
                        EditorUtility.OpenFilePanel("Choose KeyStore File", Settings.keyStorePath, "keystore");
                    if (!string.IsNullOrEmpty(path))
                    {
                        _serializedSettings.FindProperty("keyStorePath").stringValue = path;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            GUI.enabled = false;
            EditorGUILayout.TextField("KeyStore Password", BuildSettingsSO.KeyStorePassword);

// Alias Setting
            EditorGUILayout.Space(10);
            GUI.enabled = true;
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("autoSearchAlias"),
                new GUIContent("Auto Search Alias"));
            bool isAutoAlias = _serializedSettings.FindProperty("autoSearchAlias").boolValue;

// 자동 설정을 할 경우 string GUI 숨김
            if (!isAutoAlias)
            {
                GUI.enabled = true;
                EditorGUILayout.PropertyField(_serializedSettings.FindProperty("keyStoreAlias"));
            }
            GUI.enabled = false;
            EditorGUILayout.TextField("KeyStore Alias Password", BuildSettingsSO.AliasPassword);
            GUI.enabled = true;
        }

#endif

        private void DrawBuildOptions()

        {
            EditorGUILayout.Space(20);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("isDevMode"));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("defineSymbol"));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBuildButtons()

        {
            EditorGUILayout.Space(20);
            if (settings == null)
            {
                EditorGUILayout.HelpBox("Assign Build Settings asset to enable build options.", MessageType.Warning);
                return;
            }
            EditorGUILayout.Space(20);
#if UNITY_ANDROID
            if (GUILayout.Button("Android APK Build"))
            {
                SwitchPlatformAndBuild(BuildTarget.Android,
                    () =>
                    {
                        AOSBuildProcess.AndroidBuildApk(settings);
                    });
            }
            if (GUILayout.Button("Android APK Build And Run"))
            {
                SwitchPlatformAndBuild(BuildTarget.Android,
                    () =>
                    {
                        AOSBuildProcess.AndroidBuildApkRun(Settings);
                    });
            }
            if (GUILayout.Button("Android AAB Build"))
            {
                SwitchPlatformAndBuild(BuildTarget.Android,
                    () =>
                    {
                        AOSBuildProcess.AndroidBuildAab(Settings);
                    });
            }
            if (GUILayout.Button("Android AAB Build And Run"))
            {
                SwitchPlatformAndBuild(BuildTarget.Android,
                    () =>
                    {
                        AOSBuildProcess.AndroidBuildAabRun(Settings);
                    });
            }
#elif UNITY_IOS
            if (GUILayout.Button("iOS Build Append"))
            {
                SwitchPlatformAndBuild(BuildTarget.iOS,
                    () =>
                    {
                        // IOSBuildProcess.IOSBuildAppend(settings);
                    });
            }
            if (GUILayout.Button("iOS Build Replace"))
            {
                SwitchPlatformAndBuild(BuildTarget.iOS,
                    () =>
                    {
                        // IOSBuildProcess.IOSBuildReplace(settings);
                    });
            }
#endif
        }

        private void SwitchPlatformAndBuild(BuildTarget target, System.Action buildAction)

        {
            if (EditorUserBuildSettings.activeBuildTarget == target)
            {
                buildAction?.Invoke();
                return;
            }
            if (!EditorUtility.DisplayDialog("Platform Switch",
                $"Current platform is not {target}. Do you want to switch?",
                "Yes",
                "No"))
                return;
            BuildTargetGroup targetGroup =
                target == BuildTarget.Android ? BuildTargetGroup.Android : BuildTargetGroup.iOS;
            if (EditorUserBuildSettings.SwitchActiveBuildTarget(targetGroup, target))
            {
                System.Threading.Thread.Sleep(1000);
                AssetDatabase.Refresh();
                buildAction?.Invoke();
            }
            else
            {
                UnityEngine.Debug.LogError($"Failed to switch to {target} platform");
            }
        }
    }
}

#endif