using TMPro;
using UnityEngine;

/// <summary>
/// 월문 안내 문구. 화면 위쪽 가운데에 작고 은은한 글자로 잠깐 떴다가 사라진다.
/// (공용 Toast는 박스가 커서 월문 입장/퇴장 안내에는 맞지 않아 따로 만들었다)
/// UI는 Resources/UI_GateMessage 프리팹에 있고, 없으면 Toast로 대신 보여준다.
/// </summary>
public class GateMessage : MonoBehaviour
{
    private const string ResourcePath = "UI_GateMessage";

    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text label;
    [SerializeField] private float fadeSeconds = 0.25f;

    private static GateMessage instance;
    private float hideAt;

    public static void Show(string text, float duration = 3f)
    {
        if (instance == null)
        {
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null)
            {
                Toast.Show(text, duration, ToastColor.Black, ToastPosition.TopCenter);
                return;
            }

            GameObject go = Instantiate(prefab);
            DontDestroyOnLoad(go);
            instance = go.GetComponent<GateMessage>();
            if (instance == null)
            {
                Destroy(go);
                Toast.Show(text, duration, ToastColor.Black, ToastPosition.TopCenter);
                return;
            }
        }

        instance.Display(text, duration);
    }

    private void Awake()
    {
        if (group != null) group.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Display(string text, float duration)
    {
        if (label != null) label.text = text;
        hideAt = Time.unscaledTime + Mathf.Max(0.5f, duration);
    }

    private void Update()
    {
        if (group == null) return;

        float target = Time.unscaledTime < hideAt ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / Mathf.Max(0.05f, fadeSeconds));
    }
}
