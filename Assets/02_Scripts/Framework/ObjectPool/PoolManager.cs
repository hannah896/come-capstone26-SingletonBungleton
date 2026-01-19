using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

// 오브젝트 풀들을 관리하는 풀 매니저
public class PoolManager
{
    private Transform transform;
    private RectTransform rectTransform;

    private readonly Dictionary<string, Pool> pools = new();

    /// <summary>
    /// 초기화 함수
    /// 게임 실행 시 최초로 실행되어야하는 작업들 실행
    /// </summary>
    public void Init()
    {
        transform = new GameObject(nameof(PoolManager)).transform;
        transform.SetParent(Managers.Instance.transform);
        rectTransform = new GameObject("UI" + nameof(PoolManager)).AddComponent<Canvas>().GetComponent<RectTransform>();
    }

    /// <summary>
    /// pools에서 해당 key값을 가진 풀을 찾은뒤 없다면 null, 있다면 pools에서 해당 풀을 찾아 Get()으로 오브젝트를 꺼내옴
    /// </summary>
    /// <param name="key">어드레서블에 등록된 이름</param>
    /// <returns>풀에 존재하는 오브젝트 or null</returns>
    public GameObject Get(string key)
    {

        if (pools.TryGetValue(key, out var pool) == false) return null;

        return pool.Get();
    }

    /// <summary>
    /// 오브젝트를 풀에 반납할 때 사용하는 함수 
    /// 만약 pools에 해당 key값을 갖고있지않다면 새로운 풀을 생성 후 등록하고 이후 해당 풀에 오브젝트를 반납
    /// </summary>
    /// <param name="poolable"></param>
    public void Release(Poolable poolable)
    {
        string key = poolable.name;
        if (pools.TryGetValue(key, out var pool) == false)
        {
            //UI 전용 풀
            if (poolable.TryGetComponent<UI_Base>(out var ui))
                pool = new(key, rectTransform);

            // 일반 오브젝트용 풀
            else
                pool = new(key, transform);

            pools.Add(key, pool);
        }
        pool.Release(poolable);
    }
}