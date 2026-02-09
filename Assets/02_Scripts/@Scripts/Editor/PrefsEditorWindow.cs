using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using Blossom.Preference;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prefs 데이터를 에디터에서 확인하고 수정할 수 있는 EditorWindow.
/// PrefData를 상속한 모든 타입을 자동으로 수집합니다.
/// </summary>
public class PrefsEditorWindow : EditorWindow
{
    private Vector2 _scrollPosition;
    private Dictionary<string, bool> _foldouts = new();
    private Dictionary<string, object> _dataCache = new();
    private Dictionary<string, bool> _dirtyFlags = new();

    // PrefEncrypt와 동일한 암호화 키 사용
    private static readonly string EncryptKey = "m71a12x28p94r6e5";
    private static readonly byte[] KeyBytes = Encoding.UTF8.GetBytes(EncryptKey);

    // PrefData를 상속한 모든 타입을 자동 수집
    private string[] _prefKeys;

    [MenuItem("Tools/Prefs Editor")]
    public static void ShowWindow()
    {
        var window = GetWindow<PrefsEditorWindow>("Prefs Editor");
        window.minSize = new Vector2(450, 400);
    }

    private void OnEnable()
    {
        CollectPrefKeys();
        LoadAllData();
    }

    // PrefData를 상속한 모든 구체 클래스 타입명을 수집
    private void CollectPrefKeys()
    {
        _prefKeys = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.FullName.StartsWith("Unity") &&
                        !a.FullName.StartsWith("System") &&
                        !a.FullName.StartsWith("mscorlib") &&
                        !a.FullName.StartsWith("netstandard"))
            .SelectMany(a => {
                try { return a.GetTypes(); }
                catch { return Array.Empty<Type>(); }
            })
            .Where(t => !t.IsAbstract &&
                        t != typeof(PrefData) &&
                        typeof(PrefData).IsAssignableFrom(t))
            .Select(t => t.Name)
            .ToArray();
    }

    private void OnGUI()
    {
        DrawToolbar();

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        if (_prefKeys == null || _prefKeys.Length == 0)
        {
            EditorGUILayout.HelpBox("PrefData를 상속한 클래스를 찾을 수 없습니다.", MessageType.Warning);
        }
        else
        {
            foreach (string key in _prefKeys)
            {
                DrawPrefSection(key);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            CollectPrefKeys();
            LoadAllData();
        }

        if (GUILayout.Button("Save All", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            SaveAllData();
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Open Folder", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }

        GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
        if (GUILayout.Button("Delete All", EditorStyles.toolbarButton, GUILayout.Width(70)))
        {
            if (EditorUtility.DisplayDialog("Delete All Prefs",
                    "Are you sure you want to delete all prefs data?\nThis cannot be undone.", "Delete", "Cancel"))
            {
                DeleteAllPrefs();
                LoadAllData();
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        // Path info
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Path:", GUILayout.Width(35));
        EditorGUILayout.SelectableLabel(Application.persistentDataPath, EditorStyles.miniLabel, GUILayout.Height(16));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawPrefSection(string key)
    {
        if (!_foldouts.ContainsKey(key)) _foldouts[key] = false;
        if (!_dirtyFlags.ContainsKey(key)) _dirtyFlags[key] = false;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();

        // Dirty indicator
        if (_dirtyFlags[key])
        {
            GUI.color = Color.yellow;
            GUILayout.Label("*", EditorStyles.boldLabel, GUILayout.Width(10));
            GUI.color = Color.white;
        }

        _foldouts[key] = EditorGUILayout.Foldout(_foldouts[key], key, true, EditorStyles.foldoutHeader);

        GUILayout.FlexibleSpace();

        // File exists indicator
        string filePath = GetPrefsFilePath(key);
        bool fileExists = File.Exists(filePath);
        GUI.color = fileExists ? Color.green : Color.gray;
        GUILayout.Label(fileExists ? "[File]" : "[No File]", EditorStyles.miniLabel);
        GUI.color = Color.white;

        // Save button
        GUI.enabled = _dirtyFlags[key];
        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
        if (GUILayout.Button("Save", EditorStyles.miniButton, GUILayout.Width(45)))
        {
            SaveData(key);
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        // Clear button
        GUI.backgroundColor = new Color(1f, 0.85f, 0.7f);
        if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(45)))
        {
            if (EditorUtility.DisplayDialog($"Clear {key}",
                    $"Reset {key} to default values?", "Clear", "Cancel"))
            {
                ClearData(key);
            }
        }
        GUI.backgroundColor = Color.white;

        // Delete button
        GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
        if (GUILayout.Button("Delete", EditorStyles.miniButton, GUILayout.Width(50)))
        {
            if (EditorUtility.DisplayDialog($"Delete {key}",
                    $"Delete {key} file?", "Delete", "Cancel"))
            {
                DeletePrefsFile(key);
                LoadData(key);
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        if (_foldouts[key])
        {
            EditorGUI.indentLevel++;
            DrawPrefFields(key);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    private void DrawPrefFields(string key)
    {
        if (!_dataCache.TryGetValue(key, out object data) || data == null)
        {
            EditorGUILayout.LabelField("No data (will create on save)", EditorStyles.miniLabel);

            if (GUILayout.Button("Create Default", GUILayout.Width(100)))
            {
                CreateDefaultData(key);
            }
            return;
        }

        Type type = data.GetType();
        var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

        foreach (var field in fields)
        {
            // Skip non-serialized fields
            if (field.GetCustomAttribute<NonSerializedAttribute>() != null) continue;

            DrawField(key, data, field);
        }
    }

    private void DrawField(string key, object data, FieldInfo field)
    {
        Type fieldType = field.FieldType;
        object value = field.GetValue(data);
        string fieldName = field.Name.TrimStart('_');

        EditorGUILayout.BeginHorizontal();

        // Field name with type hint
        string typeHint = GetTypeHint(fieldType);
        EditorGUILayout.LabelField($"{fieldName} ({typeHint})", GUILayout.Width(180));

        // Handle PrefValue<T>
        if (fieldType.IsGenericType && fieldType.Name.StartsWith("PrefValue"))
        {
            Type valueType = fieldType.GetGenericArguments()[0];
            FieldInfo valueField = fieldType.GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance);

            if (valueField != null && value != null)
            {
                object currentValue = valueField.GetValue(value);
                object newValue = DrawValueField(currentValue, valueType);

                if (!Equals(newValue, currentValue))
                {
                    valueField.SetValue(value, newValue);
                    _dirtyFlags[key] = true;
                }
            }
            else
            {
                EditorGUILayout.LabelField("null");
            }
        }
        // Handle simple types
        else if (IsEditableType(fieldType))
        {
            object newValue = DrawValueField(value, fieldType);

            if (!Equals(newValue, value))
            {
                field.SetValue(data, newValue);
                _dirtyFlags[key] = true;
            }
        }
        // Handle lists/collections (read-only display)
        else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
        {
            var list = value as System.Collections.IList;
            EditorGUILayout.LabelField($"[List] Count: {list?.Count ?? 0}");
        }
        else
        {
            EditorGUILayout.LabelField(value?.ToString() ?? "null");
        }

        EditorGUILayout.EndHorizontal();
    }

    private string GetTypeHint(Type type)
    {
        if (type.IsGenericType && type.Name.StartsWith("PrefValue"))
        {
            Type inner = type.GetGenericArguments()[0];
            return $"PrefValue<{inner.Name}>";
        }
        return type.Name;
    }

    private object DrawValueField(object value, Type type)
    {
        if (type == typeof(int))
            return EditorGUILayout.IntField(value != null ? (int)value : 0);

        if (type == typeof(long))
            return EditorGUILayout.LongField(value != null ? (long)value : 0L);

        if (type == typeof(float))
            return EditorGUILayout.FloatField(value != null ? (float)value : 0f);

        if (type == typeof(double))
            return EditorGUILayout.DoubleField(value != null ? (double)value : 0d);

        if (type == typeof(bool))
            return EditorGUILayout.Toggle(value != null && (bool)value);

        if (type == typeof(string))
            return EditorGUILayout.TextField((string)value ?? "");

        if (type == typeof(byte))
            return (byte)Mathf.Clamp(EditorGUILayout.IntField(value != null ? (byte)value : 0), 0, 255);

        EditorGUILayout.LabelField(value?.ToString() ?? "null");
        return value;
    }

    private bool IsEditableType(Type type)
    {
        return type == typeof(int) || type == typeof(long) || type == typeof(float) ||
               type == typeof(double) || type == typeof(bool) || type == typeof(string) ||
               type == typeof(byte);
    }

    #region Data Operations

    private void LoadAllData()
    {
        _dataCache.Clear();
        _dirtyFlags.Clear();

        if (_prefKeys == null) return;

        foreach (string key in _prefKeys)
        {
            LoadData(key);
        }

        Repaint();
    }

    private void LoadData(string key)
    {
        string filePath = GetPrefsFilePath(key);
        _dirtyFlags[key] = false;

        if (!File.Exists(filePath))
        {
            _dataCache[key] = null;
            return;
        }

        try
        {
            byte[] encryptedBytes = File.ReadAllBytes(filePath);
            byte[] decryptedBytes = Decrypt(encryptedBytes);

            if (decryptedBytes == null || decryptedBytes.Length == 0)
            {
                Debug.LogError($"[PrefsEditor] Failed to decrypt {key}");
                _dataCache[key] = null;
                return;
            }

            using MemoryStream stream = new(decryptedBytes);
            BinaryFormatter formatter = new();
            _dataCache[key] = formatter.Deserialize(stream);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PrefsEditor] Load error for {key}: {e.Message}");
            _dataCache[key] = null;
        }
    }

    private void SaveAllData()
    {
        if (_prefKeys == null) return;

        foreach (string key in _prefKeys)
        {
            if (_dirtyFlags.GetValueOrDefault(key))
            {
                SaveData(key);
            }
        }
    }

    private void SaveData(string key)
    {
        if (!_dataCache.TryGetValue(key, out object data) || data == null)
        {
            Debug.LogWarning($"[PrefsEditor] No data to save for {key}");
            return;
        }

        try
        {
            string filePath = GetPrefsFilePath(key);

            // Serialize
            using MemoryStream stream = new();
            BinaryFormatter formatter = new();
            formatter.Serialize(stream, data);
            byte[] bytes = stream.ToArray();

            // Encrypt
            byte[] encryptedBytes = Encrypt(bytes);
            if (encryptedBytes == null)
            {
                Debug.LogError($"[PrefsEditor] Failed to encrypt {key}");
                return;
            }

            // Write
            File.WriteAllBytes(filePath, encryptedBytes);
            _dirtyFlags[key] = false;

            Debug.Log($"[PrefsEditor] Saved: {key}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PrefsEditor] Save error for {key}: {e.Message}");
        }
    }

    private void CreateDefaultData(string key)
    {
        Type type = Type.GetType(key);
        if (type == null)
        {
            Debug.LogError($"[PrefsEditor] Type not found: {key}");
            return;
        }

        try
        {
            _dataCache[key] = Activator.CreateInstance(type);
            _dirtyFlags[key] = true;
            Debug.Log($"[PrefsEditor] Created default: {key}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PrefsEditor] Create error for {key}: {e.Message}");
        }
    }

    private void ClearData(string key)
    {
        if (_dataCache.TryGetValue(key, out object data) && data != null)
        {
            MethodInfo clearMethod = data.GetType().GetMethod("Clear");
            if (clearMethod != null)
            {
                clearMethod.Invoke(data, null);
                _dirtyFlags[key] = true;
                Debug.Log($"[PrefsEditor] Cleared: {key}");
            }
        }
        else
        {
            CreateDefaultData(key);
        }
    }

    private void DeletePrefsFile(string key)
    {
        string filePath = GetPrefsFilePath(key);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Debug.Log($"[PrefsEditor] Deleted: {filePath}");
        }
        _dataCache[key] = null;
        _dirtyFlags[key] = false;
    }

    private void DeleteAllPrefs()
    {
        if (_prefKeys == null) return;

        foreach (string key in _prefKeys)
        {
            DeletePrefsFile(key);
        }
        Debug.Log("[PrefsEditor] All prefs deleted.");
    }

    #endregion

    #region Encryption

    private byte[] Encrypt(byte[] rawData)
    {
        if (rawData == null || rawData.Length == 0) return Array.Empty<byte>();

        try
        {
            using var rijndael = new RijndaelManaged();
            rijndael.Mode = CipherMode.CBC;
            rijndael.Padding = PaddingMode.PKCS7;
            rijndael.KeySize = 128;
            rijndael.BlockSize = 128;
            rijndael.Key = KeyBytes;
            rijndael.IV = KeyBytes;

            using MemoryStream memoryStream = new();
            using (CryptoStream cryptoStream = new(
                       memoryStream, rijndael.CreateEncryptor(), CryptoStreamMode.Write))
            {
                cryptoStream.Write(rawData, 0, rawData.Length);
                cryptoStream.FlushFinalBlock();
            }

            return memoryStream.ToArray();
        }
        catch (Exception e)
        {
            Debug.LogError($"[PrefsEditor] Encrypt Error: {e.Message}");
            return null;
        }
    }

    private byte[] Decrypt(byte[] encryptedData)
    {
        if (encryptedData == null || encryptedData.Length == 0) return Array.Empty<byte>();

        try
        {
            using var rijndael = new RijndaelManaged();
            rijndael.Mode = CipherMode.CBC;
            rijndael.Padding = PaddingMode.PKCS7;
            rijndael.KeySize = 128;
            rijndael.BlockSize = 128;
            rijndael.Key = KeyBytes;
            rijndael.IV = KeyBytes;

            using MemoryStream memoryStream = new();
            using (CryptoStream cryptoStream = new(
                       memoryStream, rijndael.CreateDecryptor(), CryptoStreamMode.Write))
            {
                cryptoStream.Write(encryptedData, 0, encryptedData.Length);
                cryptoStream.FlushFinalBlock();
            }

            return memoryStream.ToArray();
        }
        catch (Exception e)
        {
            Debug.LogError($"[PrefsEditor] Decrypt Error: {e.Message}");
            return null;
        }
    }

    #endregion

    private string GetPrefsFilePath(string key)
    {
        return Path.Combine(Application.persistentDataPath, $"{key}.dat");
    }
}
