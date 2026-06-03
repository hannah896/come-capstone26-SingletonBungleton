using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 설정 팝업. 오디오/입력 두 탭으로 구성됩니다.
/// - 탭: Audio 버튼 → UI_AudioSetting, Input 버튼 → UI_InputSetting (기본은 오디오 탭)
/// - 오디오 볼륨: JSAM(AudioManager)이 PlayerPrefs로 자동 저장/로드 (Extensions 경유)
/// - 마우스 감도: PlayerPrefs에 저장되어 다음 실행 때 카메라가 로드해 계속 사용
/// - 뒤쪽 클릭 차단: 풀스크린 BG 오브젝트. 게임씬이면 열릴 때 플레이어 입력 차단, 닫힐 때 복구
/// </summary>
public class UI_Popup_SettingUI : UI_Popup
{
    #region Constants

    // 마우스 감도 슬라이더 범위/기본값
    private const float DefaultSensitivity = 2f;
    private const float MinSensitivity = 0.5f;
    private const float MaxSensitivity = 10f;

    #endregion

    #region Fields

    [SerializeField] private UI_Button UI_Button_CloseBtn;

    // 탭 버튼
    [SerializeField] private UI_Button AudioTabButton;
    [SerializeField] private UI_Button InputTabButton;

    // 탭 패널
    [SerializeField] private GameObject UI_AudioSetting;
    [SerializeField] private GameObject UI_InputSetting;

    // 오디오 볼륨 슬라이더
    [SerializeField] private Slider UI_Slider_Master;
    [SerializeField] private Slider UI_Slider_BGM;
    [SerializeField] private Slider UI_Slider_SFX;

    // 마우스 감도 슬라이더
    [SerializeField] private Slider UI_Slider_MouseSensitivity;

    // 게임씬에서 플레이어 입력을 막았는지 여부
    private bool _playerInputBlocked;

    #endregion

    #region Initialize

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        // 탭 버튼
        if (AudioTabButton == null)
            AudioTabButton = gameObject.FindChild<UI_Button>("Audio");
        if (InputTabButton == null)
            InputTabButton = gameObject.FindChild<UI_Button>("Input");
        AudioTabButton?.SetEvent(ShowAudioTab);
        InputTabButton?.SetEvent(ShowInputTab);

        // 탭 패널
        if (UI_AudioSetting == null)
            UI_AudioSetting = gameObject.FindChild("UI_AudioSetting");
        if (UI_InputSetting == null)
            UI_InputSetting = gameObject.FindChild("UI_InputSetting");

        // 오디오 볼륨 슬라이더 (0~1)
        if (UI_Slider_Master == null)
            UI_Slider_Master = gameObject.FindChild<Slider>("UI_Slider_Master");
        SetupSlider(UI_Slider_Master, 0f, 1f, Extensions.GetMasterVolume(), OnMasterChanged);

        if (UI_Slider_BGM == null)
            UI_Slider_BGM = gameObject.FindChild<Slider>("UI_Slider_BGM");
        SetupSlider(UI_Slider_BGM, 0f, 1f, Extensions.GetBGMVolume(), OnBGMChanged);

        if (UI_Slider_SFX == null)
            UI_Slider_SFX = gameObject.FindChild<Slider>("UI_Slider_SFX");
        SetupSlider(UI_Slider_SFX, 0f, 1f, Extensions.GetSFXVolume(), OnSFXChanged);

        // 마우스 감도 슬라이더 (PlayerPrefs 저장값 → 카메라 반영)
        if (UI_Slider_MouseSensitivity == null)
            UI_Slider_MouseSensitivity = gameObject.FindChild<Slider>("UI_Slider_MouseSensitivity");
        float sens = PlayerPrefs.GetFloat(PlayerFirstPersonCameraController.MouseSensitivityKey, DefaultSensitivity);
        SetupSlider(UI_Slider_MouseSensitivity, MinSensitivity, MaxSensitivity, sens, OnMouseSensitivityChanged);
        ApplyMouseSensitivity(sens);

        // 닫기 버튼
        if (UI_Button_CloseBtn == null)
            UI_Button_CloseBtn = gameObject.FindChild<UI_Button>("UI_Button_CloseBtn");
        UI_Button_CloseBtn.SetEvent(Close);

        // 기본은 오디오 탭
        ShowAudioTab();

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

        // 차단했던 입력 복구 후 설정 저장
        RestorePlayerInput();
        PlayerPrefs.Save();   // 마우스 감도 등 PlayerPrefs 값 즉시 저장
        Extensions.SaveVolume();

        base.Close();
    }

    #endregion

    #region Tabs

    // 오디오 탭만 표시
    private void ShowAudioTab()
    {
        if (UI_AudioSetting != null) UI_AudioSetting.SetActive(true);
        if (UI_InputSetting != null) UI_InputSetting.SetActive(false);
    }

    // 입력 탭만 표시
    private void ShowInputTab()
    {
        if (UI_AudioSetting != null) UI_AudioSetting.SetActive(false);
        if (UI_InputSetting != null) UI_InputSetting.SetActive(true);
    }

    #endregion

    #region Slider Setup

    // 슬라이더 범위/초기값을 콜백 없이 맞추고 변경 콜백을 등록한다.
    private void SetupSlider(Slider slider, float min, float max, float initialValue, UnityAction<float> onChanged)
    {
        if (slider == null) return;

        slider.minValue = min;
        slider.maxValue = max;
        slider.SetValueWithoutNotify(Mathf.Clamp(initialValue, min, max));

        slider.onValueChanged.RemoveListener(onChanged);
        slider.onValueChanged.AddListener(onChanged);
    }

    #endregion

    #region Slider Events

    private void OnMasterChanged(float value) => Extensions.SetMasterVolume(value);
    private void OnBGMChanged(float value) => Extensions.SetBGMVolume(value);
    private void OnSFXChanged(float value) => Extensions.SetSFXVolume(value);

    private void OnMouseSensitivityChanged(float value)
    {
        ApplyMouseSensitivity(value);
        PlayerPrefs.SetFloat(PlayerFirstPersonCameraController.MouseSensitivityKey, value);
    }

    // 현재 활성 플레이어 카메라에 감도를 즉시 반영한다. (없으면 PlayerPrefs 값만 유지)
    private void ApplyMouseSensitivity(float value)
    {
        var cam = Object.FindFirstObjectByType<PlayerFirstPersonCameraController>();
        cam?.SetMouseSensitivity(value);
    }

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
