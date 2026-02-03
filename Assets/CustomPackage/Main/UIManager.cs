using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

#region Enums

/// <summary>
/// 화면 효과 타입.
/// </summary>
public enum ScreenEffectType
{
    None,
    Fade,
    Transition,
    Ads,
    Iap
}

#endregion

/// <summary>
/// UI 시스템을 관리하는 매니저.
/// Hud, Popup, Screen 세 가지 레이어를 통해 UI를 표시합니다.
/// </summary>
public class UIManager : PrimaryManager
{
    #region Fields

    // 이벤트 시스템
    private EventSystem _eventSystem;

    // Hud 레이어 (전체 화면 UI)
    private HudLayer _huds;

    // Popup 레이어 (모달 팝업)
    private PopupLayer _popups;

    // Screen 레이어 (로딩/전환 화면)
    private ScreenLayer _screen;

    #endregion

    #region Initialization

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();
        await Init();
    }

    // UI 시스템 초기화
    private async UniTask Init()
    {
        var go = new GameObject("@UI");
        var tr = go.transform;
        GameObject.DontDestroyOnLoad(tr);

        EnsureEventSystem(tr);

        var hudRoot = await CreateCanvas("Canvas_Hud", sortingOrder: 10, tr);
        var popupRoot = await CreateCanvas("Canvas_Popup", sortingOrder: 20, tr);
        var sceneRoot = await CreateCanvas("Canvas_Scene", sortingOrder: 1000, tr);

        _huds = new HudLayer(hudRoot);
        _popups = new PopupLayer(popupRoot);
        _screen = new ScreenLayer(sceneRoot);

        tr.SetSiblingIndex(1);
    }

    // 이벤트 시스템 존재 보장
    private void EnsureEventSystem(Transform parent)
    {
        _eventSystem = Object.FindAnyObjectByType<EventSystem>();
        if (_eventSystem != null) return;

        var go = new GameObject("EventSystem");
        _eventSystem = go.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
        go.transform.SetParent(parent, false);
    }

    // 캔버스 프리팹 로드 및 생성
    private async UniTask<Canvas> CreateCanvas(string key, int sortingOrder, Transform parent)
    {
        var prefabGo = await Main.Resource.LoadAssetAsync<GameObject>(key);
        if (prefabGo == null) return null;

        var instance = Object.Instantiate(prefabGo, parent, false);
        instance.name = key;

        if (!instance.TryGetComponent<Canvas>(out var canvas))
        {
            Object.Destroy(instance.gameObject);
            return null;
        }

        canvas.sortingOrder = sortingOrder;
        return canvas;
    }

    #endregion

    #region Hud

    /// <summary>
    /// Hud UI를 표시합니다.
    /// </summary>
    public async UniTask<T> ShowHud<T>(string key = null, CancellationToken ct = default) where T : UI_Hud
    {
        await UniTask.WaitUntil(() => IsInitialized, cancellationToken: ct);
        return await _huds.Show<T>(key, ct);
    }

    /// <summary>
    /// 현재 Hud를 닫습니다.
    /// </summary>
    public void CloseHud() => _huds.Close();

    #endregion

    #region Popup

    /// <summary>
    /// 팝업 UI를 표시합니다.
    /// </summary>
    public async UniTask<T> ShowPopup<T>(
        string key = null,
        bool clickGuard = false,
        float clickGuardAlpha = -1,
        bool clickToClose = false,
        CancellationToken ct = default) where T : UI_Popup
    {
        await UniTask.WaitUntil(() => IsInitialized, cancellationToken: ct);
        return await _popups.Show<T>(key, clickGuard, clickGuardAlpha, clickToClose, ct);
    }

    /// <summary>
    /// 특정 팝업을 닫습니다.
    /// </summary>
    public void ClosePopup(UI_Popup popup) => _popups.Close(popup);

    /// <summary>
    /// 가장 위의 팝업을 닫습니다.
    /// </summary>
    public void CloseTopPopup(bool withAnimation = true) => _popups.CloseTop(withAnimation);

    /// <summary>
    /// 모든 팝업을 닫습니다.
    /// </summary>
    public void CloseAllPopups(bool withAnimation = false) => _popups.ClearAll(withAnimation);

    #endregion

    #region Screen

    /// <summary>
    /// 화면 효과를 표시합니다.
    /// </summary>
    public void ShowScreen(ScreenEffectType type, Action act = null, CancellationToken tk = default) => ShowScreenAsync(type, act, tk).Forget();

    /// <summary>
    /// 화면 효과를 숨깁니다.
    /// </summary>
    public void HideScreen(float time = 3f) => HideScreenAsync(time).Forget();

    /// <summary>
    /// 화면 효과를 비동기로 표시합니다.
    /// </summary>
    public async UniTask ShowScreenAsync(ScreenEffectType type, Action act = null, CancellationToken tk = default) => await _screen.Show(type, act, tk);

    /// <summary>
    /// 화면 효과를 비동기로 숨깁니다.
    /// </summary>
    public async UniTask HideScreenAsync(float time = 3f) => await _screen.Hide(time);

    #endregion

    #region Cleanup

    public override void Clear()
    {
        CloseHud();
        CloseAllPopups(false);
    }

    #endregion

    #region Nested Classes

    // Hud 레이어 관리
    private sealed class HudLayer
    {
        #region Fields

        // 루트 캔버스
        private readonly Canvas _root;

        // 현재 활성화된 Hud
        private UI_Hud _current;

        #endregion

        #region Constructor

        public HudLayer(Canvas root)
        {
            if (root == null) return;
            _root = root;
        }

        #endregion

        #region Public Methods

        // Hud 표시
        public async UniTask<T> Show<T>(string key, CancellationToken ct) where T : UI_Hud
        {
            if (_root == null)
            {
                try
                {
                    await UniTask.WaitUntil(() => _root != null, cancellationToken: ct)
                                 .Timeout(TimeSpan.FromSeconds(1));
                }
                catch (TimeoutException)
                {
                    return null;
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }

            var prefab = await Main.Resource.LoadAssetAsync<T>(key, AssetCacheType.NonRequired, ct);
            if (prefab == null) return null;

            var instance = Object.Instantiate(prefab, _root.transform);
            if (!instance.TryGetComponent<T>(out var comp))
            {
                Object.Destroy(instance.gameObject);
                return null;
            }

            if (_current != null)
                _current.Close();

            _current = comp;
            return comp;
        }

        // 현재 Hud 닫기
        public void Close()
        {
            if (_current == null) return;

            Object.Destroy(_current.gameObject);
            _current = null;
        }

        #endregion
    }

    // Popup 레이어 관리
    private sealed class PopupLayer
    {
        #region Nested Types

        // 팝업 데이터
        private struct PopupData
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

        #endregion

        #region Constants

        // 가드 애니메이션 지속 시간
        private const float GUARD_DURATION = 0.2f;

        // 기본 패널 알파값
        private const float BASE_PANEL_ALPHA = 0.6f;

        #endregion

        #region Fields

        // 루트 캔버스
        private readonly Canvas _root;

        // 팝업 스택
        private readonly List<PopupData> _popups = new();

        // 클릭 가드 패널
        private Image _panel;

        // 클릭 가드 버튼
        private Button _panelButton;

        #endregion

        #region Constructor

        public PopupLayer(Canvas root)
        {
            if (root == null) return;
            _root = root;

            _panel = CreatePanel(_root.transform);
            _panel.gameObject.SetActive(false);

            _panelButton = _panel.gameObject.GetOrAddComponent<Button>();
            _panelButton.transition = Selectable.Transition.None;
            _panelButton.onClick.AddListener(OnClickPanel);
        }

        #endregion

        #region Public Methods

        // 팝업 표시
        public async UniTask<T> Show<T>(
            string key,
            bool clickGuard,
            float clickGuardAlpha,
            bool clickToClose,
            CancellationToken ct) where T : UI_Popup
        {
            if (_root == null)
            {
                try
                {
                    await UniTask.WaitUntil(() => _root != null, cancellationToken: ct)
                                 .Timeout(TimeSpan.FromSeconds(1));
                }
                catch (TimeoutException)
                {
                    return null;
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }

            var prefab = await Main.Resource.LoadAssetAsync<T>(key, AssetCacheType.NonRequired, ct);
            if (prefab == null) return null;

            var instance = Object.Instantiate(prefab, _root.transform);
            if (!instance.TryGetComponent<T>(out var comp))
            {
                Object.Destroy(instance.gameObject);
                return null;
            }

            float alpha = (clickGuardAlpha < 0) ? BASE_PANEL_ALPHA : clickGuardAlpha;
            _popups.Add(new PopupData(comp, clickGuard, alpha, clickToClose));

            RefreshPanelState();
            return comp;
        }

        // 특정 팝업 닫기
        public void Close(UI_Popup popup)
        {
            if (popup == null) return;

            int idx = _popups.FindIndex(p => p.Popup == popup);
            if (idx < 0) return;

            _popups.RemoveAt(idx);
            Object.Destroy(popup.gameObject);
            RefreshPanelState();
        }

        // 최상위 팝업 닫기
        public void CloseTop(bool withAnimation = true)
        {
            CleanupNullPopups();
            if (_popups.Count == 0) return;

            var popup = _popups[^1].Popup;
            if (popup == null) return;

            if (withAnimation) popup.Close();
            else Close(popup);
        }

        // 모든 팝업 닫기
        public void ClearAll(bool withAnimation = false)
        {
            CleanupNullPopups();

            if (withAnimation)
            {
                var temp = new List<UI_Popup>();
                foreach (var data in _popups)
                    if (data.Popup != null) temp.Add(data.Popup);

                foreach (var p in temp) p.Close();
            }
            else
            {
                foreach (var data in _popups)
                    if (data.Popup != null) Object.Destroy(data.Popup.gameObject);

                _popups.Clear();
                RefreshPanelState();
            }
        }

        #endregion

        #region Internal Methods

        // 패널 클릭 처리
        private void OnClickPanel()
        {
            int targetIndex = FindTopClickGuardPopupIndex();
            if (targetIndex < 0) return;

            var data = _popups[targetIndex];
            if (data.ClickClose && data.Popup != null)
                data.Popup.Close();
        }

        // 패널 상태 갱신
        private void RefreshPanelState()
        {
            if (_panel == null) return;

            CleanupNullPopups();
            int guardIndex = FindTopClickGuardPopupIndex();

            if (guardIndex == -1)
            {
                _panel.DOKill();
                if (_panel.gameObject.activeSelf)
                {
                    _panel.DOFade(0f, GUARD_DURATION)
                        .SetUpdate(true)
                        .OnComplete(() => _panel.gameObject.SetActive(false));
                }

                for (int i = 0; i < _popups.Count; i++)
                    if (_popups[i].Popup != null)
                        _popups[i].Popup.transform.SetSiblingIndex(i);

                return;
            }

            _panel.gameObject.SetActive(true);
            _panel.DOKill();
            _panel.DOFade(_popups[guardIndex].ClickGuardAlpha, GUARD_DURATION).SetUpdate(true);

            int sibling = 0;
            for (int i = 0; i < _popups.Count; i++)
            {
                if (i == guardIndex)
                    _panel.transform.SetSiblingIndex(sibling++);

                if (_popups[i].Popup != null)
                    _popups[i].Popup.transform.SetSiblingIndex(sibling++);
            }
        }

        // 클릭 가드가 있는 최상위 팝업 인덱스 찾기
        private int FindTopClickGuardPopupIndex()
        {
            for (int i = _popups.Count - 1; i >= 0; i--)
                if (_popups[i].Popup != null && _popups[i].IsClickGuard) return i;
            return -1;
        }

        // null 팝업 정리
        private void CleanupNullPopups() => _popups.RemoveAll(p => p.Popup == null);

        // 클릭 가드 패널 생성
        private static Image CreatePanel(Transform parent)
        {
            var panelGo = new GameObject("[PopupPanel]");
            panelGo.transform.SetParent(parent, false);

            var img = panelGo.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0);

            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            return img;
        }

        #endregion
    }

    // Screen 레이어 관리
    private sealed class ScreenLayer
    {
        #region Fields

        // 루트 캔버스
        private readonly Canvas _root;

        // 타입별 스크린 캐시
        private readonly Dictionary<ScreenEffectType, UI_Loading> _screens = new();

        // 현재 활성화된 스크린
        private UI_Loading _currentActive;

        // 표시 시작 시간
        private float _showStartTime;

        #endregion

        #region Constructor

        public ScreenLayer(Canvas root)
        {
            if (root == null) return;
            _root = root;
        }

        #endregion

        #region Public Methods

        // 스크린 표시
        public async UniTask Show(ScreenEffectType type, Action onComplete = null, CancellationToken ct = default)
        {
            if (type == ScreenEffectType.None) return;
            if (_root == null) return;

            if (!_screens.TryGetValue(type, out var loading))
            {
                string key = $"UI_Screen_{type}";
                var prefab = await Main.Resource.LoadAssetAsync<GameObject>(key, AssetCacheType.NonRequired, ct);
                if (prefab == null) return;

                var instance = Object.Instantiate(prefab, _root.transform);
                instance.name = key;

                if (instance.TryGetComponent<UI_Loading>(out loading))
                {
                    _screens.Add(type, loading);
                }
            }

            if (_currentActive == loading) return;
            loading.Set(onComplete);

            _currentActive = loading;
            _showStartTime = Time.time;
            _currentActive.FadeInLoading();
        }

        // 스크린 숨기기
        public async UniTask Hide(float minWaitSec = 0f)
        {
            if (_currentActive == null) return;

            float elapsed = Time.time - _showStartTime;
            if (elapsed < minWaitSec)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(minWaitSec - elapsed));
            }
            _currentActive.FadeOutLoading();
        }

        #endregion
    }

    #endregion
}
