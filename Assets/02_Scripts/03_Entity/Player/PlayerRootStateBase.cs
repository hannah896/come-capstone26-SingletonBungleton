using UnityEngine;

/// <summary>
/// 플레이어 루트 상태의 기본 클래스
/// </summary>
public abstract class PlayerRootStateBase : RootStateBase<Player>
{
    protected PlayerRootStateMachine Machine { get; private set; }
    protected PlayerInputData Input => Entity.InputData;
    protected PlayerMotor Motor => Entity.Motor;
    public string CurrentSubStateName => SubStateMachine?.CurrentState?.GetType().Name ?? "None";

    protected PlayerRootStateBase(PlayerRootStateMachine machine) : base(machine.Owner)
    {
        Machine = machine;
    }

    /// <summary>
    /// 카메라 기준 이동 방향을 계산한다.
    /// </summary>
    protected Vector3 CalcCameraRelativeDir(Vector2 input)
    {
        if (input.sqrMagnitude < 0.01f)
            return Vector3.zero;

        Transform cam = Camera.main != null ? Camera.main.transform : null;
        if (cam == null)
            return new Vector3(input.x, 0f, input.y).normalized;

        Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
        return (forward * input.y + right * input.x).normalized;
    }
}
