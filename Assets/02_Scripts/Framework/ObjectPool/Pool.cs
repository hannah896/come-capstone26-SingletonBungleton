using UnityEngine;
using UnityEngine.Pool;

public class Pool
{
    private GameObject original;
    private readonly Transform transform;
    private readonly ObjectPool<GameObject> poolables;

    /// <summary>
    /// 오브젝트 풀 생성자
    /// 
    /// </summary>
    /// <param name="key"></param>
    /// <param name="parent"></param>
    public Pool(string key, Transform parent)
    {
        Managers.Resource.Load<GameObject>(key, original =>
        {
            this.original = original;
        });

        transform = new GameObject($"Pool_{key}").transform;
        transform.SetParent(parent);

        poolables = new(CreateFunc, ActionOnGet, ActionOnRelease, ActionOnDestroy);
    }

    /// <summary>
    /// 풀 매니저에서 호출되는 풀에서 오브젝트를 빼오는 함수
    /// </summary>
    /// <returns></returns>
    public GameObject Get()
    {
        return poolables.Get();
    }

    /// <summary>
    /// 풀 매니저에서 호출되는 오브젝트 반납 함수
    /// </summary>
    /// <param name="poolable"></param>
    public void Release(Poolable poolable)
    {
        poolables.Release(poolable.gameObject);
    }

    /// <summary>
    /// 풀에 오브젝트가 더 필요할 때 새로 생성하는 함수
    /// </summary>
    /// <returns></returns>
    private GameObject CreateFunc()
    {
        return Managers.Resource.Instantiate(original);
    }

    /// <summary>
    /// 풀에서 오브젝트를 가져오는 함수
    /// </summary>
    /// <param name="obj"></param>
    private void ActionOnGet(GameObject obj)
    {
        if (obj == null)
            return;
        obj.SetActive(true);
    }

    /// <summary>
    /// 오브젝트를 풀에 반납할 때 호출되는 함수
    /// </summary>
    /// <param name="obj"></param>
    private void ActionOnRelease(GameObject obj)
    {
        obj.SetActive(false);
        obj.transform.SetParent(transform);
    }

    /// <summary>
    /// 오브젝트가 파괴될 때 호출되는 함수
    /// (풀에 너무 많이 쌓였거나, 특정 상황에서 진짜로 더 이상 쓸 일이 없는 오브젝트일 때만 파괴)
    /// </summary>
    /// <param name="obj"></param>
    private void ActionOnDestroy(GameObject obj)
    {
        Managers.Resource.Destroy(obj);
    }
}
