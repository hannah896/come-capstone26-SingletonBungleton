#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 돈스타브 스타일 크래프팅 UI를 씬에 자동 생성하는 에디터 도구.
/// 메뉴: Tools → Crafting UI Builder
/// </summary>
public class CraftingUIBuilder : EditorWindow
{
    // ── 치수 ────────────────────────────────────────────────────
    const float PanelW     = 292f;
    const float TabRowH    = 46f;
    const float FilterBarH = 24f;
    const float DetailH    = 204f;
    const float SlotSize   = 70f;
    const float SlotGap    = 3f;
    const float Pad        = 8f;
    const float IconSize   = 72f;

    // ── Don't Starve 컬러 팔레트 ────────────────────────────────
    static readonly Color32 cBgPanel   = new(26,  17,  12,  242);
    static readonly Color32 cBgTabRow  = new(18,  12,   7,  255);
    static readonly Color32 cBgScroll  = new(14,   9,   5,  255);
    static readonly Color32 cBgDetail  = new(20,  13,   8,  255);
    static readonly Color32 cAmber     = new(202, 136,  18,  255);
    static readonly Color32 cAmberDark = new(120,  78,   8,  255);
    static readonly Color32 cCream     = new(229, 212, 168,  255);
    static readonly Color32 cDim       = new(140, 124,  90,  255);
    static readonly Color32 cBorder    = new(72,   52,  24,  255);
    static readonly Color32 cSlotNorm  = new(38,   27,  15,  255);

    // ── 저장 경로 ───────────────────────────────────────────────
    string pathCraftingUI = "Assets/03_Prefabs/UI/Crafting/CraftingUI.prefab";
    string pathRecipeSlot = "Assets/03_Prefabs/UI/Crafting/CraftingRecipeSlot.prefab";
    string pathIngredSlot = "Assets/03_Prefabs/UI/Crafting/CraftingIngredientSlot.prefab";

    [MenuItem("Tools/Crafting UI Builder")]
    static void Open() => GetWindow<CraftingUIBuilder>("Crafting UI Builder");

