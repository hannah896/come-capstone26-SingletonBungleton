using UnityEngine;

/// <summary>
/// Screen.safeArea 정보를 가져와 RectTransform의 크기와 위치를 조정하여 
/// 노치, 상태 표시줄, 홈 표시기 등의 방해 영역을 피하게 합니다.
/// 이 스크립트를 중요 UI 요소나 Safe Area를 준수해야 하는 컨테이너에 적용하세요.
/// </summary>
public class UI_Utility_SafeArea : MonoBehaviour
{
#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
    // 크기를 조정할 대상 RectTransform
    [Header("Safe Area Panel")]
    [SerializeField] private RectTransform _safeAreaPanel;

    // 노치 영역을 채울 필러들
    [Header("Filler RectTransforms")]
    [SerializeField] private RectTransform _leftFiller;
    [SerializeField] private RectTransform _rightFiller;
    [SerializeField] private RectTransform _topFiller;
    [SerializeField] private RectTransform _bottomFiller;

    // 현재 적용된 Safe Area (중복 적용 방지용)
    private Rect lastSafeArea = new Rect(0, 0, 0, 0);

    private void Awake()
    {
        if (_safeAreaPanel == null)
            _safeAreaPanel = GetComponent<RectTransform>();
        if (_safeAreaPanel == null)
        {
            enabled = false;
            return;
        }

        ApplySafeArea();
    }

    /// <summary>
    /// 화면의 해상도나 크기, 앵커가 변경될 때 호출되어 Safe Area를 다시 적용합니다.
    /// </summary>
    private void OnRectTransformDimensionsChange()
    {
        // 해상도나 safeArea가 변경되었을 때만 로직 실행
        if (Screen.safeArea != lastSafeArea)
        {
            ApplySafeArea();
        }
    }

    private void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;

        if (_safeAreaPanel == null) return;
        if (safeArea == lastSafeArea) return;

        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        bool isLandscape = Screen.width > Screen.height;

        if (isLandscape)
        {
            anchorMin.y = 0f;
            anchorMax.y = 1f;
        }
        else
        {
            anchorMin.x = 0f;
            anchorMax.x = 1f;
        }

        _safeAreaPanel.anchorMin = anchorMin;
        _safeAreaPanel.anchorMax = anchorMax;

        _safeAreaPanel.offsetMin = Vector2.zero;
        _safeAreaPanel.offsetMax = Vector2.zero;

        UpdateFiller(_leftFiller, 0, 0, anchorMin.x, 1);     // 왼쪽: 0부터 Safe 시작점까지
        UpdateFiller(_rightFiller, anchorMax.x, 0, 1, 1);   // 오른쪽: Safe 끝점부터 1까지
        UpdateFiller(_topFiller, 0, anchorMax.y, 1, 1);     // 상단
        UpdateFiller(_bottomFiller, 0, 0, 1, anchorMin.y);  // 하단

        lastSafeArea = safeArea;
    }

    private void UpdateFiller(RectTransform filler, Vector2 min, Vector2 max)
    {
        if (filler == null) return;
        filler.anchorMin = min;
        filler.anchorMax = max;
        filler.offsetMin = Vector2.zero;
        filler.offsetMax = Vector2.zero;
    }

    // 오버로드 (float 버전)
    private void UpdateFiller(RectTransform filler, float minX, float minY, float maxX, float maxY)
    {
        UpdateFiller(filler, new Vector2(minX, minY), new Vector2(maxX, maxY));
    }
#endif
}