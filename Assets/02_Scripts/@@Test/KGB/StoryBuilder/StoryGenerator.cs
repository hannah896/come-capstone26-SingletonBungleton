using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

/// <summary>
/// Task들을 생성하고 연결하여 하나의 Story를 생성하는 클래스
/// </summary>
public class StoryGenerator : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private WorldSettings _worldSettings;
    [SerializeField] private StoryData _storyData;

    [Header("Debug")]
    [SerializeField] private bool _enableStepByStep = false;
    [SerializeField] private float _stepDelay = 0.1f;

    private CancellationTokenSource _cts;

    // 배치된 Node 데이터
    private List<Node> _placedRegions = new();

    private List<NodeConnection> _regionConnections = new();

    private List<KeyData> _availableKeys;


    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    /// <summary>
    /// StoryData와 Lock & Key 시스템을 이용하여 Story 생성
    /// </summary>
    public async UniTask<GraphResult> GenerateStoryAsync(
        StoryData storyData,
        CancellationToken ct)
    {
        try
        {
            // Phase 1: 고정 Region들 생성 및 연결
            await GenerateFixedRegionsAsync(storyData, ct);

            // Phase 2: 사이드 Task들 생성 및 연결
            await GenerateSideRegionsAsync(storyData, ct);

            
            return new GraphResult
            {
                nodes = _placedRegions,
                nodeConnections = _regionConnections,
            };
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log($"Story '{storyData.StoryName}' 생성이 취소되었습니다.");
            return null;
        }
    }

    /// <summary>
    /// WorldSettings 설정
    /// </summary>
    public void SetWorldSettings(WorldSettings settings)
    {
        _worldSettings = settings;
        
    }

    #region Helper Methods 
    private int GetChildCount(Node parentNode)
    {
        return _regionConnections.Count(conn => conn.ParentNode == parentNode);
    }
    private Node PickParentNode()
    {
        var candidates = _placedRegions.Where(n => GetChildCount(n) < 3).ToList();
        switch(_worldSettings.WorldBranch)
        {
            case WorldBranchSetting.Never:
                return _placedRegions.Last();
                
            case WorldBranchSetting.Least:
                return candidates.OrderByDescending(n => n.Depth).First();

            case WorldBranchSetting.Most:
                return candidates.OrderBy(n => n.Depth).First();

            case WorldBranchSetting.Default:
            default:
                return candidates[Random.Range(0, candidates.Count)];

        }
    }
    #endregion

    #region Phase 1: Fixed Region Generation
    private async UniTask GenerateFixedRegionsAsync(StoryData storyData,CancellationToken ct)
    {
        if (storyData.FixedRegions == null)
            return;

        var candidateRegions = storyData.FixedRegions;
        _availableKeys = candidateRegions.First<RegionData>()?.GivenKeys ?? new List<KeyData>();

        var startRegion = storyData.StartRegion;
        _placedRegions.Add(new Node
        {
            RegionData = startRegion,
            Depth = 1
        });

        while (candidateRegions.Count > 0)
        {

            // **현재 키로 갈 수 있는 Region 찾기**
            var unlockableRegions = candidateRegions.Where(region => region.IsUnlockable(_availableKeys)).ToList();
            
            if (unlockableRegions.Count == 0)
            {
                Debug.LogWarning($"더 이상 갈 수 있는 Region이 없습니다. 남은 후보: {unlockableRegions.Count}개");
                break;
            }

            // Region 선택 및 생성
            var selectedRegion = unlockableRegions[Random.Range(0, unlockableRegions.Count)];

            Node parentNode = PickParentNode();

            var selectedNode = new Node
            {
                RegionData = selectedRegion,
                Depth = parentNode.Depth + 1
            };

            _placedRegions.Add(selectedNode);
            _regionConnections.Add(new NodeConnection(parentNode, selectedNode));


            // 키 획득
            if (selectedRegion.GivenKeys != null)
            {
                foreach (var key in selectedRegion.GivenKeys)
                {
                    if (key != null)
                        _availableKeys.Add(key);
                }
            }

            candidateRegions.Remove(selectedRegion);

            if (_enableStepByStep)
                await UniTask.Delay(System.TimeSpan.FromSeconds(_stepDelay), cancellationToken: ct);
        }
    }
    #endregion

    #region Phase2 : Side Region Generation
    private async UniTask GenerateSideRegionsAsync(StoryData storyData, CancellationToken ct)
    {
        
        float branchMultiplier = _worldSettings?.GetBranchMultiplier() ?? 0.6f;

        int maxSideRegions = Mathf.RoundToInt(_placedRegions.Count*branchMultiplier);
        
        var candidateSideRegions = storyData.SideRegions
            .Where(t => t != null && t.IsUnlockable(_availableKeys))
            .OrderBy(_ => Random.value)
            .Take(maxSideRegions)
            .ToList();

        for (int i = 0 ; i < maxSideRegions ; i ++)
        {
            var unlockableRegions = candidateSideRegions.Where(region => region.IsUnlockable(_availableKeys)).ToList();

            if (unlockableRegions.Count == 0)
            {
                Debug.LogWarning($"더 이상 갈 수 있는 Region이 없습니다. 남은 후보: {unlockableRegions.Count}개");
                break;
            }

            var selectedRegion = unlockableRegions[Random.Range(0, unlockableRegions.Count)];

            Node parentNode = PickParentNode();
            var selectedNode = new Node
            {
                RegionData = selectedRegion,
                Depth = parentNode.Depth + 1
            };

            _placedRegions.Add(selectedNode);
            _regionConnections.Add(new NodeConnection(parentNode, selectedNode));

            // 키 획득
            if (selectedRegion.GivenKeys != null)
            {
                foreach (var key in selectedRegion.GivenKeys)
                {
                    if (key != null)
                        _availableKeys.Add(key);
                }
            }

            candidateSideRegions.Remove(selectedRegion);

            if (_enableStepByStep)
                await UniTask.Delay(System.TimeSpan.FromSeconds(_stepDelay), cancellationToken: ct);
        }
    }


    #endregion
}
