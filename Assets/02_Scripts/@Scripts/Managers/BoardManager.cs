
/// <summary>
/// 보드형식을 가진 게임의 데이터들을 담는 보드 매니저.
/// 게임 보드의 생성과 상태를 관리합니다.
/// </summary>
public class BoardManager : ContentManager
{
    #region Constants

    // 채우기 비율 높이
    public const float FillRateHeight = 1f;

    #endregion

    #region Properties

    // 현재 보드
    public Board Current { get; private set; }

    #endregion

    #region Board Generation

    /// <summary>
    /// 스테이지 데이터를 기반으로 보드를 생성합니다.
    /// </summary>
    public void GenerateBoard(StageData data)
    {
        Current = new(data);
    }

    /// <summary>
    /// 보드 오브젝트를 생성합니다.
    /// </summary>
    public void GenerateBoardObject()
    {
        Current?.GenerateObject();
    }

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
        Current = null;
    }

    #endregion
}
