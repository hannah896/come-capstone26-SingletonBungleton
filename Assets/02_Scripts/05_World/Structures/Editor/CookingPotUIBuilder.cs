#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 화덕(CookingPot) 전용 요리 팝업 UI를 자동 생성하는 에디터 도구.
/// 크래프팅 UI와 로직(CraftingRecipeSlotUI/CraftingIngredientSlotUI)은 그대로 재사용하지만,
/// 좁은 사이드바용인 크래프팅 UI 슬롯보다 훨씬 크게 별도 슬롯 프리팹을 새로 만든다
/// (크래프팅 UI 쪽 슬롯 크기에는 영향 없음).
/// 메뉴: Tools → Cooking Pot UI Builder
/// 생성 후 Addressables에 "UI_Popup_CookingPot" 키로 등록해야
/// Extensions.ShowPopup&lt;UI_Popup_CookingPot&gt;()가 찾는다.
/// </summary>
public class CookingPotUIBuilder : EditorWindow
{
    const int   Columns    = 3;     // 한 줄에 3개 고정
    const float SlotGap    = 6f;
    const float Pad        = 14f;
    const float SlotSize   = 140f;  // 레시피 그리드 슬롯 (크래프팅 UI는 70) — PanelW과 맞물려서 3열 정확히 채움
    const float PanelW     = Pad * 2 + Columns * SlotSize + (Columns - 1) * SlotGap;

    const float TitleBarH  = 52f;
    const float FilterBarH = 32f;
    const float ScrollH    = 480f;
    const float DetailH    = 256f;  // 결과 아이콘 블록 제거하고 재료만 남겨서 예전보다 작음
    const float PanelH     = TitleBarH + FilterBarH + ScrollH + DetailH;

    const float IngredW    = 124f;  // 재료 슬롯 (크래프팅 UI는 62x80, 그대로 2배)
    const float IngredH    = 160f;

    static readonly Color32 cBg        = new(26, 17, 12, 242);
    static readonly Color32 cTitleBar  = new(18, 12, 7, 255);
    static readonly Color32 cBgScroll  = new(14, 9, 5, 255);
    static readonly Color32 cBgDetail  = new(20, 13, 8, 255);
    static readonly Color32 cBorder    = new(72, 52, 24, 255);
    static readonly Color32 cCream     = new(229, 212, 168, 255);
    static readonly Color32 cAmber     = new(202, 136, 18, 255);
    static readonly Color32 cAmberDark = new(120, 78, 8, 255);
    static readonly Color32 cDim       = new(140, 124, 90, 255);
    static readonly Color32 cSlotNorm  = new(38, 27, 15, 255);

    string pathPopup      = "Assets/03_Prefabs/UI/Structures/UI_Popup_CookingPot.prefab";
    string pathRecipeSlot = "Assets/03_Prefabs/UI/Structures/CookingRecipeSlot.prefab";
    string pathIngredSlot = "Assets/03_Prefabs/UI/Structures/CookingIngredientSlot.prefab";

    [MenuItem("Tools/Cooking Pot UI Builder")]
    static void Open() => GetWindow<CookingPotUIBuilder>("Cooking Pot UI Builder");

