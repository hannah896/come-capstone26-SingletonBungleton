using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Blossom.Preference;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임의 스테이지 데이터를 관리해주는 매니저.
/// JSON 파일 로드, 스테이지 데이터 관리, 프리퍼런스 초기화를 담당합니다.
/// </summary>
public class DataManager : PrimaryManager
{
    #region Fields

    // 스테이지 번호별 텍스트 에셋
    private Dictionary<int, TextAsset> _stageData = new();

    // 타입별 데이터 딕셔너리
    private Dictionary<Type, Dictionary<string, Data>> _data = new();

    // 캐시된 오브젝트
    private Dictionary<string, object> _cacheObject = new();

    // 플레이 프리퍼런스
    private PlayPrefs _play;

    // 재화 프리퍼런스
    private CurrencyPrefs _currency;

    #endregion

    #region Properties

    // 에디터용 스테이지 데이터
    public StageData EditorStageData { get; set; }

    #endregion

    #region Initialization

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();
        LoadData();
        LoadStageData();
        PrefsInitialize();
    }

    // 데이터 파일 로드
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

    // 스테이지 데이터 로드
    private void LoadStageData()
    {
        _stageData = Resources.LoadAll<TextAsset>($"{BlossomPath.RESOURCES_STAGEDATA}")
            .ToDictionary(x => int.Parse(x.name.Replace("Stage", "")), x => x);
    }

    #endregion

    #region Get Data

    /// <summary>
    /// 캐시된 데이터를 가져옵니다.
    /// </summary>
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

    /// <summary>
    /// 특정 키가 존재하는지 확인합니다.
    /// </summary>
    public bool ContainsKey<T>(string key) where T : Data
    {
        if (!_data.TryGetValue(typeof(T), out Dictionary<string, Data> dictionary))
        {
            Debug.LogError($"[DataManager] Get<{typeof(T)}>({key}): Failed to get data. Not found the type.");
            return false;
        }

        return dictionary.ContainsKey(key);
    }

    /// <summary>
    /// 키로 데이터를 가져옵니다.
    /// </summary>
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

    /// <summary>
    /// 특정 타입의 모든 데이터를 가져옵니다.
    /// </summary>
    public List<T> GetAll<T>() where T : Data
    {
        if (!_data.TryGetValue(typeof(T), out Dictionary<string, Data> dictionary))
        {
            Debug.LogError($"[DataManager] GetAll<{typeof(T)}>(): Failed to get data. Not found the type.");
            return null;
        }

        return dictionary.Values.Select(x => x as T).ToList();
    }

    /// <summary>
    /// 스테이지 데이터를 가져옵니다.
    /// </summary>
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

    /// <summary>
    /// 최대 스테이지 수를 반환합니다.
    /// </summary>
    public int GetMaxStageCount()
    {
        int stageCount = 0;
        stageCount = _stageData.Select(x => x.Key).Max();
        return stageCount;
    }

    #endregion

    #region Prefs

    // 프리퍼런스 초기화
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

    // 아이템 수령 처리
    private void OnItemReceived(CollectorItemType type, int amount)
    {
        switch (type)
        {
            case CollectorItemType.Currency: _currency.Currency.DisplayValue += amount; break;
            case CollectorItemType.Lives: _currency.Lives.DisplayValue += amount; break;
            case CollectorItemType.UnlimitedLives: _currency.LivesUnlimitedRemainTime.DisplayValue += amount; break;
        }
    }

    /// <summary>
    /// 프리퍼런스 값을 동기화합니다.
    /// </summary>
    public void PrefsSync()
    {
        _currency.Currency.DisplayValueSync();
        _currency.Lives.DisplayValueSync();
        _currency.LivesUnlimitedRemainTime.DisplayValueSync();
        _play.Stage.DisplayValueSync();
    }

    #endregion
}

/// <summary>
/// 데이터 기본 클래스.
/// </summary>
public class Data
{
    /// <summary>
    /// 데이터 키.
    /// </summary>
    public string Key { get; set; }
}
