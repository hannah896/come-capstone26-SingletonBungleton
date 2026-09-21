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
    /// 도구 사용 액션(벌목/채굴 등) 바디 클립의 스윙 시작 프레임 Animation Event가 호출.
    /// 도구 스윙과 타격 타이머는 액션 상태 진입 시 이미 시작되므로, 여기서는 클립이 실제로
    /// 재생되기 시작한 시점에 맞춰 타이밍을 한 번 보정해 준다.
    /// </summary>
    public void PlayerToolUse()
    {
        player?.OnToolSwingAnimationEvent();
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
