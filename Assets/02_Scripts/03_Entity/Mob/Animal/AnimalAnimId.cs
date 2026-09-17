/// <summary>
/// 동물 애니메이션 식별자. 호스트가 재생한 애니를 클라가 그대로 재현하기 위해 네트워크로 복제된다.
///
/// byte로 복제되므로 값을 바꾸거나 중간을 지우지 말 것. 추가는 뒤에만 한다.
/// </summary>
public enum AnimalAnimId : byte
{
    None = 0,
    Idle = 1,
    Walk = 2,
    Run = 3,
}
