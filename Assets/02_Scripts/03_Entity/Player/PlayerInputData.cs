using UnityEngine;

/// <summary>
/// 플레이어 입력 데이터를 저장하는 순수 데이터 클래스
/// InputHandler가 기록하고, 각 State가 읽는다.
/// </summary>
public class PlayerInputData
{
    #region 연속 입력 (매 프레임 유지)
    
    public Vector2 MoveInput { get; set; }
    public Vector2 LookInput { get; set; }
    public bool SprintHeld { get; set; }

    // 도구 사용 키를 누르고 있는 동안 true. 벌목/채굴을 누르는 동안 반복하는 데 쓴다.
    public bool ToolUseHeld { get; set; }

    // Ctrl 키(RotateView)를 누르고 있는 동안에만 LookInput으로 시야를 회전시킨다.
    public bool RotateViewHeld { get; set; }

    #endregion

    #region 이벤트 입력 (한 프레임만 유효 - ConsumeEventInputs에서 리셋)

    public bool JumpPressed { get; set; }
    public bool AttackPressed { get; set; }
    public bool CrouchPressed { get; set; }
    public bool InteractPressed { get; set; }
    public bool PickupPressed { get; set; }
    public bool EquipSelectedPressed { get; set; }
    public bool ToolUsePressed { get; set; }
    public bool DropPressed { get; set; }
    public int QuickSlotIndex { get; set; } = -1;
    public int QuickSlotScrollDelta { get; set; }

    #endregion

    #region 파생 프로퍼티
    
    public bool HasMoveInput => MoveInput.sqrMagnitude > 0.01f;

    #endregion

    #region Public Methods

    /// <summary>
    /// 이벤트 입력 플래그를 리셋한다. Player의 OnLoopUpdate 끝에서 호출.
    /// </summary>
    public void ConsumeEventInputs()
    {
        JumpPressed = false;
        AttackPressed = false;
        CrouchPressed = false;
        InteractPressed = false;
        PickupPressed = false;
        EquipSelectedPressed = false;
        ToolUsePressed = false;
        DropPressed = false;
        QuickSlotIndex = -1;
        QuickSlotScrollDelta = 0;
    }

    /// <summary>
    /// 연출 재생 중 입력 차단. LookInput(카메라)과 ToolUseHeld는 유지한다.
    /// ToolUseHeld를 여기서 끄면 액션 중에 손을 떼지 않았는데도 반복이 한 타 만에 끊긴다.
    /// </summary>
    public void SuppressAllInputs()
    {
        MoveInput = Vector2.zero;
        SprintHeld = false;
        RotateViewHeld = false;
        JumpPressed = false;
        AttackPressed = false;
        CrouchPressed = false;
        InteractPressed = false;
        PickupPressed = false;
        EquipSelectedPressed = false;
        ToolUsePressed = false;
        DropPressed = false;
        QuickSlotIndex = -1;
        QuickSlotScrollDelta = 0;
    }

    #endregion
}
