#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class WorldGenEditorTool : EditorWindow
{
    private const string WindowMenuPath = "Tools/World Gen/Test World Generator";
    private const string GenerateMenuPath = "Tools/World Gen/Generate Test World";
    private const double ReadyTimeoutSeconds = 10.0;

    private static readonly BindingFlags PrivateInstanceFlags =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private static WorldGenManager pendingManager;
    private static WorldBranchSetting pendingBranch = WorldBranchSetting.Default;
    private static WorldLoopSetting pendingLoop = WorldLoopSetting.Default;
    private static double waitStartedAt;

    private WorldBranchSetting branch = WorldBranchSetting.Default;
    private WorldLoopSetting loop = WorldLoopSetting.Default;

    [MenuItem(WindowMenuPath)]
    private static void OpenWindow()
    {
        GetWindow<WorldGenEditorTool>("World Gen");
    }

    [MenuItem(GenerateMenuPath)]
    private static void GenerateDefaultWorld()
    {
        RequestGenerate(WorldBranchSetting.Default, WorldLoopSetting.Default);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("World Generation Test Tool", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Ensures a WorldGenManager exists in Play Mode, then calls the existing GenerateWorldFromUI entry point.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("World generation can only run in Play Mode.", MessageType.Warning);
            }
        }

        branch = (WorldBranchSetting)EditorGUILayout.EnumPopup("Branch", branch);
        loop = (WorldLoopSetting)EditorGUILayout.EnumPopup("Loop", loop);

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Generate World"))
            {
                RequestGenerate(branch, loop);
            }
        }
    }

    private static void RequestGenerate(WorldBranchSetting branchSetting, WorldLoopSetting loopSetting)
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[WorldGenEditorTool] World generation can only run in Play Mode.");
            return;
        }

        pendingManager = EnsureWorldGenManager();
        pendingBranch = branchSetting;
        pendingLoop = loopSetting;
        waitStartedAt = EditorApplication.timeSinceStartup;

        EditorApplication.update -= TryGenerateWhenReady;
        EditorApplication.update += TryGenerateWhenReady;
        TryGenerateWhenReady();
    }

    private static WorldGenManager EnsureWorldGenManager()
    {
        WorldGenManager manager = WorldGenManager.Instance;
        if (manager != null)
        {
            EnsureManagerIsActive(manager);
            return manager;
        }

        manager = Object.FindFirstObjectByType<WorldGenManager>(FindObjectsInactive.Include);
        if (manager != null)
        {
            EnsureManagerIsActive(manager);
            return manager;
        }

        GameObject managerObject = new GameObject(nameof(WorldGenManager));
        manager = managerObject.AddComponent<WorldGenManager>();
        Selection.activeGameObject = managerObject;

        Debug.Log("[WorldGenEditorTool] Created a missing WorldGenManager.");
        return manager;
    }

    private static void EnsureManagerIsActive(WorldGenManager manager)
    {
        if (!manager.gameObject.activeSelf)
        {
            manager.gameObject.SetActive(true);
        }

        if (!manager.enabled)
        {
            manager.enabled = true;
        }
    }

    private static void TryGenerateWhenReady()
    {
        if (!EditorApplication.isPlaying)
        {
            StopWaiting();
            return;
        }

        if (pendingManager == null)
        {
            pendingManager = EnsureWorldGenManager();
        }

        if (IsWorldGenManagerReady(pendingManager))
        {
            WorldGenManager manager = pendingManager;
            WorldBranchSetting branchSetting = pendingBranch;
            WorldLoopSetting loopSetting = pendingLoop;

            StopWaiting();
            _ = manager.GenerateWorldFromUI(branchSetting, loopSetting);

            Debug.Log($"[WorldGenEditorTool] Requested world generation. Branch: {branchSetting}, Loop: {loopSetting}");
            return;
        }

        double elapsed = EditorApplication.timeSinceStartup - waitStartedAt;
        if (elapsed >= ReadyTimeoutSeconds)
        {
            StopWaiting();
            Debug.LogWarning("[WorldGenEditorTool] Timed out while waiting for WorldGenManager readiness. Check Main initialization or WorldSettings loading.");
        }
    }

    private static void StopWaiting()
    {
        EditorApplication.update -= TryGenerateWhenReady;
        pendingManager = null;
    }

    private static bool IsWorldGenManagerReady(WorldGenManager manager)
    {
        if (manager == null) return false;

        FieldInfo loadedField = typeof(WorldGenManager).GetField("_isWorldSettingsLoaded", PrivateInstanceFlags);
        FieldInfo settingsField = typeof(WorldGenManager).GetField("_worldSettings", PrivateInstanceFlags);

        bool isLoaded = loadedField != null
            && loadedField.GetValue(manager) is bool loaded
            && loaded;

        bool hasSettings = settingsField != null
            && settingsField.GetValue(manager) != null;

        return isLoaded && hasSettings;
    }
}
#endif
