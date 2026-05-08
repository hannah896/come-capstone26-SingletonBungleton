using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 역할: 게임 시작(혹은 맵 생성) 시 딱 한 번만 실행되어 맵의 전체 구조를 만듭니다.
///
/// 실행 파이프라인: 1~5단계(지형 / 오브젝트 생성)-> 6단계(ChunkSlicer)
///
/// 출력물: ChunkData가 담겨 있는 WorldLogicData.
/// </summary>
public class WorldGraphDirector : MonoBehaviour
{
    private WorldSettings _worldSettings;
    private StoryData _storyData;
    private List<IGraphPipelineStage> _pipeline;
    private CancellationToken _ct;
    private WorldGenContext _currentContext;


    async void Start()
    {
        await UniTask.WaitUntil(() => Main.Instance != null);
        
        InitializeServices();
    }

    private void InitializeServices()
    {
        _pipeline = new List<IGraphPipelineStage>()
        {
            // 파이프라인 순서대로 생성자 호출 
            new StoryGenerator(),
            new ForceSimulator(),
            new TerritoryBuilder(),
            new HeightBuilder(),
            new PointSampler(),
            new ObjectDisposer(),
            new ItemDisposer(),
            new ChunkSlicer(),
            new PoolPreloader()
        };
    }

    #region public methods
    public async UniTask GenerateWorldLogicWithSettings(WorldSettings settings, CancellationToken ct)
    {
        _worldSettings = settings;
        _storyData = _worldSettings.CurrentStory;
        
        if (_storyData == null)
        {
            Debug.LogError("Story Data 가 WorldSettings에 설정되지 않았습니다!");
            return;
        }
        _ct = ct;

        await GenerateWorldLogicAsync(_ct);
    }
    #endregion

    #region Pipeline - 전체 흐름 관리
    private async UniTask GenerateWorldLogicAsync(CancellationToken ct)
    {
        if (_worldSettings == null || _storyData == null)
        {
            Debug.LogError("WorldSettings 또는 StoryData가 설정되지 않았습니다!");
            return;
        }

        InitializeServices();
        ClearWorld();

        try
        {
            Debug.Log($"=== ~ {_storyData.StoryName} ~ ===");

            _currentContext = new WorldGenContext
            {
                Settings = _worldSettings,
                GraphData = new WorldGraphData(),
                LogicData = new WorldLogicData(_worldSettings.GetWorldSize(), _worldSettings.ChunkSize),// 미리 2D 배열 할당
                DisposeData = new WorldDisposeData()
            };

            // 1~6단계: Story 생성 ~ Chunk 분리 까지 순차적으로 실행하는 파이프라인
            foreach (var stage in _pipeline)
            {
                stage.Initialize(_worldSettings);
                await stage.ExecuteAsync(_currentContext, ct);
                Debug.Log($"{stage.GetType().Name} 완료");
            }
            Debug.Log($"World Logic 생성 완료!");


        }
        catch (System.OperationCanceledException)
        {
            Debug.Log("월드 생성이 취소되었습니다.");
            ClearWorld();
        }
    }
    #endregion
    

    private void ClearWorld()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        if (_currentContext != null)
        {
            _currentContext.Clear();
        }
    }

    #region Getters
    public WorldLogicData GetWorldLogicData() => _currentContext.LogicData;
    public WorldGraphData GetWorldGraphData() => _currentContext.GraphData;   
    public WorldDisposeData GetWorldDisposeData() => _currentContext.DisposeData;
    #endregion

}


