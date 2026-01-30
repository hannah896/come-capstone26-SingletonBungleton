
/// <summary>
/// 보드형식을 가진 게임의 데이터들을 담는 보드 매니저. 
/// 게임에 그리드를 만들어서 특정 간격으로 데이터를 심는 역할을 맡는다. 
/// 좌표에 데이터를 심고, 오브젝트를 생성하는 역할을 한다.
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

    public bool CheckFail() {
        GameScene.GameState = GameState.Failed;
        return true;
    }
    
}