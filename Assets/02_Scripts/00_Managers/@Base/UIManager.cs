using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public struct PopupData
{
    public UI_Popup Popup;
    public bool IsClickGuard;
    public float ClickGuardAlpha;
    public bool ClickClose;

    public PopupData(UI_Popup popup, bool isClickGuard, float clickGuardAlpha, bool clickClose)
    {
        Popup = popup;
        IsClickGuard = isClickGuard;
        ClickGuardAlpha = clickGuardAlpha;
        ClickClose = isClickGuard && clickClose;
    }
}

public class UIManager
{
    private readonly List<UI_View> _views = new();
    private readonly List<PopupData> _popups = new();

    private const float GUARD_DURATION = 0.2f;
    private const float FADE_DURATION = 0.3f;
    private const float BASE_PANEL_ALPHA = 0.6f;

    private EventSystem _eventSystem;
    private Canvas _viewRoot;
    private Canvas _popupRoot;
    private Image _popupPanel;
    private Button _popupPanelButton;
    private CanvasGroup _sceneGroup;

    private bool _isInit = false;

    public async UniTask Init(Transform ts = null)
    {
        if (_isInit) return;

        InitEventSystem(ts);

        var viewGo = await CreateCanvas("Canvas_View", ts);
        _viewRoot = viewGo.GetComponent<Canvas>();
        _viewRoot.sortingOrder = 10;

        var popupGo = await CreateCanvas("Canvas_Popup", ts);
        _popupRoot = popupGo.GetComponent<Canvas>();
        _popupRoot.sortingOrder = 20;

        _sceneGroup = CreateSceneCanvas("Canvas_Scene", ts);

        _popupPanel = CreatePanel();
        _popupPanel.gameObject.SetActive(false);

        _popupPanelButton = _popupPanel.gameObject.GetOrAddComponent<Button>();
        _popupPanelButton.transition = Selectable.Transition.None;
        _popupPanelButton.onClick.AddListener(OnClickPanel);

        _isInit = true;
    }

    public async UniTask<T> ShowView<T>(string key, CancellationToken ct = default) where T : UI_View
    {
        await UniTask.WaitUntil(() => _isInit, cancellationToken: ct);

        var prefab = await Managers.Resource.LoadAssetAsync<GameObject>(key, AssetCacheType.NonRequired, ct);
        if (prefab == null) return null;

        var go = Object.Instantiate(prefab, _viewRoot.transform);
        if (!go.TryGetComponent<T>(out var comp))
        {
            Object.Destroy(go);
            return null;
        }

        _views.Add(comp);
        return comp;
    }

    public void CloseView(UI_View view)
    {
        if (view == null) return;
        if (_views.Remove(view))
            Object.Destroy(view.gameObject);
    }

    public void ClearAllViews()
    {
        var tempViews = new List<UI_View>(_views);
        foreach (var view in tempViews)
        {
            if (view != null) view.Close();
        }
        _views.Clear();
    }

    public async UniTask<T> ShowPopup<T>(
        string key,
        bool clickGuard = false,
        float clickGuardAlpha = -1,
        bool clickToClose = false,
        CancellationToken ct = default) where T : UI_Popup
    {
        await UniTask.WaitUntil(() => _isInit, cancellationToken: ct);

        var prefab = await Managers.Resource.LoadAssetAsync<GameObject>(key, AssetCacheType.NonRequired, ct);
        if (prefab == null) return null;

        var go = Object.Instantiate(prefab, _popupRoot.transform);
        if (!go.TryGetComponent<T>(out var comp))
        {
            Object.Destroy(go);
            return null;
        }

        float alpha = (clickGuardAlpha < 0) ? BASE_PANEL_ALPHA : clickGuardAlpha;
        _popups.Add(new PopupData(comp, clickGuard, alpha, clickToClose));

        RefreshPanelState();

        return comp;
    }

    public void ClosePopup(UI_Popup popup)
    {
        int idx = _popups.FindIndex(p => p.Popup == popup);
        if (idx >= 0)
        {
            _popups.RemoveAt(idx);
            Object.Destroy(popup.gameObject);
            RefreshPanelState();
        }
    }

    public void CloseTopPopup(bool withAnimation = true)
    {
        CleanupNullPopups();
        if (_popups.Count == 0) return;

        var popup = _popups[^1].Popup;
        if (popup == null) return;

        if (withAnimation) popup.Close();
        else ClosePopup(popup);
    }

