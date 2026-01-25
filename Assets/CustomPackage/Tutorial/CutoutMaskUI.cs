using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

public class CutoutMaskUI : Image
{
    private static readonly int StencilComp = Shader.PropertyToID("_StencilComp");
    private Material _material;
    private bool _isInitialized = false;

    private void Init()
    {
        if (_isInitialized) return;
        _material = new Material(base.materialForRendering);
        _material.SetInt(StencilComp, (int)CompareFunction.NotEqual);
        _isInitialized = true;
    }
    public override Material materialForRendering
    {
        get
        {
            Init();
            return _material;
        }
    }
}
