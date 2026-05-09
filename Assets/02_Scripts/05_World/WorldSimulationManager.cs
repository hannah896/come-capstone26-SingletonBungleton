using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public class WorldSimulationManager : MonoBehaviour
{
    private WorldChunkDirector _chunkDirector;
    private bool _isInitialized = false;

    public void Initialize(WorldChunkDirector chunkDirector)
    {
        // 🌟 6분마다(페이즈가 바뀔 때마다) 알람을 받음
        _chunkDirector = chunkDirector;

        if (WorldClock.Instance != null)
        {
            WorldClock.Instance.OnPhaseChanged += HandlePhaseChanged;
        }

        _isInitialized = true;
    }

    private void HandlePhaseChanged(TimePhase newPhase)
    {
        if (!_isInitialized) return;
        float currentTime = WorldClock.Instance.TotalInGameSeconds;

        // 현재 플레이어 주변에 로드된 청크들에 접근
        foreach (ChunkData chunk in _chunkDirector.GetActiveChunks())
        {
            // 재생성 해야 할 오브젝트 목록을 받아와서 다시 스폰
            ProcessRespawnForChunk(chunk, currentTime);
        }
    }
    private void ProcessRespawnForChunk(ChunkData chunk, float currentTime)
    {
        List<int> toRespawn = new List<int>();

        // 1. 시간이 다 된 자원 찾기
        foreach (var kvp in chunk.DestroyedObjects)
        {
            int instanceId = kvp.Key;
            float targetRespawnTime = kvp.Value;

            // 현재 시간이 목표 시간을 지났다면 부활 확정!
            if (currentTime >= targetRespawnTime)
            {
                toRespawn.Add(instanceId);
            }
        }
        // 2. 파괴 목록에서 지워주고, 다시 맵에 스폰시키기
        foreach (int instanceId in toRespawn)
        {
            chunk.DestroyedObjects.Remove(instanceId);

            PlacementData targetData = chunk.PlacementDatas.Find(p => p.instanceId == instanceId);

            _chunkDirector.ObjectSpawner.SpawnSingleObjectAsync(chunk, targetData).Forget();
        }
    }
}