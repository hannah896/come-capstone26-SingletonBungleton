using Cysharp.Threading.Tasks;
using UnityEngine;

public static class Utility
{
    #region Components
    public static T FindComponent<T>(this GameObject gameObject, string name) where T : Component
    {
        if (gameObject == null) return null;
        var components = gameObject.GetComponentsInChildren<T>(true);
        foreach (var component in components)
        {
            if (component.name.Equals(name)) return component;
        }
        return null;
    }

    public static T GetOrCreateObjectOfType<T>() where T : Component
    {
        T obj = Object.FindAnyObjectByType<T>();
        if (obj == null)
        {
            obj = new GameObject(typeof(T).Name).AddComponent<T>();
        }
        return obj;
    }

    public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
    {
        if (gameObject == null) return null;
        if (gameObject.TryGetComponent<T>(out var component)) return component;
        return gameObject.AddComponent<T>();
    }

    public static T GetOrAddComponent<T>(this Component component) where T : Component
    {
        if (component == null) return null;
        return component.gameObject.GetOrAddComponent<T>();
    }
    #endregion

    #region Transform & Active
    public static void SetActive(this GameObject gameObject, bool isActive)
    {
        if (gameObject == null) return;
        if (gameObject.activeSelf != isActive)
            gameObject.SetActive(isActive);
    }

    public static void SetActive(this Component component, bool isActive)
    {
        if (component == null || component.gameObject == null) return;
        component.gameObject.SetActive(isActive);
    }

    public static void SetParent(this Component component, Transform parent, bool worldPositionStays = true)
    {
        if (component != null && component.transform != null)
            component.transform.SetParent(parent, worldPositionStays);
    }

    public static void ResetLocalTransform(this Transform transform)
    {
        if (transform == null) return;
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    public static void ResetTransform(this Transform transform)
    {
        if (transform == null) return;
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }
    #endregion

    #region Position Helpers
    public static void SetPositionX(this Transform transform, float x)
    {
        if (transform == null) return;
        Vector3 pos = transform.position;
        pos.x = x;
        transform.position = pos;
    }

    public static void SetPositionY(this Transform transform, float y)
    {
        if (transform == null) return;
        Vector3 pos = transform.position;
        pos.y = y;
        transform.position = pos;
    }

    public static void SetPositionZ(this Transform transform, float z)
    {
        if (transform == null) return;
        Vector3 pos = transform.position;
        pos.z = z;
        transform.position = pos;
    }
    #endregion

    #region Layers
    public static void SetLayerRecursively(this GameObject gameObject, int layer)
    {
        if (gameObject == null) return;
        gameObject.layer = layer;
        foreach (Transform child in gameObject.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    public static void SetLayerRecursively(this GameObject gameObject, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer == -1)
        {
            Debug.LogError($"Layer '{layerName}' not found.");
            return;
        }
        SetLayerRecursively(gameObject, layer);
    }
    #endregion
}