    void OnGUI()
    {
        GUILayout.Label("화덕 요리 팝업 UI 빌더 (크래프팅 UI보다 큰 전용 슬롯)", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);
        pathPopup      = EditorGUILayout.TextField("Popup 저장 경로", pathPopup);
        pathRecipeSlot = EditorGUILayout.TextField("레시피 슬롯 저장 경로", pathRecipeSlot);
        pathIngredSlot = EditorGUILayout.TextField("재료 슬롯 저장 경로", pathIngredSlot);
        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "레시피/재료 슬롯을 화덕 전용으로 새로 만듭니다 (크래프팅 UI 쪽 슬롯은 그대로 둠).\n" +
            "생성 후 Addressables에 UI_Popup_CookingPot.prefab을 \"UI_Popup_CookingPot\" 키로 등록하세요.",
            MessageType.Info);
        EditorGUILayout.Space(6);
        GUI.backgroundColor = new Color(0.8f, 0.6f, 0.1f);
        if (GUILayout.Button("UI 생성 + 프리팹 저장", GUILayout.Height(36)))
            Build();
        GUI.backgroundColor = Color.white;
    }

    void Build()
    {
        EnsureDir(pathRecipeSlot);
        EnsureDir(pathIngredSlot);
        EnsureDir(pathPopup);

        // 슬롯 프리팹은 크래프팅 UI의 실제 슬롯(이름표 포함)을 손으로 그대로 옮겨 만든 것이라
        // 이미 있으면 절대 덮어쓰지 않는다 — 새로 만들고 싶으면 파일을 지우고 다시 실행.
        var recipePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(pathRecipeSlot);
        if (recipePrefab == null)
        {
            var recipeGO = MakeRecipeSlot();
            PrefabUtility.SaveAsPrefabAsset(recipeGO, pathRecipeSlot);
            DestroyImmediate(recipeGO);
            AssetDatabase.Refresh();
            recipePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(pathRecipeSlot);
        }

        var ingredPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(pathIngredSlot);
        if (ingredPrefab == null)
        {
            var ingredGO = MakeIngredientSlot();
            PrefabUtility.SaveAsPrefabAsset(ingredGO, pathIngredSlot);
            DestroyImmediate(ingredGO);
            AssetDatabase.Refresh();
            ingredPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(pathIngredSlot);
        }

        if (recipePrefab == null || ingredPrefab == null)
        {
            EditorUtility.DisplayDialog("실패", "슬롯 프리팹 저장/로드에 실패했습니다.", "확인");
            return;
        }

        var panelGO = MakePanel(recipePrefab, ingredPrefab);
        PrefabUtility.SaveAsPrefabAssetAndConnect(panelGO, pathPopup, InteractionMode.AutomatedAction);

        AssetDatabase.Refresh();
        Selection.activeGameObject = panelGO;
        EditorUtility.DisplayDialog("완료",
            "UI_Popup_CookingPot 생성 완료!\nAddressables에 \"UI_Popup_CookingPot\" 키로 등록하세요.", "확인");
    }

    // ═══════════════════════════════════════════════════════════
    //  레시피 슬롯 (그리드에 보이는 아이콘 칸)
    // ═══════════════════════════════════════════════════════════
    GameObject MakeRecipeSlot()
    {
        var root = new GameObject("CookingRecipeSlot");
        var rootRT = root.AddComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(SlotSize, SlotSize);

        var border = MakeImg(root, "Border", cBorder);
        SetRect(RT(border), V(0, 0), V(1, 1), V(.5f, .5f), V(0, 0), V(0, 0));

        var bg = MakeImg(root, "Background", cSlotNorm);
        SetRect(RT(bg), V(0, 0), V(1, 1), V(.5f, .5f), V(2, 2), V(-2, -2));

        var btn = root.AddComponent<Button>();
        btn.targetGraphic = bg;
        var bc = btn.colors;
        bc.normalColor      = Color.white;
        bc.highlightedColor = new Color32(255, 220, 140, 255);
        bc.pressedColor     = cAmber;
        bc.disabledColor    = new Color32(80, 60, 30, 200);
        btn.colors = bc;

        var icon = MakeImg(root, "Icon", Color.white);
        icon.preserveAspect = true;
        SetRect(RT(icon), V(0, 0), V(1, 1), V(.5f, .5f), V(16, 16), V(-16, -16));

        var lockGO = new GameObject("LockOverlay");
        lockGO.transform.SetParent(root.transform, false);
        var lockRT = lockGO.AddComponent<RectTransform>();
        lockGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);
        SetRect(lockRT, V(0, 0), V(1, 1), V(.5f, .5f), V(2, 2), V(-2, -2));
        lockGO.SetActive(false);

        var badgeGO = new GameObject("LearnedBadge");
        badgeGO.transform.SetParent(root.transform, false);
        var badgeRT = badgeGO.AddComponent<RectTransform>();
        badgeGO.AddComponent<Image>().color = cAmber;
        badgeRT.anchorMin = badgeRT.anchorMax = V(0, 1);
        badgeRT.pivot = V(0, 1);
        badgeRT.sizeDelta = V(14, 14);
        badgeRT.anchoredPosition = V(3, -3);
        badgeGO.SetActive(false);

        var comp = root.AddComponent<CraftingRecipeSlotUI>();
        WireField(comp, "iconImage", icon);
        WireField(comp, "background", bg);
        WireField(comp, "lockOverlay", lockGO);
        WireField(comp, "learnedBadge", badgeGO);
        return root;
    }

    // ═══════════════════════════════════════════════════════════
    //  재료 슬롯 (상세 패널 하단, 재료 아이콘+이름+보유수)
    // ═══════════════════════════════════════════════════════════
    GameObject MakeIngredientSlot()
    {
        var root = new GameObject("CookingIngredientSlot");
        var rootRT = root.AddComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(IngredW, IngredH);

        var icon = MakeImg(root, "Icon", Color.white);
        icon.preserveAspect = true;
        SetRect(RT(icon), V(0, 0), V(1, 1), V(.5f, .5f), V(8, 38), V(-8, -6));

        var nameTxt = MakeTMP(root, "NameText", 14f, cDim);
        nameTxt.alignment = TextAlignmentOptions.Bottom;
        nameTxt.overflowMode = TextOverflowModes.Ellipsis;
        SetRect(RT(nameTxt), V(0, 0), V(1, 0), V(.5f, 0), V(4, 0), V(-4, 34));

        var cntTxt = MakeTMP(root, "CountText", 18f, cCream);
        cntTxt.fontStyle = FontStyles.Bold;
        cntTxt.alignment = TextAlignmentOptions.BottomRight;
        SetRect(RT(cntTxt), V(0, 0), V(1, 1), V(1, 0), V(0, 34), V(-4, 4));

        var comp = root.AddComponent<CraftingIngredientSlotUI>();
        WireField(comp, "iconImage", icon);
        WireField(comp, "nameText", nameTxt);
        WireField(comp, "countText", cntTxt);
        return root;
    }

    // ═══════════════════════════════════════════════════════════
    //  팝업 패널
    // ═══════════════════════════════════════════════════════════
    GameObject MakePanel(GameObject recipePrefab, GameObject ingredPrefab)
    {
        var root = new GameObject("UI_Popup_CookingPot");
        var rootRT = root.AddComponent<RectTransform>();
        rootRT.anchorMin = rootRT.anchorMax = V(.5f, .5f);
        rootRT.pivot = V(.5f, .5f);
        rootRT.sizeDelta = V(PanelW, PanelH);
        rootRT.anchoredPosition = V(0, 0);

        root.AddComponent<Image>().color = cBg;
        root.AddComponent<CanvasGroup>();

        // ── 타이틀 바 ────────────────────────────────────────────
        var titleBar = Child(root, "TitleBar");
        titleBar.AddComponent<Image>().color = cTitleBar;
        SetRect(RT(titleBar), V(0, 1), V(1, 1), V(.5f, 1), V(0, -TitleBarH), V(0, 0));

        var titleTxt = MakeTMP(titleBar, "titleText", 24f, cCream);
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.alignment = TextAlignmentOptions.MidlineLeft;
        titleTxt.text = "화덕";
        SetRect(RT(titleTxt), V(0, 0), V(1, 1), V(0, .5f), V(Pad, 0), V(-44, 0));

        var closeGO = Child(titleBar, "CloseButton");
        var closeImg = closeGO.AddComponent<Image>();
        closeImg.color = cAmber;
        var closeBtn = closeGO.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        SetRect(RT(closeGO), V(1, .5f), V(1, .5f), V(1, .5f), V(-32, -14), V(-6, 14));

        var closeTxt = MakeTMP(closeGO, "X", 16f, Color.black);
        closeTxt.text = "X";
        closeTxt.fontStyle = FontStyles.Bold;
        closeTxt.alignment = TextAlignmentOptions.Center;
        SetRect(RT(closeTxt), V(0, 0), V(1, 1), V(.5f, .5f), V(0, 0), V(0, 0));

        // ── 필터 버튼 ────────────────────────────────────────────
        var filterGO = Child(root, "FilterToggleButton");
        var filterImg = filterGO.AddComponent<Image>();
        filterImg.color = cTitleBar;
        var filterBtn = filterGO.AddComponent<Button>();
        filterBtn.targetGraphic = filterImg;
        {
            var rt = RT(filterGO);
            rt.anchorMin = rt.anchorMax = V(1, 1);
            rt.pivot = V(1, 1);
            rt.sizeDelta = V(140, FilterBarH - 4);
            rt.anchoredPosition = V(-Pad, -(TitleBarH + 3f));
        }
        var filterTxt = MakeTMP(filterGO, "FilterText", 13f, cDim);
        filterTxt.text = "전체 보기 [F]";
        filterTxt.alignment = TextAlignmentOptions.Center;
        SetRect(RT(filterTxt), V(0, 0), V(1, 1), V(.5f, .5f), V(0, 0), V(0, 0));

        // ── 레시피 스크롤뷰 ──────────────────────────────────────
        var scrollGO = Child(root, "RecipeScrollView");
        scrollGO.AddComponent<Image>().color = cBgScroll;
        SetRect(RT(scrollGO), V(0, 0), V(1, 1), V(.5f, .5f), V(Pad, DetailH), V(-Pad, -(TitleBarH + FilterBarH)));

        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        var viewport = Child(scrollGO, "Viewport");
        viewport.AddComponent<RectMask2D>();
        SetRect(RT(viewport), V(0, 0), V(1, 1), V(0, 1), V(0, 0), V(0, 0));

        var grid = Child(viewport, "RecipeListContainer");
        var gridRT = RT(grid);
        gridRT.anchorMin = V(0, 1); gridRT.anchorMax = V(1, 1);
        gridRT.pivot = V(.5f, 1); gridRT.sizeDelta = V(0, 0);

        var glg = grid.AddComponent<GridLayoutGroup>();
        glg.cellSize = V(SlotSize, SlotSize);
        glg.spacing = V(SlotGap, SlotGap);
        glg.padding = new RectOffset((int)Pad, (int)Pad, (int)Pad, (int)Pad);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = Columns;
        glg.childAlignment = TextAnchor.UpperLeft;

        var csf = grid.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = gridRT;
        scrollRect.viewport = RT(viewport);

        // ── 상세 패널 ────────────────────────────────────────────
        var detail = Child(root, "DetailPanel");
        detail.AddComponent<Image>().color = cBgDetail;
        SetRect(RT(detail), V(0, 0), V(1, 0), V(.5f, 0), V(0, 0), V(0, DetailH));

        {
            var go = Child(detail, "TopBorder");
            go.AddComponent<Image>().color = cBorder;
            SetRect(RT(go), V(0, 1), V(1, 1), V(.5f, 1), V(0, -2), V(0, 0));
        }

        // 결과 아이콘은 그리드 슬롯에 이미 보이므로 상세 패널에서는 재료만 보여준다
        var ingCont = Child(detail, "IngredientContainer");
        var ingContRT = RT(ingCont);
        ingContRT.anchorMin = V(0, 0); ingContRT.anchorMax = V(1, 0);
        ingContRT.pivot = V(.5f, 0);
        // RectTransform pivot이 (.5,0)이라 anchoredPosition.y는 "바닥 기준 오프셋" 그 자체다
        // (센터 기준으로 착각해서 +height/2를 더했다가 재료 줄이 위로 붕 뜨는 버그가 났었음 — 그대로 쓰면 됨)
        const float craftButtonTop = 8f + 44f; // CraftButton bottom(8) + height(44)
        const float gap = 10f;
        ingContRT.sizeDelta = V(0, IngredH + 16);
        ingContRT.anchoredPosition = V(0, craftButtonTop + gap);

        var ingHLG = ingCont.AddComponent<HorizontalLayoutGroup>();
        ingHLG.padding = new RectOffset((int)Pad, (int)Pad, 8, 8);
        ingHLG.spacing = 10f;
        ingHLG.childAlignment = TextAnchor.MiddleLeft;
        ingHLG.childControlWidth = false;
        ingHLG.childControlHeight = true;
        ingHLG.childForceExpandHeight = true;

        var craftBtnGO = Child(detail, "CraftButton");
        var craftBtnImg = craftBtnGO.AddComponent<Image>();
        craftBtnImg.color = cAmberDark;
        var craftBtnRT = RT(craftBtnGO);
        craftBtnRT.anchorMin = V(0, 0); craftBtnRT.anchorMax = V(1, 0);
        craftBtnRT.pivot = V(.5f, 0);
        craftBtnRT.sizeDelta = V(-Pad * 2, 44);
        craftBtnRT.anchoredPosition = V(0, 8);

        var craftBtn = craftBtnGO.AddComponent<Button>();
        craftBtn.targetGraphic = craftBtnImg;
        var cc = craftBtn.colors;
        cc.normalColor      = Color.white;
        cc.highlightedColor = new Color32(255, 210, 70, 255);
        cc.pressedColor     = new Color32(160, 100, 10, 255);
        cc.disabledColor    = new Color32(80, 60, 30, 180);
        craftBtn.colors = cc;

        var craftTxt = MakeTMP(craftBtnGO, "CraftButtonText", 17f, cCream);
        craftTxt.text = "요리 [Space]";
        craftTxt.fontStyle = FontStyles.Bold;
        craftTxt.alignment = TextAlignmentOptions.Center;
        SetRect(RT(craftTxt), V(0, 0), V(1, 1), V(.5f, .5f), V(0, 0), V(0, 0));

        // ── 컴포넌트 연결 ────────────────────────────────────────
        var popup = root.AddComponent<UI_Popup_CookingPot>();
        WireField(popup, "titleText", titleTxt);
        WireField(popup, "closeButton", closeBtn);
        WireField(popup, "recipeListContainer", gridRT);
        WireField(popup, "recipeSlotPrefab", recipePrefab);
        WireField(popup, "detailPanel", detail);
        WireField(popup, "ingredientContainer", ingContRT);
        WireField(popup, "ingredientSlotPrefab", ingredPrefab);
        WireField(popup, "craftButton", craftBtn);
        WireField(popup, "craftButtonText", craftTxt);
        WireField(popup, "filterToggleButton", filterBtn);
        WireField(popup, "filterButtonText", filterTxt);

        return root;
    }

    // ── 헬퍼 (ChestUIBuilder/CraftingUIBuilder와 동일 패턴) ─────
    static GameObject Child(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static Image MakeImg(GameObject parent, string name, Color32 color)
    {
        var go = Child(parent, name);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    const string KoreanFontPath = "Assets/09_Font/NEXON Football Gothic B SDF.asset";
    static TMP_FontAsset koreanFont;

    static TextMeshProUGUI MakeTMP(GameObject parent, string name, float size, Color32 color)
    {
        var go = Child(parent, name);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (koreanFont == null)
            koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
        if (koreanFont != null)
            tmp.font = koreanFont;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 offMin, Vector2 offMax)
    {
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.pivot = pivot;
        rt.offsetMin = offMin; rt.offsetMax = offMax;
    }

    static RectTransform RT(Component c) => c.GetComponent<RectTransform>();
    static RectTransform RT(GameObject go) => go.GetComponent<RectTransform>();
    static Vector2 V(float x, float y) => new(x, y);

    static void WireField(Object comp, string field, Object value)
    {
        var so = new SerializedObject(comp);
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogWarning($"[CookingPotUIBuilder] 필드 없음: {field}"); return; }
        prop.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    static void EnsureDir(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }
}
#endif
