/// <summary>
/// 인 게임 내의 낮밤을 관리.
/// 나중에 낮밤 구현하실때 여기 구현해주세요~~
/// 내부 내용은 대충 무시해주세요~~
/// </summary>
public class GameManager : ContentManager
{
    #region Properties

    // 현재 보드
    public Board Current { get; private set; }

    // 게임 시간 관리
    public GameTime Time { get; } = new();

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
    /// 클리어 조건을 확인합니다.
    /// </summary>
    public void CheckClear()
    {
        if (Current.IsAllClear())
        {
            GameScene.GameState = GameState.Success;
        }
    }

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
        Time.Clear();
    }

    #endregion
}
