using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

/// <summary>
/// Task들을 생성하고 연결하여 하나의 Story를 생성하는 클래스
/// </summary>
public class StoryGenerator
{
    [Header("Settings")]
    [SerializeField] private WorldSettings _worldSettings;
    [SerializeField] private StoryData _storyData;



    private CancellationToken _ct;

    // 배치된 Node 데이터
    private GraphResult _storyResult;

    private List<string> _availableKeys;


    /// <summary>
    /// 외부 호출 메서드 : StoryData와 Lock & Key 시스템을 이용하여 Story 생성
    /// </summary>
    public async UniTask<GraphResult> GenerateStoryAsync(
        GraphResult result,
        WorldSettings settings,
        CancellationToken ct)
    {
        
        try
        {
            _storyResult = result;
            _worldSettings = settings;
            _storyData = _worldSettings.CurrentStory;
            _ct = ct;

            // Phase 1: 고정 Region들 생성 및 연결
            await GenerateFixedRegionsAsync();

            // Phase 2: 사이드 Task들 생성 및 연결
            await GenerateSideRegionsAsync();

            // Phase 3: World Loop 적용
            await ApplyWorldLoopAsync();


            return _storyResult;
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log($"Story '{_storyData.StoryName}' 생성이 취소되었습니다.");
            return null;
        }
    }

   
    #region Helper Methods 

    private Node PickParentNode()
    {
        var candidates = _storyResult.Nodes.Where(n => _storyResult.GetChildCount(n) < 3).ToList();
        switch(_worldSettings.WorldBranch)
        {
            case WorldBranchSetting.Never:
                return _storyResult.Nodes.Last();
                
            case WorldBranchSetting.Least:
                return candidates.OrderByDescending(n => n.Depth).First();

            case WorldBranchSetting.Most:
                return candidates.OrderBy(n => n.Depth).First();

            case WorldBranchSetting.Default:
            default:
                return candidates[Random.Range(0, candidates.Count)];

        }
    }

    private async UniTask<bool> TryProcessNextRegionAsync(List<RegionData> candidateRegions)
    {
        // [디버깅 로그 추가] 현재 내가 가진 열쇠 목록 출력
        Debug.Log($"[StoryGen] 현재 보유 키: {string.Join(", ", _availableKeys)}");

        // [디버깅 로그 추가] 후보 지역들이 열리는지 검사
        foreach (var region in candidateRegions)
        {
            bool isOpen = region.IsUnlockable(_availableKeys);
            Debug.Log($"[StoryGen] 후보 지역 '{region.RegionName}' 잠금 해제 가능? -> {isOpen}");
        }

        // 1. 현재 키로 갈 수 있는 Region 필터링
        var unlockableRegions = candidateRegions
            .Where(region => region.IsUnlockable(_availableKeys))
            .ToList();

        if (unlockableRegions.Count == 0)
        {
            return false; // 갈 수 있는 곳이 없음
        }

        // 2. 랜덤 선택
        var selectedRegion = unlockableRegions[Random.Range(0, unlockableRegions.Count)];

        // 3. 부모 노드 선정
        Node parentNode = PickParentNode();
        if (parentNode == null) return false; // 방어 코드

        // 4. 노드 생성 및 연결
        var selectedNode = new Node
        {
            RegionData = selectedRegion,
            Depth = parentNode.Depth + 1
        };

        _storyResult.Nodes.Add(selectedNode);
        _storyResult.NodeConnections.Add(new NodeConnection(parentNode, selectedNode));

        // 5. 열쇠 획득 (중복 방지)
        if (selectedRegion.GivenKeyIDs != null)
        {
            foreach (var keyID in selectedRegion.GivenKeyIDs)
            {
                if (!_availableKeys.Contains(keyID))
                {
                    _availableKeys.Add(keyID);
                    // Debug.Log($"Key Obtained: {keyID}"); // 필요시 주석 해제
                }
            }
        }

        // 6. 후보 목록에서 제거 (중요!)
        candidateRegions.Remove(selectedRegion);

        // 7. 시각화 딜레이
        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);

        return true;
    }
    #endregion

    #region Phase 1: Fixed Region Generation

    private async UniTask GenerateFixedRegionsAsync()
    {
        if (_storyData.FixedRegions == null)
            return;

        var candidateRegions = _storyData.FixedRegions.ToList();

        _availableKeys = new();

        var startRegionData = _storyData.StartRegion;
        _storyResult.Nodes.Add(new Node
        {
            RegionData = startRegionData,
            Depth = 1
        });

        // 시작 Region의 열쇠 획득
        if (startRegionData.GivenKeyIDs != null)
        {
            foreach (var key in startRegionData.GivenKeyIDs)
            {
                if (!_availableKeys.Contains(key))
                {
                    _availableKeys.Add(key);
                    Debug.Log($"[Start Region] 초기 열쇠 획득: {key}");
                }
            }
        }

        while (candidateRegions.Count > 0)
        {
            // 후보 Region 중에서  갈 수 있는 Region을 골라 배치 후 Key 획득
            bool success = await TryProcessNextRegionAsync(candidateRegions);

            if (!success)
            {
                Debug.LogWarning($"[Phase 1] 더 이상 갈 수 있는 Fixed Region이 없습니다. 남은 후보: {candidateRegions.Count}개");
                Debug.LogWarning($"현재 보유 키: {string.Join(", ", _availableKeys)}");
                break;
            }
        }
    }
    #endregion

    #region Phase2 : Side Region Generation
    private async UniTask GenerateSideRegionsAsync()
    {
        // 1. 메인 스토리 구역의 일정 비율만큼 사이드 구역 배치
        int maxSideRegions = Mathf.RoundToInt(_storyData.FixedRegions.Count*_storyData.SideRegionRatio);
        
        var candidateSideRegions = _storyData.SideRegions.ToList();

        int placedCount = 0;

        for (int i = 0 ; i < maxSideRegions ; i ++)
        {
            if (placedCount >= maxSideRegions) break;

            bool success = await TryProcessNextRegionAsync(candidateSideRegions);

            if (success)
            {
                placedCount++;
            }
            
        }
    }


    #endregion

    #region Phase 3 : Loop Apply
    private async UniTask ApplyWorldLoopAsync()
    {
        // 1. 설정 확인 (Never면 안 함)
        if (_worldSettings.WorldLoop == WorldLoopSetting.Never) return;

        // 그래프의 인접 정보 갱신
        _storyResult.RebuildAdjacency();

        // 2. 확률 체크 (Don't Starve는 기본 50% 또는 설정값)
        if (Random.value > _worldSettings.GetLoopMultiplier()) return;

        // 3. 시작 노드와 끝 노드 찾기
        Node startNode = _storyResult.Nodes.FirstOrDefault(n => n.Depth == 1);
        Node endNode = _storyResult.Nodes.OrderByDescending(n => n.Depth).FirstOrDefault();

        if (startNode != null && endNode != null && startNode != endNode)
        {
            // 4. 연결 생성!
            // RegionGenerator가 이 연결 정보를 보고 나중에 두 구역 사이에 다리(Bridge)를 놓게 됩니다.
            _storyResult.CreateConnection(endNode, startNode);
            _storyResult.IsLooped = true;

            Debug.Log($"[StoryGenerator] Macro Loop Created: {endNode.RegionData.RegionName} -> {startNode.RegionData.RegionName}");
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }
    #endregion
}
