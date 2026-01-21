
public class UI_View : UI_Base
{
    public override void Close()
    {
        base.Close();
        if (Managers.Instance != null && Managers.UI != null)
        {
            Managers.UI.CloseView(this);
        }
    }
}