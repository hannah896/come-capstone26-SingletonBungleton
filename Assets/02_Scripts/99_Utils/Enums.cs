#region UI
public enum UIType
{
    None,
    View,
    Popup,
}

public enum UIEvent
{
    Click,
    Drag,
    Count,
}

public enum CanvasType
{
    Static,
    Dynamic,
    Count,
}
#endregion

#region Resource
public enum AssetCacheType
{
    Required,
    NonRequired
}
#endregion

#region Item
public enum ItemType
{
    Gatherables,   // 자원
    Food,       // 음식
    Tool,       // 도구
    Weapon,     // 무기
    Armor      // 방어구}
}
#endregion