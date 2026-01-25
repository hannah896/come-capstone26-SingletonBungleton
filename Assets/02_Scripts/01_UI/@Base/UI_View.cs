
public class UI_View : UI_Base
{
    public override void Close()
    {
        base.Close();
        if (Main.Instance != null && Main.UI != null)
        {
            Main.UI.CloseView(this);
        }
    }
}