    public void ClearAllPopups(bool withAnimation = false)
    {
        CleanupNullPopups();

        if (withAnimation)
        {
            var tempPopups = new List<UI_Popup>();
            foreach (var data in _popups)
            {
                if (data.Popup != null) tempPopups.Add(data.Popup);
            }

            foreach (var popup in tempPopups)
            {
                popup.Close();
            }
        }
        else
        {
            foreach (var data in _popups)
            {
                if (data.Popup != null) Object.Destroy(data.Popup.gameObject);
            }
            _popups.Clear();
            RefreshPanelState();
        }
    }

    public void ClearAll(bool withAnimation = false)
    {
        ClearAllViews();
        ClearAllPopups(withAnimation);
    }

    public async UniTask SceneFadeAsync(float from, float to, CancellationToken token)
    {
        if (_sceneGroup == null) return;

        _sceneGroup.blocksRaycasts = true;
        _sceneGroup.alpha = from;

        await _sceneGroup.DOFade(to, FADE_DURATION)
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .WithCancellation(token);

        if (to <= 0.0001f)
            _sceneGroup.blocksRaycasts = false;
    }

    private void RefreshPanelState()
    {
        if (_popupPanel == null) return;
        CleanupNullPopups();

        int targetIndex = FindTopClickGuardPopupindex();

        if (targetIndex == -1)
        {
            _popupPanel.DOKill();
            if (_popupPanel.gameObject.activeSelf)
            {
                _popupPanel.DOFade(0f, GUARD_DURATION)
                    .SetUpdate(true)
                    .OnComplete(() => _popupPanel.gameObject.SetActive(false));
            }
            for (int i = 0; i < _popups.Count; i++)
            {
                if (_popups[i].Popup != null)
                    _popups[i].Popup.transform.SetSiblingIndex(i);
            }
            return;
        }

        _popupPanel.gameObject.SetActive(true);
        _popupPanel.DOKill();
        _popupPanel.DOFade(_popups[targetIndex].ClickGuardAlpha, GUARD_DURATION).SetUpdate(true);

        int currentSiblingIndex = 0;
        for (int i = 0; i < _popups.Count; i++)
        {
            if (i == targetIndex)
            {
                _popupPanel.transform.SetSiblingIndex(currentSiblingIndex++);
            }

            if (_popups[i].Popup != null)
            {
                _popups[i].Popup.transform.SetSiblingIndex(currentSiblingIndex++);
            }
        }
    }

    private void OnClickPanel()
    {
        int targetIndex = FindTopClickGuardPopupindex();
        if (targetIndex < 0) return;

        var data = _popups[targetIndex];
        if (data.ClickClose && data.Popup != null)
        {
            data.Popup.Close();
        }
    }

    private int FindTopClickGuardPopupindex()
    {
        for (int i = _popups.Count - 1; i >= 0; i--)
        {
            if (_popups[i].Popup != null && _popups[i].IsClickGuard) return i;
        }
        return -1;
    }

    private void CleanupNullPopups()
    {
        _popups.RemoveAll(p => p.Popup == null);
    }

    private void InitEventSystem(Transform ts = null)
    {
        _eventSystem = Object.FindAnyObjectByType<EventSystem>();

        if (_eventSystem == null)
        {
            var go = new GameObject("EventSystem");
            _eventSystem = go.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif

            if (ts != null) go.transform.SetParent(ts);
            else Object.DontDestroyOnLoad(go);
        }
    }
    private async UniTask<GameObject> CreateCanvas(string key, Transform ts = null)
    {
        var prefab = await Managers.Resource.LoadAssetAsync<GameObject>(key);
        var instance = Object.Instantiate(prefab);
        instance.name = key;
        if (ts != null) instance.transform.SetParent(ts, false);
        else Object.DontDestroyOnLoad(instance);
        return instance;
    }

    private Image CreatePanel()
    {
        var panelGo = new GameObject("[PopupPanel]");
        panelGo.transform.SetParent(_popupRoot.transform, false);
        var img = panelGo.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0);

        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        return img;
    }

    private CanvasGroup CreateSceneCanvas(string key = null, Transform ts = null)
    {
        var name = key ?? "Canvas_Scene";
        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        go.AddComponent<CanvasScaler>();

        var group = go.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        var imageGo = new GameObject("FadeImage");
        imageGo.transform.SetParent(go.transform, false);

        var image = imageGo.AddComponent<Image>();
        image.color = Color.black;
        var rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        if (ts != null) go.transform.SetParent(ts, false);
        else Object.DontDestroyOnLoad(go);

        return group;
    }
}