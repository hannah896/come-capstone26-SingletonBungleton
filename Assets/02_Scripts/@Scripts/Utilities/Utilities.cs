using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

public static class Utilities {
    
    #region Generals

    public static T GetOrAddComponent<T>(GameObject obj) where T : Component {
        if (!obj.TryGetComponent<T>(out T component)) component = obj.AddComponent<T>();
        return component;
    }

    public static T GetOrAddComponent<T>(this Component component) where T : Component
    {
        if (component == null) return null;
        return component.gameObject.GetOrAddComponent<T>();
    }
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
    
    public static void SetParent(this Transform transform, Transform parent, bool worldPositionStays = true)
    {
        if (transform == null) return;
        if (transform.parent == parent) return;
        transform.SetParent(parent, worldPositionStays);
    }
    
    public static void SetParent(this Component component, Transform parent, bool worldPositionStays = true)
    {
        if (component == null) return;
        if (component.transform == null) return;
        if (component.transform.parent == parent) return;
        component.transform.SetParent(parent, worldPositionStays);
    }

    public static void SetParent(this GameObject gameObject, Transform parent, bool worldPositionStays = true)
    {
        if (gameObject == null) return;
        if (gameObject.transform.parent == parent) return;
        gameObject.transform.SetParent(parent, worldPositionStays);
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

    public static T Instantiate<T>(this GameObject gameObject) where T : Component
    {
        GameObject obj = Object.Instantiate(gameObject);
        obj.name = obj.name.Replace("(Clone)", "");
        return obj.GetComponent<T>();
    }

    public static GameObject Instantiate(this GameObject gameObject)
    {
        GameObject obj = Object.Instantiate(gameObject);
        obj.name = obj.name.Replace("(Clone)", "");
        return obj;
    }
    
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

    public static T GetOrCreateObjectOfType<T>() where T : Component
    {
        T obj = Object.FindAnyObjectByType<T>();
        if (obj == null)
        {
            obj = new GameObject(typeof(T).Name).AddComponent<T>();
        }
        return obj;
    }

    public static T FindChild<T>(GameObject obj, string name = null) where T : Component {
        if (obj == null) return null;
        T[] components = obj.GetComponentsInChildren<T>(true);
        if (components.Length == 0) return null;
        if (string.IsNullOrEmpty(name)) return components[0];
        return components.Where(x => x.name == name).FirstOrDefault();
    }

    public static T FindChildDirect<T>(GameObject obj, string name = null) where T : Component {
        if (obj == null) return null;
        for (int i = 0; i < obj.transform.childCount; i++) {
            Transform t = obj.transform.GetChild(i);
            if (string.IsNullOrEmpty(name) || t.name == name)
                if (t.TryGetComponent(out T component)) return component;
        }
        return null;
    }

    public static GameObject FindChild(GameObject obj, string name = null) {
        Transform transform = FindChild<Transform>(obj, name);
        if (transform == null) return null;
        return transform.gameObject;
    }

    public static GameObject FindChildDirect(GameObject obj, string name = null) {
        Transform transform = FindChildDirect<Transform>(obj, name);
        if (transform == null) return null;
        return transform.gameObject;
    }

    public static void DestroyAllChildren(this Transform t) {
        for (int i = t.childCount - 1; i >= 0; i--) {
            Object.Destroy(t.GetChild(i).gameObject);
        }
    }
    
    #endregion

    #region Colors
    
    public static Color GetDisableColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    public static Color GetColor(this ColorType type) => type switch
    {
        ColorType.Black  => "#495349".GetHtmlColor(),
        ColorType.Red    => "#CE535C".GetHtmlColor(),
        ColorType.Lime   => "#8AF36D".GetHtmlColor(),
        ColorType.Yellow => "#F3BF20".GetHtmlColor(),
        ColorType.Orange => "#ED9433".GetHtmlColor(),
        ColorType.Purple => "#D68AFA".GetHtmlColor(),
        ColorType.Pink   => "#F772BE".GetHtmlColor(),
        ColorType.Sky    => "#50A6E1".GetHtmlColor(),
        _ => Color.black,
    };

    public static Color GetColor(this Difficulty difficulty) => difficulty switch
    {
        Difficulty.Normal   => "#AEEEFF".GetHtmlColor(),
        Difficulty.Hard     => "#CFA7E3".GetHtmlColor(),
        Difficulty.VeryHard => "#F1B0CB".GetHtmlColor(),
        _ => "#AEEEFF".GetHtmlColor(),
    };

    public static Color GetTextColor(this Difficulty difficulty) => difficulty switch
    {
        Difficulty.Normal   => "#1D5995".GetHtmlColor(),
        Difficulty.Hard     => "#BA6E79".GetHtmlColor(),
        Difficulty.VeryHard => "#C6B179".GetHtmlColor(),
        _ => "#1D5995".GetHtmlColor(),
    };

    public static Color GetHtmlColor(this string htmlString)
    {
        if (ColorUtility.TryParseHtmlString(htmlString, out var color)) return color;
        return Color.white;
    }

    #endregion
    
    #region Math

    public static byte RotateShift(this byte value, int count) {
        return (byte)((value << count) | (value >> (8 - count)));
    }

    public static string GetFormattedCurrency(this int value) {
        if (value < 1000) return value.ToString();

        if (value < 1000000) {
            double v = (double)value / 1000;
            if (v >= 100) return $"{Math.Floor(v)}K";
            return v >= 10 ? $"{Math.Floor(v * 10) / 10:0.0}K" : $"{Math.Floor(v * 100) / 100:0.00}K";
        }

        if (value < 1000000000) {
            double v = (double)value / 1000000;
            if (v >= 100) return $"{Math.Floor(v)}M";
            return v >= 10 ? $"{Math.Floor(v * 10) / 10:0.0}M" : $"{Math.Floor(v * 100) / 100:0.00}M";
        }

        return string.Empty;
    }

    #endregion

    #region Vector
    
    public static Vector3 SetX(this Vector3 vector, float x) {
        return new(x, vector.y, vector.z);
    }

    public static Vector3 SetY(this Vector3 vector, float y) {
        return new(vector.x, y, vector.z);
    }

    public static Vector3 SetZ(this Vector3 vector, float z) {
        return new(vector.x, vector.y, z);
    }

    public static Vector2 GetCenter(this List<Vector2> vectors) {
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        foreach (Vector2 v in vectors) {
            if (v.x < minX) minX = v.x;
            if (v.x > maxX) maxX = v.x;
            if (v.y < minY) minY = v.y;
            if (v.y > maxY) maxY = v.y;
        }

        return new((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
    }

    public static Vector3 GetCenter(this List<Vector3> vectors) {
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;
        foreach (Vector3 v in vectors) {
            if (v.x < minX) minX = v.x;
            if (v.x > maxX) maxX = v.x;
            if (v.y < minY) minY = v.y;
            if (v.y > maxY) maxY = v.y;
            if (v.z < minZ) minZ = v.z;
            if (v.z > maxZ) maxZ = v.z;
        }

        return new((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f);
    }

    // 좌표 리스트를 원점(최조 좌표)을 기준으로 정규화.
    public static void Normalize(this List<Vector2Int> points) {
        int minX = points.Min(p => p.x);
        int minY = points.Min(p => p.y);
        for (int i = 0; i < points.Count; i++) {
            points[i] = new(points[i].x - minX, points[i].y - minY);
        }
    }

    #endregion

    #region Direction

    public static Direction GetDirection(this Vector3 from, Vector3 to) {
        const float e = 0.1f;
        
        float dx = to.x - from.x;
        float dy = to.y - from.y;

        if (Mathf.Abs(dx) >= Mathf.Abs(dy)) {
            if (dx > e) return Direction.Right;
            if (dx < -e) return Direction.Left;
        }
        else {
            if (dy > e) return Direction.Top;
            if (dy < -e) return Direction.Bottom;
        }

        return Direction.None;
    }

    public static Direction GetDirection(this Vector2Int from, Vector2Int to) => GetDirection((Vector2)from, (Vector2)to);

    public static Vector2Int GetIndex(this Direction direction) {
        return direction switch {
            Direction.Top => new(0, 1),
            Direction.Right => new(1, 0),
            Direction.Bottom => new(0, -1),
            Direction.Left => new(-1, 0),
            _ => Vector2Int.zero
        };
    }

    public static Vector2Int GetIndex(this Direction direction, Direction additionalDirection) {
        return direction.GetIndex() + additionalDirection.GetIndex();
    }
    
    public static Vector3 GetRotation(this Direction direction) {
        return direction switch {
            Direction.Top => new(0, 180, 0),
            Direction.Right => new(0, 270, 0),
            Direction.Bottom => new(0, 0, 0),
            Direction.Left => new(0, 90, 0),
            _ => Vector3.zero
        };
    }

    public static Direction GetOpposite(this Direction direction) {
        return direction switch {
            Direction.Top => Direction.Bottom,
            Direction.Right => Direction.Left,
            Direction.Bottom => Direction.Top,
            Direction.Left => Direction.Right,
            _ => Direction.None
        };
    }

    public static Direction GetCounterClockwise(this Direction direction) {
        return direction switch {
            Direction.Top => Direction.Left,
            Direction.Left => Direction.Bottom,
            Direction.Bottom => Direction.Right,
            Direction.Right => Direction.Top,
            _ => Direction.None
        };
    }

    public static Direction GetClockwise(this Direction direction) {
        return direction switch {
            Direction.Top => Direction.Right,
            Direction.Right => Direction.Bottom,
            Direction.Bottom => Direction.Left,
            Direction.Left => Direction.Top,
            _ => Direction.None
        };
    }

    public static Orientation Flip(this Orientation orientation) {
        return orientation switch {
            Orientation.Horizontal => Orientation.Vertical,
            Orientation.Vertical => Orientation.Horizontal,
            _ => Orientation.None
        };
    }

    public static Vector2Int DirectionVector(this Direction direction) {
        return direction switch {
            Direction.Top => new(0, 1),
            Direction.Bottom => new(0, -1),
            Direction.Left => new(-1, 0),
            Direction.Right => new(1, 0),
            _ => Vector2Int.zero
        };
    }
    
    public static Vector2Int WallDirectionVector(this Direction direction) {
        return direction switch {
            Direction.Top => new(-1, 0),
            Direction.Bottom => new(1, 0),
            Direction.Left => new(0, -1),
            Direction.Right => new(0, 1),
            _ => Vector2Int.zero
        };
    }

    #endregion
}

public class Vector2IntDictionaryConverter : JsonConverter<Dictionary<Vector2Int, int>> {
    public override Dictionary<Vector2Int, int> ReadJson(JsonReader reader, Type objectType,
        Dictionary<Vector2Int, int> existingValue, bool hasExistingValue, JsonSerializer serializer) {
        
        JObject jObject = JObject.Load(reader);
        Dictionary<Vector2Int, int> dictionary = new(jObject.Count);

        foreach (JProperty prop in jObject.Properties()) {
            string s = prop.Name.Trim('(', ')');
            string[] parts = s.Split(',');
            if (parts.Length != 2) throw new JsonSerializationException($"Invalid Vector2Int key format: {prop.Name}");

            if (!int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y))
                throw new JsonSerializationException($"Cannot parse Vector2Int components from \"{prop.Name}\"");

            int value = prop.Value.ToObject<int>();
            dictionary.Add(new Vector2Int(x, y), value);
        }

        return dictionary;
    }

    public override void WriteJson(JsonWriter writer, Dictionary<Vector2Int, int> value, JsonSerializer serializer) {
        writer.WriteStartObject();
        foreach (KeyValuePair<Vector2Int, int> kv in value) {
            writer.WritePropertyName($"({kv.Key.x}, {kv.Key.y})");
            writer.WriteValue(kv.Value);
        }

        writer.WriteEndObject();
    }
}