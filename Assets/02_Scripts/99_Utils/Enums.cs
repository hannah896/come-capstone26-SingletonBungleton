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

/// <summary>
/// 에셋 캐시 타입.
/// </summary>
public enum AssetCacheType
{
    Required, // 필수 에셋. 명시적으로 해제하기 전까지 유지됩니다.
    NonRequired, // 비필수 에셋. 씬 변경 시 해제될 수 있습니다.
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