using UnityEngine;

/// <summary>
/// 플레이어 하위 상태의 기본 클래스
/// </summary>
public abstract class PlayerSubStateBase : SubStateBase<Player>
{
    protected PlayerRootStateMachine Machine { get; private set; }
    protected PlayerInputData Input => Entity.InputData;

    protected PlayerSubStateBase(PlayerRootStateMachine machine) : base(machine.Owner)
    {
        Machine = machine;
    }

    /// <summary>
    /// 현재 Root 상태를 특정 타입으로 캐스팅하여 가져온다.
    /// </summary>
    protected T GetRootState<T>() where T : PlayerRootStateBase
    {
        return Machine.CurrentState as T;
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
