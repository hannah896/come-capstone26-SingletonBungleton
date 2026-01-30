using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// 게임의 입력(UI 클릭 제외)을 총괄해주는 매니저
/// </summary>
public class InputManager : ContentManager
{
    #region Field

    private InputController _inputController = new();
    
    // 현재 적용된 입력 상태들
    private InputActionType _currentInputActionType = InputActionType.None;

    // Action들을 추가로 넣을 때 아래에 추가해서 생성.
    private readonly InputActions_CameraMove _cameraMove = new();
    private readonly InputActions_CameraZoom _cameraZoom = new();

    private Dictionary<InputActionType, InputActions> _inputActions = new();

    #endregion

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();
        
        _inputController.Initialize();

        // Field에 등록해둔 InputAction들을 읽고 초기화 및 inputActions에 캐싱해둠.
        foreach (FieldInfo fieldInfo in typeof(InputManager).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
        {
            InputActions actions = fieldInfo.GetValue(Main.Input) as InputActions;
            if (actions == null) continue;
            actions.Init(_inputController);
            _inputActions[actions.GetInputActionType()] = actions;
        }
    }

    // 모든 인풋 액션을 덮어쓰는 함수
    public void SetInputActions(InputActionType setType)
    {
        if (_currentInputActionType == setType) return;

        // 1. 변화가 필요한 비트만 추출 (XOR)
        InputActionType changed = _currentInputActionType ^ setType;

        // 2. 바뀐 비트들 중에서 전체 리스트를 순회하며 처리
        foreach (var kvp in _inputActions)
        {
            InputActionType actionType = kvp.Key;
            InputActions action = kvp.Value;

            // 이 액션이 변화가 필요한 대상인가?
            if ((changed & actionType) != 0)
            {
                // 변화가 필요한데 nextType에 포함되어 있다면 -> Connect
                if ((setType & actionType) != 0)
                {
                    action.ConnectInputController();
                }
                // 변화가 필요한데 nextType에 없다면 (기존엔 있었다는 뜻) -> Disconnect
                else
                {
                    action.DisconnectInputController();
                }
            }
        }
        
        _currentInputActionType = setType;
    }

    // 해당 인풋 액션을 추가하는 함수
    public void AddInputAction(InputActionType addType)
    {
        // 1. 이미 가지고 있는 비트를 제외하고 '새로 추가될 비트'만 계산
        InputActionType bitsToAdd = addType & ~_currentInputActionType;
        if (bitsToAdd == InputActionType.None) return;

        // 2. 등록된 모든 액션을 순회하며 새로 추가된 비트에 해당하는 것만 실행
        foreach (var kvp in _inputActions)
        {
            if ((bitsToAdd & kvp.Key) != 0)
            {
                kvp.Value.ConnectInputController();
            }
        }

        _currentInputActionType |= bitsToAdd;
    }

    // 해당 인풋 액션을 제거하는 함수
    public void RemoveInputAction(InputActionType removeType)
    {
        // 1. 현재 가지고 있는 비트 중에서 '삭제할 비트'만 추출
        InputActionType bitsToRemove = removeType & _currentInputActionType;
        if (bitsToRemove == InputActionType.None) return;

        // 2. 삭제 대상 비트에 해당하는 액션만 연결 해제
        foreach (var kvp in _inputActions)
        {
            if ((bitsToRemove & kvp.Key) != 0)
            {
                kvp.Value.DisconnectInputController();
            }
        }

        _currentInputActionType &= ~bitsToRemove;
    }
    
    // 해당 인풋 액션을 토글(On <-> Off) 형태로 전환하는 함수
    public void ToggleInputAction(InputActionType toggleType)
    {
        if (toggleType == InputActionType.None) return;

        // 1. 등록된 모든 액션을 순회
        foreach (var kvp in _inputActions)
        {
            // 이 액션이 토글 대상에 포함되는지 확인
            if ((toggleType & kvp.Key) != 0)
            {
                // 현재 상태 확인: 켜져 있으면 끄고, 꺼져 있으면 킴
                bool isCurrentlyActive = (_currentInputActionType & kvp.Key) != 0;

                if (isCurrentlyActive)
                {
                    kvp.Value.DisconnectInputController();
                }
                else
                {
                    kvp.Value.ConnectInputController();
                }
            }
        }

        // 2. 현재 상태 비트마스크 반전 (XOR 연산)
        // XOR(^)은 서로 다르면 1, 같으면 0이 되므로 켜진 비트는 꺼지고, 꺼진 비트는 켜짐
        _currentInputActionType ^= toggleType;
    }

    // 해당 인풋 액션이 활성화되어있는지 확인하는 함수
    public bool IsActiveAction(InputActionType checkType)
    {
        if (checkType == InputActionType.None) return false;
        return (_currentInputActionType & checkType) == checkType;
    }
}

[Flags]
public enum InputActionType
{
    None = 0,
    CameraMove = 1 << 0,
    CameraZoom = 1 << 1,
    
    GameScenePlay = CameraMove | CameraZoom,

}