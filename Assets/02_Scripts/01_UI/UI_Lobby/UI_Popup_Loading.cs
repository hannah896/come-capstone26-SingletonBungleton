using UnityEngine;

public class UI_Popup_Loading : UI_Popup
{
    [SerializeField] private UI_Image ProgressFill;
    [SerializeField] private UI_Text ProgressText;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        SetProgress(0f, "로딩 중...");
        return true;
    }

    public void SetProgress(float value, string label)
    {
        float clamped = Mathf.Clamp01(value);

        if (ProgressFill != null)
        {
            ProgressFill.SetFill(clamped);
        }

        if (ProgressText != null)
        {
            int percent = Mathf.RoundToInt(clamped * 100f);
            ProgressText.Text = $"{label} {percent}%";
        }
    }
}