    void OnGUI()
    {
        GUILayout.Label("Don't Starve Style — Crafting UI Builder", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);
        pathCraftingUI = EditorGUILayout.TextField("Crafting Panel", pathCraftingUI);
        pathRecipeSlot = EditorGUILayout.TextField("Recipe Slot",    pathRecipeSlot);
        pathIngredSlot = EditorGUILayout.TextField("Ingred. Slot",   pathIngredSlot);
        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "씬에 Canvas가 있어야 합니다.\n생성 후 CraftingUI를 Canvas 하위로 드래그하세요.",
            MessageType.Info);
        EditorGUILayout.Space(6);
        GUI.backgroundColor = new Color(0.8f, 0.6f, 0.1f);
        if (GUILayout.Button("UI 생성 + 프리팹 저장", GUILayout.Height(36)))
            Build();
        GUI.backgroundColor = Color.white;
    }

    void Build()
    {
        EnsureDir(pathIngredSlot);
        EnsureDir(pathRecipeSlot);
        EnsureDir(pathCraftingUI);

        // 서브 프리팹 저장
        var ingredGO = MakeIngredientSlot();
        PrefabUtility.SaveAsPrefabAsset(ingredGO, pathIngredSlot);
        DestroyImmediate(ingredGO);

        var recipeGO = MakeRecipeSlot();
        PrefabUtility.SaveAsPrefabAsset(recipeGO, pathRecipeSlot);
        DestroyImmediate(recipeGO);

        AssetDatabase.Refresh();

        var ingredPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(pathIngredSlot);
        var recipePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(pathRecipeSlot);

        if (ingredPrefab == null || recipePrefab == null)
        {
            Debug.LogError("[CraftingUIBuilder] 서브 프리팹 로드 실패. 경로를 확인하세요.");
            return;
        }

        var panelGO = MakeCraftingPanel(recipePrefab, ingredPrefab);
        PrefabUtility.SaveAsPrefabAssetAndConnect(panelGO, pathCraftingUI, InteractionMode.AutomatedAction);

        AssetDatabase.Refresh();
        Selection.activeGameObject = panelGO;
        EditorUtility.DisplayDialog("완료",
            "CraftingUI 생성 완료!\nCanvas 하위로 이동 후 Canvas Scaler를 설정하세요.", "확인");
    }

    // ═══════════════════════════════════════════════════════════
    //  재료 슬롯 프리팹
    // ═══════════════════════════════════════════════════════════

    GameObject MakeIngredientSlot()
    {
        var root = new GameObject("CraftingIngredientSlot");  // RectTransform 수동 추가
        var rootRT = root.AddComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(62f, 80f);

        var icon = MakeImg(root, "Icon", Color.white);
        icon.preserveAspect = true;
        SetRect(RT(icon), V(0,0), V(1,1), V(.5f,.5f), V(4,20), V(-4,-2));

        var nameTxt = MakeTMP(root, "NameText", 8f, cDim);
        nameTxt.alignment = TextAlignmentOptions.Bottom;
        nameTxt.overflowMode = TextOverflowModes.Ellipsis;
        SetRect(RT(nameTxt), V(0,0), V(1,0), V(.5f,0), V(2,0), V(-2,20));

        var cntTxt = MakeTMP(root, "CountText", 10f, cCream);
        cntTxt.fontStyle = FontStyles.Bold;
        cntTxt.alignment = TextAlignmentOptions.BottomRight;
        SetRect(RT(cntTxt), V(0,0), V(1,1), V(1,0), V(0,20), V(-2,2));

        var comp = root.AddComponent<CraftingIngredientSlotUI>();
        WireField(comp, "iconImage", icon);
        WireField(comp, "nameText",  nameTxt);
        WireField(comp, "countText", cntTxt);
        return root;
    }

    // ═══════════════════════════════════════════════════════════
    //  레시피 슬롯 프리팹
    // ═══════════════════════════════════════════════════════════

    GameObject MakeRecipeSlot()
    {
        var root   = new GameObject("CraftingRecipeSlot");
        var rootRT = root.AddComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(SlotSize, SlotSize);

        // 테두리
        var border = MakeImg(root, "Border", cBorder);
        SetRect(RT(border), V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));

        // 배경 (실제 색상 담당, CraftingRecipeSlotUI.background 연결)
        var bg = MakeImg(root, "Background", cSlotNorm);
        SetRect(RT(bg), V(0,0), V(1,1), V(.5f,.5f), V(1,1), V(-1,-1));

        // 버튼
        var btn = root.AddComponent<Button>();
        btn.targetGraphic = bg;
        var bc = btn.colors;
        bc.normalColor      = Color.white;
        bc.highlightedColor = new Color32(255, 220, 140, 255);
        bc.pressedColor     = cAmber;
        bc.disabledColor    = new Color32(80, 60, 30, 200);
        btn.colors = bc;

        // 아이콘
        var icon = MakeImg(root, "Icon", Color.white);
        icon.preserveAspect = true;
        SetRect(RT(icon), V(0,0), V(1,1), V(.5f,.5f), V(8,8), V(-8,-8));

        // 잠금 오버레이
        var lockGO  = new GameObject("LockOverlay");
        lockGO.transform.SetParent(root.transform, false);
        var lockRT  = lockGO.AddComponent<RectTransform>();
        lockGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);
        SetRect(lockRT, V(0,0), V(1,1), V(.5f,.5f), V(1,1), V(-1,-1));
        lockGO.SetActive(false);

        // 해금 뱃지 (왼쪽 상단 앰버 점)
        var badgeGO = new GameObject("LearnedBadge");
        badgeGO.transform.SetParent(root.transform, false);
        var badgeRT = badgeGO.AddComponent<RectTransform>();
        badgeGO.AddComponent<Image>().color = cAmber;
        badgeRT.anchorMin = badgeRT.anchorMax = V(0,1);
        badgeRT.pivot     = V(0,1);
        badgeRT.sizeDelta = V(10,10);
        badgeRT.anchoredPosition = V(2,-2);
        badgeGO.SetActive(false);

        var comp = root.AddComponent<CraftingRecipeSlotUI>();
        WireField(comp, "iconImage",    icon);
        WireField(comp, "background",   bg);
        WireField(comp, "lockOverlay",  lockGO);
        WireField(comp, "learnedBadge", badgeGO);
        return root;
    }

    // ═══════════════════════════════════════════════════════════
    //  메인 크래프팅 패널
    // ═══════════════════════════════════════════════════════════

    GameObject MakeCraftingPanel(GameObject recipePrefab, GameObject ingredPrefab)
    {
        // ── 루트 패널 ────────────────────────────────────────────
        var root   = new GameObject("CraftingUI");
        var rootRT = root.AddComponent<RectTransform>();
        rootRT.anchorMin        = V(0,0);
        rootRT.anchorMax        = V(0,1);
        rootRT.pivot            = V(0,.5f);
        rootRT.sizeDelta        = V(PanelW, 0);
        rootRT.anchoredPosition = V(0,0);
        root.AddComponent<Image>().color = cBgPanel;

        // 우측 경계선
        {
            var go  = Child(root, "RightBorder");
            go.AddComponent<Image>().color = cBorder;
            var rt = RT(go);
            rt.anchorMin = V(1,0); rt.anchorMax = V(1,1);
            rt.pivot = V(1,.5f); rt.sizeDelta = V(2,0); rt.anchoredPosition = V(0,0);
        }

        // ── 카테고리 탭 행 ───────────────────────────────────────
        string[] tabLabels = { "도구", "광원", "생존", "무기", "건물", "달" };
        var tabRow = Child(root, "CategoryTabsRow");
        tabRow.AddComponent<Image>().color = cBgTabRow;
        SetRect(RT(tabRow), V(0,1), V(1,1), V(.5f,1), V(0,-TabRowH), V(0,0));

        var hlg = tabRow.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(2,2,2,2);
        hlg.spacing = 1f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = true;

        // 탭 행 하단 강조선 (탭 행 밖, root 기준)
        {
            var go = Child(root, "TabAccentLine");
            go.AddComponent<Image>().color = cAmber;
            SetRect(RT(go), V(0,1), V(1,1), V(.5f,1), V(0,-TabRowH-2), V(0,-TabRowH));
        }

        var tabButtons = new Button[tabLabels.Length];
        for (int i = 0; i < tabLabels.Length; i++)
        {
            var tab    = Child(tabRow, $"Tab_{tabLabels[i]}");
            var tabImg = tab.AddComponent<Image>();
            tabImg.color = cBgTabRow;
            var tabBtn = tab.AddComponent<Button>();
            tabBtn.targetGraphic = tabImg;
            var tc = tabBtn.colors;
            tc.normalColor      = Color.white;
            tc.highlightedColor = new Color32(200,160,60,255);
            tc.pressedColor     = cAmber;
            tabBtn.colors = tc;

            var lbl = MakeTMP(tab, "Label", 9f, cDim);
            lbl.text = tabLabels[i];
            lbl.alignment = TextAlignmentOptions.Center;
            SetRect(RT(lbl), V(0,0), V(1,1), V(.5f,.5f), V(1,1), V(-1,-1));

            tabButtons[i] = tabBtn;
        }

        // ── 필터 버튼 ────────────────────────────────────────────
        var filterGO  = Child(root, "FilterToggleButton");
        var filterImg = filterGO.AddComponent<Image>();
        filterImg.color = cBgTabRow;
        var filterBtn = filterGO.AddComponent<Button>();
        filterBtn.targetGraphic = filterImg;
        {
            var rt = RT(filterGO);
            rt.anchorMin = rt.anchorMax = V(1,1);
            rt.pivot = V(1,1);
            rt.sizeDelta = V(96, FilterBarH);
            rt.anchoredPosition = V(-2, -(TabRowH + 2f));
        }
        var filterTxt = MakeTMP(filterGO, "FilterText", 9f, cDim);
        filterTxt.text = "전체 보기 [F]";
        filterTxt.alignment = TextAlignmentOptions.Center;
        SetRect(RT(filterTxt), V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));

        // ── 스크롤 뷰 ────────────────────────────────────────────
        float scrollTop = TabRowH + FilterBarH + 4f;
        float scrollBot = DetailH;

        var scrollGO = Child(root, "RecipeScrollView");
        scrollGO.AddComponent<Image>().color = cBgScroll;
        SetRect(RT(scrollGO), V(0,0), V(1,1), V(.5f,.5f), V(0,scrollBot), V(0,-scrollTop));

        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal        = false;
        scrollRect.vertical          = true;
        scrollRect.movementType      = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 25f;

        var viewport = Child(scrollGO, "Viewport");
        viewport.AddComponent<RectMask2D>();
        SetRect(RT(viewport), V(0,0), V(1,1), V(0,1), V(0,0), V(0,0));

        var grid   = Child(viewport, "RecipeListContainer");
        var gridRT = RT(grid);
        gridRT.anchorMin = V(0,1); gridRT.anchorMax = V(1,1);
        gridRT.pivot = V(.5f,1); gridRT.sizeDelta = V(0,0);

        int cols = Mathf.Max(2, Mathf.FloorToInt((PanelW - Pad*2 + SlotGap) / (SlotSize + SlotGap)));
        var glg = grid.AddComponent<GridLayoutGroup>();
        glg.cellSize        = V(SlotSize, SlotSize);
        glg.spacing         = V(SlotGap, SlotGap);
        glg.padding         = new RectOffset((int)Pad, (int)Pad, (int)Pad, (int)Pad);
        glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = cols;
        glg.childAlignment  = TextAnchor.UpperLeft;

        var csf = grid.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content  = gridRT;
        scrollRect.viewport = RT(viewport);

        // ── 상세 패널 ────────────────────────────────────────────
        var detail = Child(root, "DetailPanel");
        detail.AddComponent<Image>().color = cBgDetail;
        SetRect(RT(detail), V(0,0), V(1,0), V(.5f,0), V(0,0), V(0,DetailH));

        // 상단 경계선
        {
            var go = Child(detail, "TopBorder");
            go.AddComponent<Image>().color = cBorder;
            SetRect(RT(go), V(0,1), V(1,1), V(.5f,1), V(0,-2), V(0,0));
        }

        // 결과 아이콘 배경
        {
            var go = Child(detail, "ResultIconBg");
            go.AddComponent<Image>().color = new Color32(12,8,4,200);
            var rt = RT(go);
            rt.anchorMin = rt.anchorMax = V(0,1);
            rt.pivot = V(0,1);
            rt.sizeDelta = V(IconSize+4, IconSize+4);
            rt.anchoredPosition = V(Pad-2, -(Pad-2));
        }

        // 결과 아이콘
        var resIconGO  = Child(detail, "ResultIcon");
        var resIconImg = resIconGO.AddComponent<Image>();
        resIconImg.preserveAspect = true;
        {
            var rt = RT(resIconGO);
            rt.anchorMin = rt.anchorMax = V(0,1);
            rt.pivot = V(0,1);
            rt.sizeDelta = V(IconSize, IconSize);
            rt.anchoredPosition = V(Pad, -Pad);
        }

        float textX = Pad + IconSize + 8f;

        // 결과 이름
        var resNameTxt = MakeTMP(detail, "ResultNameText", 14f, cCream);
        resNameTxt.fontStyle = FontStyles.Bold;
        resNameTxt.alignment = TextAlignmentOptions.TopLeft;
        resNameTxt.overflowMode = TextOverflowModes.Ellipsis;
        resNameTxt.enableWordWrapping = false;
        SetRect(RT(resNameTxt), V(0,1), V(1,1), V(0,1), V(textX,-Pad), V(-Pad,-Pad-22));

        // 결과 수량
        var resAmtTxt = MakeTMP(detail, "ResultAmountText", 11f, cDim);
        resAmtTxt.alignment = TextAlignmentOptions.TopRight;
        SetRect(RT(resAmtTxt), V(0,1), V(1,1), V(1,1), V(textX,-Pad), V(-Pad,-Pad-20));

        // 스테이션 힌트
        var stationTxt = MakeTMP(detail, "StationHintText", 9f, cDim);
        stationTxt.alignment = TextAlignmentOptions.TopLeft;
        SetRect(RT(stationTxt), V(0,1), V(1,1), V(0,1), V(textX,-Pad-22), V(-Pad,-Pad-36));

        // 구분선
        {
            var go = Child(detail, "Divider");
            go.AddComponent<Image>().color = cBorder;
            SetRect(RT(go), V(0,0), V(1,0), V(.5f,0), V(Pad,76), V(-Pad,78));
        }

        // 재료 컨테이너
        var ingCont = Child(detail, "IngredientContainer");
        var ingContRT = RT(ingCont);
        ingContRT.anchorMin = V(0,0); ingContRT.anchorMax = V(1,0);
        ingContRT.pivot = V(.5f,0);
        ingContRT.sizeDelta = V(0, 76);
        ingContRT.anchoredPosition = V(0,38);

        var ingHLG = ingCont.AddComponent<HorizontalLayoutGroup>();
        ingHLG.padding = new RectOffset((int)Pad, (int)Pad, 4, 4);
        ingHLG.spacing = 4f;
        ingHLG.childAlignment = TextAnchor.MiddleLeft;
        ingHLG.childControlWidth  = false;
        ingHLG.childControlHeight = true;
        ingHLG.childForceExpandHeight = true;

        // 제작 버튼
        var craftBtnGO  = Child(detail, "CraftButton");
        var craftBtnImg = craftBtnGO.AddComponent<Image>();
        craftBtnImg.color = cAmberDark;
        var craftBtnRT  = RT(craftBtnGO);
        craftBtnRT.anchorMin = V(0,0); craftBtnRT.anchorMax = V(1,0);
        craftBtnRT.pivot = V(.5f,0);
        craftBtnRT.sizeDelta = V(-Pad*2, 34);
        craftBtnRT.anchoredPosition = V(0,3);

        var craftBtn = craftBtnGO.AddComponent<Button>();
        craftBtn.targetGraphic = craftBtnImg;
        var cc = craftBtn.colors;
        cc.normalColor      = Color.white;
        cc.highlightedColor = new Color32(255, 210, 70, 255);
        cc.pressedColor     = new Color32(160, 100, 10, 255);
        cc.disabledColor    = new Color32(80, 60, 30, 180);
        craftBtn.colors = cc;

        var craftTxt = MakeTMP(craftBtnGO, "CraftButtonText", 13f, cCream);
        craftTxt.text = "제작 [Space]"; craftTxt.fontStyle = FontStyles.Bold;
        craftTxt.alignment = TextAlignmentOptions.Center;
        SetRect(RT(craftTxt), V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));

        // ── CraftingUI 컴포넌트 연결 ─────────────────────────────
        var craftingUI = root.AddComponent<CraftingUI>();
        var so = new SerializedObject(craftingUI);

        var tabsProp = so.FindProperty("categoryTabButtons");
        tabsProp.arraySize = tabButtons.Length;
        for (int i = 0; i < tabButtons.Length; i++)
            tabsProp.GetArrayElementAtIndex(i).objectReferenceValue = tabButtons[i];

        so.FindProperty("recipeListContainer").objectReferenceValue   = gridRT;
        so.FindProperty("recipeSlotPrefab").objectReferenceValue      = recipePrefab;
        so.FindProperty("detailPanel").objectReferenceValue           = detail;
        so.FindProperty("resultIcon").objectReferenceValue            = resIconImg;
        so.FindProperty("resultNameText").objectReferenceValue        = resNameTxt;
        so.FindProperty("resultAmountText").objectReferenceValue      = resAmtTxt;
        so.FindProperty("ingredientContainer").objectReferenceValue   = ingContRT;
        so.FindProperty("ingredientSlotPrefab").objectReferenceValue  = ingredPrefab;
        so.FindProperty("craftButton").objectReferenceValue           = craftBtn;
        so.FindProperty("craftButtonText").objectReferenceValue       = craftTxt;
        so.FindProperty("filterToggleButton").objectReferenceValue    = filterBtn;
        so.FindProperty("filterButtonText").objectReferenceValue      = filterTxt;
        so.FindProperty("stationHintText").objectReferenceValue       = stationTxt;

        so.ApplyModifiedProperties();
        return root;
    }

    // ═══════════════════════════════════════════════════════════
    //  헬퍼
    // ═══════════════════════════════════════════════════════════

    // 자식 GO 생성 (RectTransform 포함)
    static GameObject Child(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static Image MakeImg(GameObject parent, string name, Color32 color)
    {
        var go  = Child(parent, name);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    static TextMeshProUGUI MakeTMP(GameObject parent, string name, float size, Color32 color)
    {
        var go  = Child(parent, name);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize      = size;
        tmp.color         = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 offMin, Vector2 offMax)
    {
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.pivot     = pivot;
        rt.offsetMin = offMin; rt.offsetMax = offMax;
    }

    static RectTransform RT(Component c)  => c.GetComponent<RectTransform>();
    static RectTransform RT(GameObject go) => go.GetComponent<RectTransform>();
    static Vector2 V(float x, float y)    => new(x, y);

    static void WireField(Object comp, string field, Object value)
    {
        var so   = new SerializedObject(comp);
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogWarning($"[CraftingUIBuilder] 필드 없음: {field}"); return; }
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
