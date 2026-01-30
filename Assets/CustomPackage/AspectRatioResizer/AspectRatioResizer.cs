using UnityEngine;
using UnityEngine.UI;

// 에디터 모드에서도 스크립트가 실행되도록 설정 (Editor에서 바로 결과를 확인하기 위함)
[ExecuteInEditMode]
// Image와 RectTransform 컴포넌트가 필수로 필요함을 명시
[RequireComponent(typeof(RectTransform))]
public class AspectRatioResizer : MonoBehaviour
{
    // 리사이징 기준 축을 선택하는 Enum
    public enum BaseAxis
    {
        Horizontal,
        Vertical
    }

    // 인스펙터에서 설정할 변수
    public BaseAxis baseAxis = BaseAxis.Horizontal; // 기본 기준 축
    public float targetSize = 100f; // 목표 크기 (가로 또는 세로)
    public string targetImage;
    
    // UI Image 컴포넌트 참조 (자동으로 가져옵니다)
    private Image imageComponent;
    private RectTransform rectTransform;

    private void Awake()
    {
        imageComponent = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();
    }

    public void ApplyResize()
    {
        if (rectTransform == null)
        {
            Debug.LogWarning("not found RectTransform.");
            return;
        }

        if (!string.IsNullOrEmpty(targetImage))
        {
            imageComponent = gameObject.FindChild<Image>(targetImage);
        }
        
        if(imageComponent == null)
        {
            Debug.LogWarning("not found imageComponent");
            return;
        }

        Sprite sprite = imageComponent.sprite;
        float originalWidth = sprite.rect.width;
        float originalHeight = sprite.rect.height;

        float aspectRatio = originalWidth / originalHeight;

        Vector2 newSize = rectTransform.sizeDelta;

        if (baseAxis == BaseAxis.Horizontal)
        {
            newSize.x = targetSize;
            newSize.y = targetSize / aspectRatio;
        }
        else 
        {
            newSize.y = targetSize;
            newSize.x = targetSize * aspectRatio;
        }

        rectTransform.sizeDelta = newSize;
    }
}