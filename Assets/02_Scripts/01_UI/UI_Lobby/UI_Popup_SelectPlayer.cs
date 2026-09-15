using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 플레이어 설정 팝업. 이름과 캐릭터 성별을 고르고 확정 버튼을 누르면 콜백을 실행한다.
///
/// 방 생성/참가 직전에 끼워 넣는 확인 단계로 사용한다:
///   - 호스트: UI_Popup_MakeRoom의 방 생성 버튼 → 이 팝업 → 확정 시 방 생성
///   - 클라이언트: UI_Popup_EnterRoom의 방 클릭 → 이 팝업 → 확정 시 방 참가
///
/// 확정 시 선택 값을 Main.Network에 먼저 기록한다.
/// 방 참가 후 NetworkPlayerData가 이 값을 호스트로 올려 보내고, 호스트는 그 성별로 캐릭터를 스폰한다.
/// </summary>
public class UI_Popup_SelectPlayer : UI_Popup
{
    #region Fields
    [Header("이름")]
    [SerializeField] private TMP_InputField PlayerNameInput;

    [Header("성별 버튼")]
    [SerializeField] private UI_Button FemaleButton;
    [SerializeField] private UI_Button MaleButton;

    [Header("하단 버튼")]
    [SerializeField] private UI_Button ConfirmButton;

    // 선택 표시용 색
    private static readonly Color SelectedColor = Color.white;
    private static readonly Color UnselectedColor = new(0.5f, 0.5f, 0.5f, 1f);

    // 선택된 캐릭터 (PlayerCharacter 값)
    private int _characterIndex = (int)PlayerCharacter.Female;

    // 확정 시 실행할 동작 (방 생성 / 방 참가)
    private Action _onConfirm;

    // 확정 중복 클릭 방지
    private bool _confirmed;
    #endregion

    /// <summary>확정 버튼을 눌렀을 때 실행할 동작을 등록한다. (ShowPopup 직후 호출)</summary>
    public void SetOnConfirm(Action onConfirm)
    {
        _onConfirm = onConfirm;
    }

    protected override void Start()
    {
        base.Start();

        // 이전에 설정한 값이 있으면 그대로 보여준다
        if (Main.Network != null)
        {
            _characterIndex = Main.Network.LocalCharacterIndex;
            if (PlayerNameInput != null) PlayerNameInput.text = Main.Network.LocalPlayerName;
        }

        // OnButtonUp은 SetDownUpButton()으로 PointerUp EventTrigger를 등록해야 발생한다.
        if (FemaleButton != null)
        {
            FemaleButton.SetDownUpButton();
            FemaleButton.OnButtonUp += () => OnSelectCharacter((int)PlayerCharacter.Female);
        }

        if (MaleButton != null)
        {
            MaleButton.SetDownUpButton();
            MaleButton.OnButtonUp += () => OnSelectCharacter((int)PlayerCharacter.Male);
        }

        if (ConfirmButton != null)
        {
            ConfirmButton.SetDownUpButton();
            ConfirmButton.OnButtonUp += OnConfirm;
        }

        RefreshCharacterButtons();
    }

    // 성별 선택
    private void OnSelectCharacter(int characterIndex)
    {
        _characterIndex = characterIndex;
        RefreshCharacterButtons();
    }

    // 선택된 성별 버튼만 밝게 표시
    private void RefreshCharacterButtons()
    {
        if (FemaleButton != null)
            FemaleButton.SetColor(_characterIndex == (int)PlayerCharacter.Female ? SelectedColor : UnselectedColor);
        if (MaleButton != null)
            MaleButton.SetColor(_characterIndex == (int)PlayerCharacter.Male ? SelectedColor : UnselectedColor);
    }

    // 확정 → 선택 값 기록 후 등록된 동작(방 생성/참가) 실행
    private void OnConfirm()
    {
        if (_confirmed) return;
        _confirmed = true;

        if (Main.Network != null)
        {
            // 빈 이름은 SetLocalPlayerName 내부에서 무시되어 기존 이름이 유지된다
            if (PlayerNameInput != null) Main.Network.SetLocalPlayerName(PlayerNameInput.text.Trim());
            Main.Network.SetLocalCharacter(_characterIndex);
        }

        Action onConfirm = _onConfirm;
        _onConfirm = null;

        Close();
        onConfirm?.Invoke();
    }
}
