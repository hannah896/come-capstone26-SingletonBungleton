using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 풀링이 가능한 Object들을 관리하는 매니저
/// 이 부분은 나중에 보완이나 삭제해야할 듯?
/// </summary>
public class ObjectManager : ContentManager {

    #region Fields

    private HashSet<Entity> _entities = new();

    #endregion
    
    #region Generals

    public override void Clear() {
        base.Clear();
        DestroyAll();
    }

    public T Instantiate<T>(string prefabName = null, Transform parent = null) where T : Entity {
        if (string.IsNullOrEmpty(prefabName)) prefabName = typeof(T).Name;
        
        GameObject obj = Main.Pool.Spawn(prefabName);
        obj.transform.SetParent(parent);
        
        T entity = obj.GetComponent<T>();
        _entities.Add(entity);
        return entity;
    }

    public void DestroyAll() {
        List<Entity> entities = new(_entities);
        foreach (Entity entity in entities) {
            Destroy(entity);
        }
    }
    
    public void Destroy<T>(T entity) where T : Entity {
        if (entity == null) return;
        _entities.Remove(entity);
        if (entity.gameObject == null || !entity.gameObject.activeSelf) return;
        entity.OnRelease();
        Main.Pool.Despawn(entity.gameObject);
    }
    #endregion
    
}