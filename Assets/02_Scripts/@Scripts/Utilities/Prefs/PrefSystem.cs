using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blossom.Preference {

    public static class Prefs {
        public static bool IsInitialized { get; private set; }
        public static event Action OnBeforeSave;

        #region Initialize
        
        public static void Initialize() {
            if (IsInitialized) return;
            PrefSystem.Initialize();
            IsInitialized = true;
        }
        
        public static void DeleteAll() => PrefSystem.DeleteAll();
        
        #endregion

        #region Get

        public static T Get<T>() where T : class, IPrefData, new() => PrefSystem.Get<T>();

        #endregion

        #region Save / Load

        public static void SaveAll(bool forceSave = false) => PrefSystem.SaveAll(forceSave);
        public static void Save(string key, bool forceSave = false) => PrefSystem.Save(key, forceSave);
        public static void LoadAll() => PrefSystem.LoadAll();
        public static void Load(string key) => PrefSystem.Load(key);
        
        #endregion
        
        #region Events
        
        public static void OnChanged(string key) => PrefSystem.OnChanged(key);
        public static void InvokeOnBeforeSave() => OnBeforeSave?.Invoke();
        public static void RegisterOnDataLoaded(Action<string> cb) => PrefSystem.OnDataLoaded += cb;
        public static void UnregisterOnDataLoaded(Action<string> cb) => PrefSystem.OnDataLoaded -= cb;

        #endregion
        
    }
    
    internal static class PrefSystem {

        #region Settings.

        public static readonly bool UseEncryption = true;
        public static readonly bool SaveUseThread = true;
        public static readonly bool ClearOnSaves = false;
        public static readonly float AutoSaveInterval = 0f;
        public static readonly string[] PrefKeys = new[]
        {
            nameof(PlayPrefs),
            nameof(CurrencyPrefs),
            //nameof(AdditionalPrefs),
            nameof(SettingPrefs),
            //nameof(SupportPrefs),
        };

        #endregion

        #region Fields

        private static Dictionary<string, PrefData> _prefDataMap;
        private static Dictionary<string, bool> _saveRequestMap;

        public static event Action<string> OnDataLoaded;

        #endregion

        #region Initialize

        public static void Initialize() {
            PrefIO.Initialize();

            _prefDataMap = new();
            _saveRequestMap = new();

            LoadAll();
            if (ClearOnSaves) ClearAllPrefData();

            GameObject gameObject = new("[SAVE CALLBACK RECEIVER]") {
                hideFlags = HideFlags.HideInHierarchy
            };
            Object.DontDestroyOnLoad(gameObject);
            UnityCallbackReceiver receiver = gameObject.AddComponent<UnityCallbackReceiver>();
            if (AutoSaveInterval > 0) receiver.StartCoroutine(CoAutoSave());
        }

        #endregion

        #region Get

        public static T Get<T>() where T : class, IPrefData, new() {
            string key = typeof(T).Name;
            if (_prefDataMap.TryGetValue(key, out PrefData data)) return data as T;
            LoadAll();
            if (_prefDataMap.TryGetValue(key, out data)) return data as T;
            Debug.LogError($"[PrefSystem] Get<{key}>(): PrefData is not registered.");
            return null;
        }

        #endregion

        #region Save / Load

        public static void SaveAll(bool forceSave = false) {
            foreach (string key in PrefKeys) Save(key, forceSave);
        }

        public static void Save(string key, bool forceSave = false) {
            if (!_prefDataMap.TryGetValue(key, out PrefData data)) {
                Debug.LogError($"[PrefSystem] Save({key}): Data is not loaded.");
                return;
            }

            if (!forceSave && !_saveRequestMap[key]) return;

            data.Flush();
            if (SaveUseThread) {
                Thread thread = new(() => PrefIO.Serialize(data));
                thread.Start();
            }
            else {
                PrefIO.Serialize(data);
            }

            _saveRequestMap[key] = false;
        }

        private static IEnumerator CoAutoSave() {
            WaitForSeconds wait = new(AutoSaveInterval);
            while (true) {
                yield return wait;
                SaveAll();
            }
        }

        public static void LoadAll() {
            foreach (string key in PrefKeys) Load(key);
        }

        public static void Load(string key) {
             if (_prefDataMap.ContainsKey(key)) return;

             Type type = Type.GetType(key);
             if (type == null) {
                 Debug.LogError($"[PrefSystem] Load<{key}>(): Type is not found");
                 return;
             }

             MethodInfo method =
                 typeof(PrefIO).GetMethod(nameof(PrefIO.Deserialize), BindingFlags.Public | BindingFlags.Static);
             MethodInfo closedMethod = method.MakeGenericMethod(type);
             PrefData data = (PrefData)closedMethod.Invoke(null, new object[] { key });
             _prefDataMap[key] = data;
             // _prefDataMap[key] = PrefIO.Deserialize(key);
             
             _saveRequestMap[key] = false;
             OnDataLoaded?.Invoke(key);
        }

        #endregion

        #region Clear

        public static void DeleteAll() {
            foreach (string key in PrefKeys) PrefIO.DeleteFile(key);
        }
        
        private static void ClearAllPrefData() {
            foreach (string key in PrefKeys) ClearPrefData(key);
        }

        private static void ClearPrefData(string key) {
            if (!_prefDataMap.TryGetValue(key, out PrefData data)) return;
            data.Clear();
            _saveRequestMap[key] = true;
            Debug.Log($"[PrefSystem] PrefData({key}) is cleared.");
        }

        #endregion

        #region Events

        public static void OnChanged(string key) {
            if (_prefDataMap.ContainsKey(key))
                _saveRequestMap[key] = true;
        }

        #endregion

        private class UnityCallbackReceiver : MonoBehaviour {
            
            private void OnDestroy() {
#if UNITY_EDITOR
                Prefs.InvokeOnBeforeSave();
                SaveAll(true);
#endif
            }

            private void OnApplicationFocus(bool hasFocus) {
#if !UNITY_EDITOR
                if (!hasFocus) {
                    Prefs.InvokeOnBeforeSave();
                    SaveAll();
                }
#endif
            }
        }
        
    }
    
}
