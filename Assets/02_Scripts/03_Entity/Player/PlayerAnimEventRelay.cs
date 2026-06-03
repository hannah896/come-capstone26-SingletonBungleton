using UnityEngine;

/// <summary>
/// 바디 애니메이션 클립의 Animation Event를 플레이어 로직으로 전달하는 릴레이.
/// Animation Event는 Animator와 동일한 GameObject에 붙은 컴포넌트의 메서드만 호출할 수 있으므로,
/// 이 컴포넌트는 Animator가 있는 캐릭터 모델 GameObject에 부착해야 한다.
/// </summary>
public class PlayerAnimEventRelay : MonoBehaviour
{
    [SerializeField] private Player player;

    private void Awake()
    {
        if (player == null) player = GetComponentInParent<Player>();
    }

    /// <summary>
    /// 도구 사용 액션(벌목/채굴/땅파기 등) 바디 클립의 타격 프레임 Animation Event가 호출.
    /// 1인칭 도구 스윙을 바디 타격 순간과 동기화하여 재생한다.
    /// </summary>
    public void PlayerToolUse()
    {
        player?.FPCameraController?.PlayChopSwing();
    }

    /// <summary>
    /// 액션(채굴/벌목 등) 클립의 Stop 모션 프레임 Animation Event가 호출.
    /// 도구 데미지 적용 + 반복/종료 판정을 현재 액션 상태로 전달한다.
    /// </summary>
    public void OnActionStop()
    {
        player?.OnActionAnimationEvent();
    }
}
