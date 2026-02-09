
public class UI_Hud : UI_Panel
{
    public override void Close()
    {
        base.Close();
        if (Main.Instance != null && Main.UI != null)
        {
            Main.UI.CloseHud();
        }
    }
}