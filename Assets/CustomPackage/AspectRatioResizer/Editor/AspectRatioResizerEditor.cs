// Editor/AspectRatioResizerEditor.cs

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AspectRatioResizer))]
public class AspectRatioResizerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 기본 인스펙터 GUI를 그립니다. (baseAxis, targetSize 등)
        DrawDefaultInspector();

        // 타겟 스크립트 참조
        AspectRatioResizer myScript = (AspectRatioResizer)target;

        // 버튼을 그립니다.
        if (GUILayout.Button("Apply Resize"))
        {
            // 버튼 클릭 시 ApplyResize 함수 호출
            myScript.ApplyResize();
        }
    }
}