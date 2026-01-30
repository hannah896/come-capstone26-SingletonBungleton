using UnityEngine;
using UnityEngine.UI;

public class UI_Canvas : UI_Base
{
    protected Canvas _canvas;
    protected CanvasScaler _scaler;

    public override bool Initialize() {
        if (!base.Initialize()) return false;
        _canvas = this.GetComponent<Canvas>();
        _scaler = this.GetComponent<CanvasScaler>();
        SetCanvas();
        return true;
    }

    protected virtual void SetCanvas() {
        // _canvas.overrideSorting = true;
        // _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        // _scaler.referenceResolution = Main.Screen.ReferenceResolution;
    }
    
}