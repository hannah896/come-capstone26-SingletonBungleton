using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Blossom.Preference;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 보드 매니저(역직렬화)
/// 데이터매니저(직렬화) 에서 설치되어있는 데이터를 저장함
/// 게임의 스테이지 데이터를 관리해주는 매니저
/// </summary>
public class DataManager : CoreManager
{
    #region Properties

    public StageData EditorStageData { get; set; }

    #endregion

    #region Fields

    private Dictionary<int, TextAsset> _stageData = new();
    private Dictionary<Type, Dictionary<string, Data>> _data = new();
    private Dictionary<string, object> _cacheObject = new();

    #endregion

    #region Initialize

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();
        LoadData();
        LoadStageData();
        PrefsInitialize();
    }

    private void LoadData()
    {
        TextAsset[] dataFiles = Resources.LoadAll<TextAsset>($"{BlossomPath.RESOURCES_DATA}");
        foreach (TextAsset textAsset in dataFiles)
        {
            string typeName = textAsset.name.Split('-')[0];
            Type type = Type.GetType(typeName);
            if (type == null)
            {
                Debug.LogError($"[DataManager] Initialize(): The type({textAsset}) was not found.");
                continue;
            }

            if (!_data.ContainsKey(type)) _data[type] = new();

            IEnumerable<Data> dataList = typeof(JsonConvert)
                    .GetMethods()
                    .FirstOrDefault(m =>
                        m.Name == "DeserializeObject" && m.IsGenericMethod && m.GetParameters().Length == 1)
                    ?.MakeGenericMethod(typeof(List<>).MakeGenericType(type))
                    .Invoke(null, new object[] { textAsset.text })
                as IEnumerable<Data>;
            if (dataList == null) continue;

            Dictionary<string, Data> dataDictionary = dataList.ToDictionary(x => x.Key);
            foreach (KeyValuePair<string, Data> pair in dataDictionary)
            {
                _data[type][pair.Key] = pair.Value;
            }
        }
    }

    private void LoadStageData()
    {
        _stageData = Resources.LoadAll<TextAsset>($"{BlossomPath.RESOURCES_STAGEDATA}")
            .ToDictionary(x => int.Parse(x.name.Replace("Stage", "")), x => x);
    }

    #endregion

    #region GetData

    public T GetData<T>(string key) where T : class
    {
        if (!_cacheObject.TryGetValue(key, out object data))
        {
            Debug.LogError($"cache data not found {key}");
            return default;
        }
        
        T result = data as T;
        if (result == null)
        {
            Debug.LogError($"change failed data {typeof(T)}");
            return default;
        }
        
        return result;
    }

    public bool ContainsKey<T>(string key) where T : Data
    {
        if (!_data.TryGetValue(typeof(T), out Dictionary<string, Data> dictionary))
        {
            Debug.LogError($"[DataManager] Get<{typeof(T)}>({key}): Failed to get data. Not found the type.");
            return false;
        }

        return dictionary.ContainsKey(key);
    }

    public T Get<T>(string key) where T : Data
    {
        if (!_data.TryGetValue(typeof(T), out Dictionary<string, Data> dictionary))
        {
            Debug.LogError($"[DataManager] Get<{typeof(T)}>({key}): Failed to get data. Not found the type.");
            return null;
        }

        if (!dictionary.TryGetValue(key, out Data data))
        {
            Debug.LogError($"[DataManager] Get<{typeof(T)}>({key}): Failed to get data. Not found the key.");
            return null;
        }

        return data as T;
    }

    public List<T> GetAll<T>() where T : Data
    {
        if (!_data.TryGetValue(typeof(T), out Dictionary<string, Data> dictionary))
        {
            Debug.LogError($"[DataManager] GetAll<{typeof(T)}>(): Failed to get data. Not found the type.");
            return null;
        }

        return dictionary.Values.Select(x => x as T).ToList();
    }

    public StageData GetStageData(int stage)
    {
        if (!_stageData.TryGetValue(stage, out TextAsset textAsset)) return null;
        try
        {
            JsonSerializerSettings settings = new();
            settings.Converters.Add(new Vector2IntDictionaryConverter());
            StageData stageData = JsonConvert.DeserializeObject<StageData>(textAsset.text, settings);
            return stageData;
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] GetStageData({stage}): Failed to deserialize stage data: {e.Message}");
            return null;
        }
    }

    public int GetMaxStageCount()
    {
        int stageCount = 0;
        stageCount = _stageData.Select(x => x.Key).Max();
        return stageCount;
    }

    #endregion

    #region Prefs

    private PlayPrefs _play;
    private CurrencyPrefs _currency;

    private void PrefsInitialize()
    {
        if (SceneManager.GetActiveScene().name == "EditorScene") Prefs.DeleteAll();
        Prefs.Initialize();
        _play = Prefs.Get<PlayPrefs>();
        _currency = Prefs.Get<CurrencyPrefs>();
        if (_play.FirstSessionTime == Def.TimeMin) _play.FirstSessionTime = Def.TimeCurrent;
        _play.LastSessionTime = Def.TimeCurrent;
        ItemDisplay.OnItemReceived += OnItemReceived;
    }

    private void OnItemReceived(CollectorItemType type, int amount)
    {
        switch (type)
        {
            case CollectorItemType.Currency: _currency.Currency.DisplayValue += amount; break;
            case CollectorItemType.Lives: _currency.Lives.DisplayValue += amount; break;
            case CollectorItemType.UnlimitedLives: _currency.LivesUnlimitedRemainTime.DisplayValue += amount; break;
        }
    }

    public void PrefsSync()
    {
        _currency.Currency.DisplayValueSync();
        _currency.Lives.DisplayValueSync();
        _currency.LivesUnlimitedRemainTime.DisplayValueSync();
        _play.Stage.DisplayValueSync();
    }

    #endregion
}

public class Data
{
    public string Key { get; set; }
}