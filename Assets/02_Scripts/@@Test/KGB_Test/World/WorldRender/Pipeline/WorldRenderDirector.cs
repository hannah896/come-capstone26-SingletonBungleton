using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class WorldRenderDirector : MonoBehaviour
{
    #region Worker Fields
    private TerrainBuilder _terrainBuilder;
    private TerrainPainter _terrainPainter;
    // private ObjectSpawner _objectSpawner;
    #endregion

    #region 
    TerrainData _terrainData;
    GameObject _terrainGO;
    #endregion


    async void Start()
    {
        await UniTask.WaitUntil(() => Main.Instance != null);

        InitializeServices();
    }

    private void InitializeServices()
    {
        // 작업자 고용
        _terrainBuilder = new TerrainBuilder();
        _terrainPainter = new TerrainPainter();
        // _objectSpawner = new ObjectSpawner();
    }

    /// <summary>
    /// 외부(UsageExample)에서 호출할 렌더링 메인 함수
    /// </summary>
    public async UniTask RenderWorldAsync(
        WorldLogicData logicData,
        WorldGraphData graphData,
        List<WorldObjectDisposer.SpawnResult> spawnData,
        WorldSettings settings,
        CancellationToken ct)
    {
        Debug.Log("🎨 월드 렌더링 파이프라인 시작!");

        // 기존에 렌더링된 메쉬나 오브젝트가 있다면 싹 다 지우기 (초기화)
        ClearRenderedWorld();

        // 1. 지형(Terrain) 생성 및 높이맵 데이터 적용
        (_terrainData, _terrainGO) = await _terrainBuilder.BuildTerrainAsync(logicData, graphData, settings, ct);

        // 2. 프리팹 오브젝트 스폰
        // await _objectSpawner.SpawnObjectsAsync(spawnData, ...);

        Debug.Log("✨ 월드 렌더링 완료!");
    }

    private void ClearRenderedWorld()
    {
        // 1. 기존 지형 게임 오브젝트 파괴
        if (_terrainGO != null)
        {
            Destroy(_terrainGO);
            _terrainGO = null;
        }

        // 2. 메모리에 남은 TerrainData 에셋 해제 (메모리 릭 방지)
        if (_terrainData != null)
        {
            Destroy(_terrainData);
            _terrainData = null;
        }
    }
}