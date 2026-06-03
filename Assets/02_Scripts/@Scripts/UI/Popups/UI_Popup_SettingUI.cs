using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 설정 팝업. 마스터/BGM/SFX 볼륨을 슬라이더로 조정합니다.
/// - 뒤쪽 클릭 차단: 풀스크린 BG 오브젝트(raycastTarget)가 담당합니다.
/// - 열릴 때: 게임씬이면 플레이어 입력을 차단하고, 닫힐 때 복구합니다.
/// - 볼륨 저장/로드: JSAM(AudioManager)이 PlayerPrefs로 자동 처리합니다.
///   설정/조회는 Extensions.SetXxxVolume / GetXxxVolume 경유로만 다룹니다.
/// </summary>
public class UI_Popup_SettingUI : UI_Popup
{
    #region Fields

    [SerializeField] private UI_Button UI_Button_CloseBtn;
    // 볼륨 슬라이더 (부모 오브젝트 All/BGM/SFX 하위의 Slider)
    [SerializeField] private Slider UI_Slider_Master;
    [SerializeField] private Slider UI_Slider_BGM;
    [SerializeField] private Slider UI_Slider_SFX;

    // 게임씬에서 플레이어 입력을 막았는지 여부
    private bool _playerInputBlocked;

    #endregion

    #region Initialize

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        // Master
        if (UI_Slider_Master == null)
            UI_Slider_Master = gameObject.FindChild<Slider>("UI_Slider_Master");
        SetupSlider(UI_Slider_Master, Extensions.GetMasterVolume(), OnMasterChanged);

        // BGM
        if (UI_Slider_BGM == null)
            UI_Slider_BGM = gameObject.FindChild<Slider>("UI_Slider_BGM");
        SetupSlider(UI_Slider_BGM, Extensions.GetBGMVolume(), OnBGMChanged);

        // SFX
        if (UI_Slider_SFX == null)
            UI_Slider_SFX = gameObject.FindChild<Slider>("UI_Slider_SFX");
        SetupSlider(UI_Slider_SFX, Extensions.GetSFXVolume(), OnSFXChanged);

        // 닫기 버튼
        if (UI_Button_CloseBtn == null)
            UI_Button_CloseBtn = gameObject.FindChild<UI_Button>("UI_Button_CloseBtn");
        UI_Button_CloseBtn.SetEvent(Close);

        return true;
    }

    protected override void Start()
    {
        base.Start();

        // 게임씬이면 플레이어 입력 차단 (슬라이더 조작만 허용)
        BlockPlayerInput();
    }

    #endregion

    #region Close

    public override void Close()
    {
        if (_onClose) return;

        // 차단했던 입력 복구 후 볼륨 설정 저장
        RestorePlayerInput();
        Extensions.SaveVolume();

        base.Close();
    }

    #endregion

    #region Slider Setup

    // 슬라이더 초기값을 콜백 없이 맞추고 변경 콜백을 등록한다.
    private void SetupSlider(Slider slider, float initialValue, UnityAction<float> onChanged)
    {
        if (slider == null) return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(Mathf.Clamp01(initialValue));

        slider.onValueChanged.RemoveListener(onChanged);
        slider.onValueChanged.AddListener(onChanged);
    }

    #endregion

    #region Slider Events

    private void OnMasterChanged(float value) => Extensions.SetMasterVolume(value);
    private void OnBGMChanged(float value) => Extensions.SetBGMVolume(value);
    private void OnSFXChanged(float value) => Extensions.SetSFXVolume(value);

    #endregion

    #region Input Block

    // 게임씬에서만 플레이어 입력을 차단한다.
    private void BlockPlayerInput()
    {
        if (Main.Scene?.Current is not GameScene) return;
        if (Main.Input == null) return;
        if (!Main.Input.IsActive<InputActions_PlayerInputHandler>()) return;

        Main.Input.RemoveInput<InputActions_PlayerInputHandler>();
        _playerInputBlocked = true;
    }

    // 차단했던 플레이어 입력을 복구한다.
    private void RestorePlayerInput()
    {
        if (!_playerInputBlocked) return;
        _playerInputBlocked = false;

        if (Main.Input == null) return;
        Main.Input.AddInput<InputActions_PlayerInputHandler>();
    }

    #endregion
}
