
/// <summary>
/// 보드형식을 가진 게임의 데이터들을 담는 보드 매니저. 
/// </summary>
public class BoardManager : ContentManager {

    #region Const.

    public const float FillRateHeight = 1f;

    #endregion
    
    #region Properties
    
    public Board Current { get; private set; }

    #endregion

    #region Generate

    public void GenerateBoard(StageData data) {
        Current = new(data);
    }

    public void GenerateBoardObject() {
        Current?.GenerateObject();
    }

    public override void Clear() {
        base.Clear();
        Current = null;
    }

    #endregion
    
    public void CheckClear() {
        if (Current.IsAllClear()) {
            GameScene.GameState = GameState.Success;
        }
    }

    public bool CheckFail() {
        GameScene.GameState = GameState.Failed;
        return true;
    }
    
}