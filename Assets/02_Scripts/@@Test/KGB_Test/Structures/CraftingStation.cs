using System;
using System.Collections.Generic;
using UnityEngine;

// RecipeDataSO
//TODO: requiredStation 필드 추가
//[Header("=== 제작 조건 ===")]
//[Tooltip("이 레시피 제작에 필요한 스테이션. None이면 맨손 가능.")]
//public StationType requiredStation = StationType.None;
// CraftingManager
//TODO:     if (!_context.IsUnlocked(recipe.requiredStation)) return false;   // CraftingManagner에서 Crafting 검사 로직에서 추가
//TODO: 

public abstract class CraftingStation : Structure
{
    public abstract StationType StationType { get; }

    [Header("근접 트리거")]
    [SerializeField] private SphereCollider proximityTrigger;
    [SerializeField] private float proximityRadius = 2.5f;

    private void Awake() => EnsureTrigger();
    private void OnValidate() => EnsureTrigger();

    // 
    private void EnsureTrigger()    
    {
        if (proximityTrigger == null)
        {
            proximityTrigger = Extensions.GetOrAddComponent<SphereCollider>(gameObject);
        }
        proximityTrigger.radius = proximityRadius;
        proximityTrigger.isTrigger = true;
    }

    protected virtual void OnPlayerEnter(Player player) { }
    protected virtual void OnPlayerExit(Player player) { }

    private void OnTriggerEnter(Collider other)
    {
        Player player = other.GetComponentInParent<Player>();
        if (player == null) return;
        OnPlayerEnter(player);
    }
    private void OnTriggerExit(Collider other) 
    {
        Player player = other.GetComponentInParent<Player>();
        if (player == null) return;
        OnPlayerExit(player);
    }
}

/// <summary>
///  플레이어가 특정 크래프팅 스테이션(작업대, 모닥불 등) 근처에 있을 때 해당 스테이션이 활성화됨.
/// </summary>
public class CraftingContext
{
    private readonly Dictionary<StationType, int> _activeStationCounts = new();
    public event Action OnStationsChanged;

    public bool IsUnlocked(StationType s)
        => s == StationType.None || _activeStationCounts.ContainsKey(s);
    public void AddStation(CraftingStation station) 
    {
        if (_activeStationCounts.ContainsKey(station.StationType))
        {
            _activeStationCounts[station.StationType]++;
        }
        else
        {
            _activeStationCounts[station.StationType] = 1;
            OnStationsChanged?.Invoke();
        }
    }
    public void RemoveStation(CraftingStation station)
    {
        if (_activeStationCounts.ContainsKey(station.StationType))
        {
            _activeStationCounts[station.StationType]--;
            if (_activeStationCounts[station.StationType] <= 0)
            {
                _activeStationCounts.Remove(station.StationType);
                OnStationsChanged?.Invoke();
            }
        }
    }
}

public enum StationType
{
    None,        // 맨손 (Survival 카테고리 — 횃불, 모닥불, 망치 같은 기초)
    Workbench,   // 제작대
    Campfire,    // 모닥불
    CookingPot,  // 요리솥
    Furnace      // 화덕
}