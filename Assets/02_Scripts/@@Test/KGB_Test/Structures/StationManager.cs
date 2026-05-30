using System.Collections.Generic;
using UnityEngine;

public class StationManager : MonoBehaviour
{
    public static StationManager Instance { get; private set; }
    // TODO: 플레이어 ID별로 근처에 활성화된 스테이션 타입과 그 개수 관리
    private readonly Dictionary<int, StationContext> stationContexts = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // 특정한 스테이션 타입이 플레이어 근처에 있는지 여부 반환.
    // 거리의 판정의 책임은 각 스테이션 타입의 트리거 콜라이더에 맡긴다고 가정.
    // None은 항상 활성화된 것으로 간주
    public bool IsStationNearby(int playerId, StationType stationType)
       => GetOrCreateContext(playerId).IsStationNearby(stationType);

    public void AddStation(int playerId, StationType stationType)
        => GetOrCreateContext(playerId).AddStation(stationType);

    public void RemoveStation(int playerId, StationType stationType)
        => GetOrCreateContext(playerId).RemoveStation(stationType);

    private StationContext GetOrCreateContext(int playerId)
    {
        if (!stationContexts.TryGetValue(playerId, out StationContext context))
        {
            context = new StationContext();
            stationContexts[playerId] = context;
        }
        return context;
    }

}


/// <summary>
/// 플레이어가 특정 스테이션(작업대, 모닥불 등) 근처에 있을 때 해당 스테이션이 활성화됨.
/// </summary>
public class StationContext
{
    // 플레이어 근처 활성화 된 스테이션 타입과 그 개수
    private readonly Dictionary<StationType, int> activeStationCounts = new();

    // 특정 스테이션 타입이 활성화 되어있는지 여부 반환. None은 항상 활성화된 것으로 간주
    public bool IsStationNearby(StationType stationType)
        => stationType == StationType.None || activeStationCounts.ContainsKey(stationType);

    public void AddStation(StationType stationType)
    {
        if (activeStationCounts.ContainsKey(stationType))
        {
            activeStationCounts[stationType]++;
        }
        else
        {
            activeStationCounts[stationType] = 1;
        }
    }

    public void RemoveStation(StationType stationType)
    {
        if (activeStationCounts.ContainsKey(stationType))
        {
            activeStationCounts[stationType]--;
            if (activeStationCounts[stationType] <= 0)
            {
                activeStationCounts.Remove(stationType);
            }
        }
    }
}



