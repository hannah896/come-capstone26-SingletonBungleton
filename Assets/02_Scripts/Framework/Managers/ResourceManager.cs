using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

public class ResourceManager
{
    private readonly Dictionary<string, List<string>> sceneKeys = new();

    private Dictionary<string, Object> operations = new();

    /// <summary>
    /// 단일 리소스 로드용 메서드
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="path"></param>
    /// <param name="onComplete"></param>
    public async UniTask<T> LoadAsync<T>(string path, Action<T> onComplete = null) where T : Object
    {
        //이미 했던 작업이라면
        if (operations.TryGetValue(path, out var operation))
        {
            var result = operation as T;
            onComplete?.Invoke(result);
            return result;
        }

        var oper = Resources.LoadAsync<T>(path);
        await oper;

        if (oper.asset == null)
        {
            Debug.LogError($"{path}에 그딴 에셋 없다 ");
            onComplete?.Invoke(null);
            return oper.asset as T;
        }

        operations.Add(path, oper.asset);
        onComplete?.Invoke(oper.asset as T);
        return oper.asset as T;
    }

    /// <summary>
    /// 단일 리소스 로드용 메서드
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="path"></param>
    /// <returns></returns>
    public T Load<T>(string path, Action<T> onComplete = null) where T : Object
    {
        if (operations.TryGetValue(path, out var op))
            return op as T;

        // LoadAsync를 동기적으로 기다림
        var result = LoadAsync<T>(path, onComplete).GetAwaiter().GetResult();
        return result;
    }


    /// <summary>
    /// 경로의 오브젝트를 생성.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="onComplete"></param>
    public async void Instantiate(string path, Action<GameObject> onComplete = null)
    {
        GameObject obj = Managers.Pool.Get(path);
        if (obj != null)
        {
            onComplete?.Invoke(obj);
            return;
        }

        await LoadAsync<GameObject>(path);
        GameObject newObj = Instantiate(operations[path] as GameObject);
        onComplete?.Invoke(newObj);
    }

    /// <summary>
    /// 프레임워크 일부분으로 절대 이걸로 오브젝트 생성하면 안됨!!! 
    /// 오브젝트를 실제로 생성하는 생성부 메서드. 
    /// 게임 컨텐츠 제작시에는 void Instantiate(string key, Action<GameObject> onComplete = null) 사용할것
    /// </summary>
    /// <param name="original"></param>
    /// <returns></returns>
    public GameObject Instantiate(GameObject original)
    {
        if (original == null) return null;
        GameObject obj = Object.Instantiate(original);
        obj.name = original.name;
        return obj;
    }

    /// <summary>
    /// 오브젝트 파괴 메서드 (풀링된 오브젝트는 풀로 반환, 일반 오브젝트는 파괴)
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="tryForcePool"></param>
    public void Destroy(GameObject obj, bool tryForcePool = false)
    {
        if (obj.TryGetComponent<Poolable>(out var poolable))
        {
            Managers.Pool.Release(poolable);
            return;
        }

        Object.Destroy(obj);
    }
}