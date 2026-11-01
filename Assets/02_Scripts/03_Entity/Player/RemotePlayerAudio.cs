using UnityEngine;

/// <summary>
/// 원격 캐릭터의 발소리·착지음.
///
/// 원격 캐릭터는 상태 머신(PlayerWalkState/PlayerRunState/PlayerAirState)이 돌지 않아 발소리가 나지 않는다.
/// 대신 NetworkPlayerSync가 복제해 넣은 애니메이터 Bool(Walk/Run/Jump)을 보고, 로컬과 같은 주기로 그 위치에서 3D로 재생한다.
/// 네트워크로 따로 보내는 것은 없다. NetworkPlayerSync가 원격 캐릭터에만 런타임에 붙인다.
/// </summary>
public class RemotePlayerAudio : MonoBehaviour
{
    private static readonly int s_walkHash = Animator.StringToHash("Walk");
    private static readonly int s_runHash = Animator.StringToHash("Run");
    private static readonly int s_jumpHash = Animator.StringToHash("Jump");

    private Animator _animator;
    private float _footstepTimer;
    private bool _footstepToggle;
    private bool _wasWalking;
    private bool _wasRunning;
    private bool _wasJumping;

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>(true);
    }

    private void OnEnable()
    {
        Main.Loop.OnUpdate -= OnUpdate; // 재활성화 시 중복 구독 방지
        Main.Loop.OnUpdate += OnUpdate;
    }

    // 구독 해제는 OnDisable이 아니라 파괴 시점에만 수행한다.
    private void OnDestroy()
    {
        if (Main.Loop != null)
            Main.Loop.OnUpdate -= OnUpdate;
    }

    private void OnUpdate(float deltaTime)
    {
        if (!isActiveAndEnabled || _animator == null || _animator.runtimeAnimatorController == null) return;

        bool walking = _animator.GetBool(s_walkHash);
        bool running = _animator.GetBool(s_runHash);
        bool jumping = _animator.GetBool(s_jumpHash);

        // 공중 → 지상 (PlayerAirState 착지와 같은 시점: Jump가 꺼지고 Idle/Walk/Run으로 돌아감)
        if (_wasJumping && !jumping)
            Extensions.PlaySFXAt(AudioLibrarySounds.Land, transform.position);

        // 로컬 상태는 진입 즉시 첫 발소리를 낸다 — 걷기/달리기가 바뀌면 똑같이 바로 한 번 낸다
        if (walking != _wasWalking || running != _wasRunning)
            _footstepTimer = PlayerWalkState.FootstepInterval;

        _wasWalking = walking;
        _wasRunning = running;
        _wasJumping = jumping;

        if (running)
        {
            _footstepTimer += deltaTime;
            if (_footstepTimer < PlayerRunState.FootstepInterval) return;

            Extensions.PlaySFXAt(_footstepToggle ? AudioLibrarySounds.Walk3 : AudioLibrarySounds.Walk2, transform.position);
            _footstepToggle = !_footstepToggle;
            _footstepTimer = 0f;
        }
        else if (walking)
        {
            _footstepTimer += deltaTime;
            if (_footstepTimer < PlayerWalkState.FootstepInterval) return;

            Extensions.PlaySFXAt(AudioLibrarySounds.Walk1, transform.position);
            _footstepTimer = 0f;
        }
    }
}
