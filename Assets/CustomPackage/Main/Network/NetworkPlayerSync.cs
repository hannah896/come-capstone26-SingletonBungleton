#if PHOTON_FUSION
using Fusion;
using UnityEngine;

/// <summary>
/// 플레이어 캐릭터 위치/회전 동기화 (Host 모드).
/// 플레이어 프리팹(NetworkObject 포함)에 부착한다.
///
/// 동작 방식:
/// - 입력 권한 클라이언트: 로컬 시뮬레이션(HFSM/PlayerMotor)이 transform을 직접 움직이고,
///   그 결과를 NetworkManager.OnInput이 Fusion Input(CharacterPosition/Yaw)으로 호스트에 보고한다.
/// - 호스트: 보고받은 값을 transform과 Networked 프로퍼티에 확정 기록한다.
///   호스트 자신의 캐릭터는 로컬 시뮬레이션 값을 그대로 기록한다.
/// - 프록시(그 외 모든 피어): Networked 값을 Render에서 보간해 표시한다.
/// </summary>
public class NetworkPlayerSync : NetworkBehaviour
{
    #region Networked Properties

    // 호스트가 확정한 위치/Yaw (프록시가 보간 대상)
    [Networked] private Vector3 NetPosition { get; set; }
    [Networked] private float NetYaw { get; set; }

    #endregion

    #region Fields

    // 프록시 보간 속도 (클수록 빠르게 따라붙음)
    [SerializeField] private float _lerpSpeed = 12f;

    // 이 거리 이상 벌어지면 보간 없이 스냅 (텔레포트/스폰 직후 대비)
    [SerializeField] private float _snapDistance = 8f;

    #endregion

    #region Lifecycle

    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            // 자기 캐릭터: OnInput 수집 대상으로 등록 (transform을 읽어 호스트로 보고)
            Main.Network?.RegisterLocalCharacter(Object);
        }

        if (Object.HasStateAuthority)
        {
            // 호스트: 스폰 위치로 초기화
            NetPosition = transform.position;
            NetYaw = transform.eulerAngles.y;
        }
        else if (!Object.HasInputAuthority)
        {
            // 프록시: 첫 프레임은 확정 값으로 즉시 스냅
            transform.SetPositionAndRotation(NetPosition, Quaternion.Euler(0f, NetYaw, 0f));
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Object.HasInputAuthority)
        {
            Main.Network?.UnregisterLocalCharacter(Object);
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 상태 확정은 호스트(StateAuthority) 전담
        if (!Object.HasStateAuthority) return;

        if (Object.HasInputAuthority)
        {
            // 호스트 자신의 캐릭터: 로컬 시뮬레이션이 이미 transform을 움직였다 → 그대로 확정
            NetPosition = transform.position;
            NetYaw = transform.eulerAngles.y;
        }
        else if (GetInput(out NetworkInputData input) && input.HasCharacterState)
        {
            // 원격 클라이언트 캐릭터: 클라가 보고한 로컬 시뮬레이션 결과를 확정
            transform.SetPositionAndRotation(
                input.CharacterPosition, Quaternion.Euler(0f, input.CharacterYaw, 0f));
            NetPosition = input.CharacterPosition;
            NetYaw = input.CharacterYaw;
        }
    }

    public override void Render()
    {
        // 자기 캐릭터(입력 권한)는 로컬 시뮬레이션이 그린다 — 복제 값으로 덮지 않는다
        if (Object.HasInputAuthority) return;

        // 호스트가 보는 원격 캐릭터는 FixedUpdateNetwork에서 이미 적용됨
        if (Object.HasStateAuthority) return;

        // 프록시: 확정 값으로 부드럽게 보간
        Vector3 targetPos = NetPosition;
        if ((transform.position - targetPos).sqrMagnitude > _snapDistance * _snapDistance)
        {
            transform.SetPositionAndRotation(targetPos, Quaternion.Euler(0f, NetYaw, 0f));
            return;
        }

        float t = 1f - Mathf.Exp(-_lerpSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPos, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, NetYaw, 0f), t);
    }

    #endregion
}
#endif
