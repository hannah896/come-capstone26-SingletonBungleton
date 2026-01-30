using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class UI_GameHUD_Bot : UI_Base {

    #region Const

    private const int EditorOpenClickCount = 20;
    
  #endregion
    
    #region Fields

    private GameScene _scene;

    private int _clickEditorOpenCount = 0;
    private bool _isActiveGrid = false;

    #endregion

    #region Initialize

    public override bool Initialize() {
        if (!base.Initialize()) return false;
        
        return true;
    }

    public void Set() {
        Initialize();

        _scene = (Main.Scene.Current as GameScene);
    }

    #endregion

    #region Events
    
    #endregion
    
}