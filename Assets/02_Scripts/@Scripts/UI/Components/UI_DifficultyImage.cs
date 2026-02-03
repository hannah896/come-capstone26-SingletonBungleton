using UnityEngine;

public class UI_DifficultyImage : UI_Image {

    #region Fields

    [SerializeField] private Sprite[] _sprites;

    #endregion

    #region Initialize / Set

    public void Set(Difficulty difficulty) {
        Initialize();
        this.Sprite = _sprites[(int)difficulty];
    }

    #endregion
}