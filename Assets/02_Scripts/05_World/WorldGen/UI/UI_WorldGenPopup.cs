using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

public class UI_WorldGenPopup : UI_Popup
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

        //TODO: 임시 싱글톤을 통해 호출, 나중에 리팩토링 필요.
        if (WorldGenManager.Instance != null)
        {
            WorldGenManager.Instance.GenerateWorldFromUI(branch, loop).Forget();
        }
        Close();
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
