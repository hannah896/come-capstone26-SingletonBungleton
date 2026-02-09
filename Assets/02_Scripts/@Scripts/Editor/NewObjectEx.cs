using UnityEngine;
using UnityEditor;
using System;
using Object = UnityEngine.Object;

public class NewObjectEx {

    #region Properties

    private const string Name = "UIBase";

    #endregion

    #region Generals
    
    
    private static Prefabs FindPrefabs()
    {
        var guids = AssetDatabase.FindAssets("t:Prefabs");
        if (guids.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<Prefabs>(path);
        }
        return null;
    }

    private static void Instantiate(Func<Prefabs, GameObject> selector) {
        if (FindPrefabs() == null) {
            Debug.LogWarning($"Prefabs not found at path {Name}");
            return;
        }

        Object instance = PrefabUtility.InstantiatePrefab(selector(FindPrefabs()), Selection.activeTransform);

        Undo.RegisterCreatedObjectUndo(instance, $"Create {instance.name}");
        Selection.activeObject = instance;
    }

    #endregion

    #region Editor Menu

    [MenuItem("GameObject/>>>UI/Base/Image", false, 1)]
    private static void CreateNewImage() => Instantiate(findPrefabs => findPrefabs.Image);


    [MenuItem("GameObject/>>>UI/Base/Text", false, 1)]
    private static void CreateNewText() => Instantiate(findPrefabs => findPrefabs.Text);


    [MenuItem("GameObject/>>>UI/Base/Button", false, 1)]
    private static void CreateNewButton() => Instantiate(findPrefabs => findPrefabs.Button);
    
    
    [MenuItem("GameObject/>>>UI/Base/Toggle", false, 1)]
    private static void CreateNewToggle() => Instantiate(findPrefabs => findPrefabs.Toggle);

    #endregion

    #region Check Validate

    [MenuItem("GameObject/>>>UI/Base/Image", true)]
    [MenuItem("GameObject/>>>UI/Base/Text", true)]
    [MenuItem("GameObject/>>>UI/Base/Button", true)]
    [MenuItem("GameObject/>>>UI/Base/Toggle", true)]
    private static bool CreateNewUIComponentValidate() => Selection.activeGameObject && Selection.activeGameObject.GetComponentInParent<Canvas>();

    #endregion

}