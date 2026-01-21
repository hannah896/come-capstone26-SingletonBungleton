#if UNITY_EDITOR
using UnityEditor;
#endif

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[System.Serializable]
public class RatioItem
{
    public RectTransform rectTransform;
    public float ratio = 1f;
}

[ExecuteAlways]
[RequireComponent(typeof(HorizontalOrVerticalLayoutGroup))]
public class UI_Utility_AspectRatioLayoutGroup : UIBehaviour
{
    public List<RatioItem> ratios = new List<RatioItem>();

    private HorizontalOrVerticalLayoutGroup _layoutGroup;
    private float _initialSpacing;
    private bool _initialized;
    private Vector2 _lastParentSize;

    protected override void Awake()
    {
        base.Awake();
        EnsureInit();
        UpdateLayout();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        EnsureInit();
        UpdateLayout();
#if UNITY_EDITOR
        EditorApplication.update += OnEditorUpdate;
#endif
    }

    protected override void OnDisable()
    {
        base.OnDisable();
#if UNITY_EDITOR
        EditorApplication.update -= OnEditorUpdate;
#endif
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        UpdateLayout();
    }

#if UNITY_EDITOR
    private void OnEditorUpdate()
    {
        if (Application.isPlaying) return;
        if (this == null) return;

        var parentRect = transform.parent as RectTransform;
        if (parentRect != null)
        {
            if (parentRect.rect.size != _lastParentSize)
            {
                _lastParentSize = parentRect.rect.size;
                UpdateLayout();
            }
        }
    }

    protected override void OnValidate()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall -= EditorUpdate;
            EditorApplication.delayCall += EditorUpdate;
        }
    }

    private void EditorUpdate()
    {
        if (this == null) return;
        EnsureInit();
        UpdateLayout();
    }
#endif

    private void EnsureInit()
    {
        if (_initialized) return;

        _layoutGroup = GetComponent<HorizontalOrVerticalLayoutGroup>();
        if (_layoutGroup == null) return;

        _initialSpacing = _layoutGroup.spacing;

        _layoutGroup.childControlWidth = false;
        _layoutGroup.childControlHeight = false;
        _layoutGroup.childForceExpandWidth = false;
        _layoutGroup.childForceExpandHeight = false;
        _layoutGroup.childScaleWidth = false;
        _layoutGroup.childScaleHeight = false;

        _initialized = true;
    }

    public void UpdateLayout()
    {
        EnsureInit();
        if (_layoutGroup == null || ratios == null || ratios.Count == 0) return;

        var parentRect = transform as RectTransform;
        if (parentRect == null) return;

        bool isHorizontal = _layoutGroup is HorizontalLayoutGroup;

        float pWidth = parentRect.rect.width;
        float pHeight = parentRect.rect.height;

        float baseSize = isHorizontal ? pHeight : pWidth;
        float targetSpace = isHorizontal ? pWidth : pHeight;

        if (baseSize <= 0f || targetSpace <= 0f) return;

        float spacingRatio = _initialSpacing / baseSize;
        float totalWeight = (ratios.Count - 1) * spacingRatio;

        for (int i = 0; i < ratios.Count; i++)
        {
            if (ratios[i] == null) continue;
            totalWeight += Mathf.Max(0f, ratios[i].ratio);
        }

        if (totalWeight <= 0f) return;

        float desiredTotalSize = baseSize * totalWeight;
        float scale = (desiredTotalSize > targetSpace) ? targetSpace / desiredTotalSize : 1f;
        float finalBaseSize = baseSize * scale;

        _layoutGroup.spacing = _initialSpacing * scale;

        for (int i = 0; i < ratios.Count; i++)
        {
            var item = ratios[i];
            if (item == null || item.rectTransform == null) continue;

            float r = Mathf.Max(0f, item.ratio);
            float calculatedSize = (baseSize * r) * scale;

            if (isHorizontal)
            {
                item.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, calculatedSize);
                item.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, finalBaseSize);
            }
            else
            {
                item.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, calculatedSize);
                item.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, finalBaseSize);
            }
        }

        LayoutRebuilder.MarkLayoutForRebuild(parentRect);
    }
}