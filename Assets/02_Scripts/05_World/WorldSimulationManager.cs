using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WorldSimulationManager : 월드 시뮬레이션 관리 클래스 
/// 자원 재생, 몬스터 스폰, 시간대 변화, 
/// </summary>
public class WorldSimulationManager : MonoBehaviour
{
    private WorldChunkDirector _chunkDirector;
    private WorldClock _worldClock;
    private WorldSkyBoxController _skyBoxController;
    private Light _directionalLight;
    private bool _isInitialized = false;

    public void Initialize(WorldChunkDirector chunkDirector, WorldSkyBoxSettings skyBoxSettings)
    {
        _chunkDirector = chunkDirector;

        // 재초기화 시 이전 시계 구독부터 해제하여 시간대 이벤트가 중복 실행되지 않게 한다.
        if (_worldClock != null)
            _worldClock.OnPhaseChanged -= HandlePhaseChanged;

        // 월드 시뮬레이션이 사용할 시계를 준비한다. 이미 존재하면 재사용한다.
        _worldClock = WorldClock.Instance;
        if (_worldClock == null)
            _worldClock = new GameObject(nameof(WorldClock)).AddComponent<WorldClock>();

        _worldClock.OnPhaseChanged += HandlePhaseChanged;

        InitializeSkyBox(skyBoxSettings);

        _isInitialized = true;
    }

    private void InitializeSkyBox(WorldSkyBoxSettings skyBoxSettings)
    {
        if (skyBoxSettings == null)
        {
            if (_skyBoxController != null) _skyBoxController.Unbind();
            Debug.LogError("[WorldSimulationManager] WorldSettings에 SkyBoxSettings가 연결되지 않았습니다.", this);
            return;
        }

        // 시뮬레이션 오브젝트에 런타임으로 생성한다. 씬에 미리 배치할 필요가 없다.
        if (_skyBoxController == null && !TryGetComponent(out _skyBoxController))
            _skyBoxController = gameObject.AddComponent<WorldSkyBoxController>();

        if (_directionalLight == null)
            _directionalLight = FindDirectionalLight();

        _skyBoxController.Initialize(_worldClock, skyBoxSettings, _directionalLight);
    }

    private Light FindDirectionalLight()
    {
        // 씬의 태양이 지정되어 있으면 우선 사용하고, 없으면 같은 씬의 가장 밝은 방향광을 찾는다.
        Light sun = RenderSettings.sun;
        if (sun != null && sun.type == LightType.Directional &&
            sun.isActiveAndEnabled && sun.gameObject.scene == gameObject.scene)
            return sun;

        Light brightest = null;
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional || !light.isActiveAndEnabled ||
                light.gameObject.scene != gameObject.scene) continue;

            if (brightest == null || light.intensity > brightest.intensity)
                brightest = light;
        }
        return brightest;
    }

    private void OnDestroy()
    {
        _isInitialized = false;
        if (_skyBoxController != null) _skyBoxController.Unbind();
        _skyBoxController = null;
        if (_worldClock != null)
            _worldClock.OnPhaseChanged -= HandlePhaseChanged;
        _worldClock = null;
    }

    private void HandlePhaseChanged(TimePhase newPhase)
    {
        if (!_isInitialized || _worldClock == null) return;
        float currentTime = _worldClock.TotalInGameSeconds;

        // 현재 플레이어 주변에 로드된 청크들에 접근
        foreach (ChunkData chunk in _chunkDirector.GetActiveChunks())
        {
            // 재생성 해야 할 오브젝트 목록을 받아와서 다시 스폰
            ProcessRespawnForChunk(chunk, currentTime);
        }
    }
    /// <summary>
    ///  청크내에 파괴된 오브젝트들을 확인하고, 재생성해야 할 오브젝트들을 다시 스폰시키는 함수
    /// </summary>
    /// <param name="chunk"></param>
    /// <param name="currentTime"></param>
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

            DisposeData targetData = chunk.DisposeDatas.Find(p => p.instanceId == instanceId);

            _chunkDirector.ObjectSpawner.SpawnSingleObjectAsync(chunk, targetData).Forget();
        }
    }
}