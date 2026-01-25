using System;

[Serializable]
public sealed class PageState
{
    public int Current { get; set; }
    public int Target { get; set; }
    public bool IsSnapping { get; set; }
    public bool IsManualNavigate { get; set; }
    
    public void Reset()
    {
        Current = 0;
        Target = 0;
        IsSnapping = false;
        IsManualNavigate = false;
    }
}