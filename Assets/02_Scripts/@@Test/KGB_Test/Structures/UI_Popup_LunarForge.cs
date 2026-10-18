/// <summary>
/// 문(달) 연성대를 E키로 열었을 때 뜨는 마력 무기 제작 팝업.
/// 작업대 팝업(UI_Popup_Workbench)과 같은 구조이고, 카테고리 탭만 "마력 무기" 하나다.
/// </summary>
public class UI_Popup_LunarForge : UI_Popup_Workbench
{
    private static readonly RecipeCategory[] LunarCategories = { RecipeCategory.Magic };

    protected override RecipeCategory[] Categories => LunarCategories;

    protected override string PopupTitle => "문(달) 연성대";
}
