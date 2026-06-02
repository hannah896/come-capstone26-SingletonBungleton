using Cysharp.Threading.Tasks;
using UnityEngine;

public class UI_HUD_LobbyScene : UI_Hud
{
    [SerializeField] private UI_Button WorldGenerateButton;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        if (WorldGenerateButton != null)
        {
            WorldGenerateButton.SetEvent(OnClickWorldGenerateButton);
        }

        return true;
    }

    private void OnDestroy()
    {
        if (WorldGenerateButton != null)
        {
            WorldGenerateButton.EventClear();
        }
    }

    private void OnClickWorldGenerateButton()
    {
        Debug.Log("World Generate Button Clicked");
        if (Main.UI == null) return;

        Main.UI.ShowPopup<UI_Popup_WorldGen>(clickGuard: true).Forget();
    }
}
