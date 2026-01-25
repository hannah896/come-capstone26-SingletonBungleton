using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UI_GameHUD_Top : UI_Base {

    #region Const

    private const int EditorOpenClickCount = 20;
    
  #endregion
    
    #region Fields

    private UI_DifficultyImage[] _difficultyImages;
    private UI_Image[] _imgHeart = new UI_Image[3];
    private UI_Text _txtStage;
    private UI_Text _txtDifficulty;
    private UI_Editor _editor;
    private GameScene _scene;

    private RectTransform _rectHorRight;
    private Sequence _seqHint;

    private int _clickEditorOpenCount = 0;

    #endregion

    protected void OnDisable()
    {
        GameEvents.OnChangeHeart -= SetHeartCount;
        GameEvents.OnChangeHeart -= CheckGameFailed;
    }

    #region Initialize

    public override bool Initialize() {
        if (!base.Initialize()) return false;
        
        _txtStage = gameObject.FindChild<UI_Text>("Txt_Stage");
        _txtDifficulty = gameObject.FindChild<UI_Text>("Txt_Difficulty");
        _editor = gameObject.FindChild<UI_Editor>("UI_Editor");
        _rectHorRight = gameObject.FindChild<RectTransform>("Hor_Right");
        
        for (int i = 0; i < _imgHeart.Length; i++)
        {
            _imgHeart[i] = gameObject.FindChild<UI_Image>($"Img_Heart{i}");
        }

        gameObject.FindChild<UI_Button>("Img_Heart1").SetEvent(_editor.OnClickEditor);
        gameObject.FindChild<UI_Button>("Btn_Retry").SetEvent(OnClickRetry);
        
        GameEvents.OnChangeHeart += SetHeartCount;
        GameEvents.OnChangeHeart += CheckGameFailed;
        
        return true;
    }

    public void Set() {
        Initialize();
        int stage = GameScene.CurrentStage.Index;
        Difficulty difficulty = GameScene.CurrentStage.Difficulty;
        _clickEditorOpenCount = 0;

        _txtStage.Text = $"Stage {stage.ToString()}";
        _txtDifficulty.Text = $"{difficulty.ToString()}";

        _rectHorRight.anchoredPosition = new(_rectHorRight.sizeDelta.x, 0);
        
        SetCloseHint();
        _scene = (Main.Scene.Current as GameScene);
    }

    #endregion

    #region Events

    // 매개변수에 입력된 수 만큼 Heart이미지에 적용
    private void SetHeartCount(int heartCount)
    {
        for (int i = 0; i < _imgHeart.Length; i++)
        {
            Color setColor = i >= heartCount ? Utilities.GetDisableColor : Color.white;
            _imgHeart[i].SetColor(setColor);
        }
    }

    // Heart의 수를 확인하고 GameOver상태인지 체큰
    private void CheckGameFailed(int heartCount)
    {
        if (heartCount > 0) return;
        GameScene.GameState = GameState.Failed;
    }
    
    // Retry 버튼 클릭 시 호출
    private void OnClickRetry() => _scene.RetryGame();

    // Hint 버튼을 HUD에 나타나게 함.
    private void SetOpenHint()
    {
        _rectHorRight.gameObject.SetActive(true);
        _seqHint?.Kill();
        _seqHint = DOTween.Sequence();
        _seqHint.Append(_rectHorRight.DOAnchorPosX(0, 0.5f));
    }

    // Hint 버튼을 HUD에서 사라지게 함.
    public void SetCloseHint()
    {
        _seqHint?.Kill();
        _seqHint = DOTween.Sequence();
        _seqHint.Append(_rectHorRight.DOAnchorPosX(_rectHorRight.sizeDelta.x, 0.5f));
        _seqHint.AppendCallback(() => _rectHorRight.gameObject.SetActive(false));
        _seqHint.AppendInterval(5);
        _seqHint.AppendCallback(SetOpenHint);
    }
    #endregion
    
}