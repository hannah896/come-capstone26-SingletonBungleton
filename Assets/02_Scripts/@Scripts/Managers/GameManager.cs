/// <summary>
/// 인 게임 내의 낮밤을 관리.
/// 나중에 낮밤 구현하실때 여기 구현해주세요~~
/// 내부 내용은 대충 무시해주세요~~
/// </summary>
public class GameManager : ContentManager
{
    #region Properties

    // 게임 시간 관리
    public GameTime Time { get; } = new();

    #endregion

    #region Game State Check

    /// <summary>
    /// 실패 조건을 확인합니다.
    /// </summary>
    public bool CheckFail()
    {
        GameScene.GameState = GameState.Failed;
        return true;
    }

    #endregion

    #region Cleanup

    public override void Clear()
    {
        base.Clear();
        Time.Clear();
    }

    #endregion
}
