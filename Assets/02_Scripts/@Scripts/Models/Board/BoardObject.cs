public class BoardObject : Entity {

    private Board _board;

    public void Set(Board board) {
        Initialize();
        _board = board;

        this.transform.name = $"Board";
        this.transform.SetParent(null);
        this.transform.position = _board.Center;
    }

}