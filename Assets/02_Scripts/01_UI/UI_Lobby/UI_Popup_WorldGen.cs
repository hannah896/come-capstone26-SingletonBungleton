using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

public class UI_Popup_WorldGen : UI_Popup
{
    #region Fields
    [SerializeField] private UI_Button BranchPrevButton;
    [SerializeField] private UI_Button BranchNextButton;
    [SerializeField] private UI_Text BranchValueText;

    [SerializeField] private UI_Button LoopPrevButton;
    [SerializeField] private UI_Button LoopNextButton;
    [SerializeField] private UI_Text LoopValueText;
    [SerializeField] private UI_Button GenerateButton;
    [SerializeField] private UI_Button CloseButton;
    #endregion
    private static readonly WorldBranchSetting[] BranchOptions =
    {
        WorldBranchSetting.Never,
        WorldBranchSetting.Least,
        WorldBranchSetting.Default,
        WorldBranchSetting.Most,
        WorldBranchSetting.Random
    };

    private static readonly WorldLoopSetting[] LoopOptions =
    {
        WorldLoopSetting.Never,
        WorldLoopSetting.Default,
        WorldLoopSetting.Always
    };

    private int _branchIndex;
    private int _loopIndex;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        InitializeSelections();
        BindEvents();

        return true;
    }

    private void InitializeSelections()
    {
        _branchIndex = Array.IndexOf(BranchOptions, WorldBranchSetting.Default);
        if (_branchIndex < 0) _branchIndex = 0;

        _loopIndex = Array.IndexOf(LoopOptions, WorldLoopSetting.Default);
        if (_loopIndex < 0) _loopIndex = 0;

        RefreshTexts();
    }

    private void BindEvents()
    {
        if (BranchPrevButton != null) BranchPrevButton.SetEvent(OnClickBranchPrev);
        if (BranchNextButton != null) BranchNextButton.SetEvent(OnClickBranchNext);
        if (LoopPrevButton != null) LoopPrevButton.SetEvent(OnClickLoopPrev);
        if (LoopNextButton != null) LoopNextButton.SetEvent(OnClickLoopNext);

        if (GenerateButton != null) GenerateButton.SetEvent(OnClickGenerate);
        if (CloseButton != null) CloseButton.SetEvent(OnClickClose);
    }

    private void OnClickBranchPrev()
    {
        _branchIndex = WrapIndex(_branchIndex - 1, BranchOptions.Length);
        RefreshTexts();
    }

    private void OnClickBranchNext()
    {
        _branchIndex = WrapIndex(_branchIndex + 1, BranchOptions.Length);
        RefreshTexts();
    }

    private void OnClickLoopPrev()
    {
        _loopIndex = WrapIndex(_loopIndex - 1, LoopOptions.Length);
        RefreshTexts();
    }

    private void OnClickLoopNext()
    {
        _loopIndex = WrapIndex(_loopIndex + 1, LoopOptions.Length);
        RefreshTexts();
    }

    private void OnClickGenerate()
    {
        WorldBranchSetting branch = BranchOptions[_branchIndex];
        WorldLoopSetting loop = LoopOptions[_loopIndex];

        // 시드는 여기(로비)에서 확정 → 재현/공유 가능.
        int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        WorldGenRequest.Set(branch, loop, seed);

        Close();

        // 분기는 = "지금 어디서 팝업을 열었나"
        // 이미 WorldGenManager가 있는 씬(에디터/테스트)이면 그 자리에서 생성하고,
        // 없으면 게임씬으로 전환하여 로딩 화면(전환 오버레이) 동안 GameScene이 생성합니다.
        if (WorldGenManager.Instance != null)
        {
            WorldGenRequest.Data req = WorldGenRequest.Consume();
            WorldGenManager.Instance.GenerateWorld(req.Branch, req.Loop, req.Seed, req.Size).Forget();
        }
        else
        {
            // 게임씬 EnterScene 함수에서  WorldGenRequest.HasRequest 체크 → Consume → GenerateWorld 호출됨.
            Extensions.ChangeScene("GameScene");
        }
    }

    private void OnClickClose()
    {
        Close();
    }

    private void RefreshTexts()
    {
        if (BranchValueText != null) BranchValueText.Text = BranchOptions[_branchIndex].ToString();
        if (LoopValueText != null) LoopValueText.Text = LoopOptions[_loopIndex].ToString();
    }
    private static int WrapIndex(int index, int length)
    {
        if (length <= 0) return 0;
        if (index < 0) return length - 1;
        if (index >= length) return 0;
        return index;
    }
}
