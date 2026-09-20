#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 상자/냉장고 보관함 팝업 UI를 자동 생성하는 에디터 도구.
/// 메뉴: Tools → Chest UI Builder
/// 생성 후 Addressables에 "UI_Popup_Chest" 키로 등록해야 Extensions.ShowPopup&lt;UI_Popup_Chest&gt;()가 찾는다.
/// </summary>
public class ChestUIBuilder : EditorWindow
{
    const int Columns = 4;
    const int Rows = 4;
    const float SlotSize = 64f;
    const float SlotGap = 6f;
    const float Pad = 12f;
    const float TitleBarH = 40f;
    const float PanelW = Columns * SlotSize + (Columns - 1) * SlotGap + Pad * 2;
    const float PanelH = TitleBarH + Rows * SlotSize + (Rows - 1) * SlotGap + Pad * 2;

    static readonly Color32 cBg       = new(26, 17, 12, 242);
    static readonly Color32 cTitleBar = new(18, 12, 7, 255);
    static readonly Color32 cSlot     = new(38, 27, 15, 255);
    static readonly Color32 cBorder   = new(72, 52, 24, 255);
    static readonly Color32 cCream    = new(229, 212, 168, 255);
    static readonly Color32 cAmber    = new(202, 136, 18, 255);

    string pathPopup = "Assets/03_Prefabs/UI/Structures/UI_Popup_Chest.prefab";
    string pathSlot  = "Assets/03_Prefabs/UI/Structures/StorageSlot.prefab";

    [MenuItem("Tools/Chest UI Builder")]
    static void Open() => GetWindow<ChestUIBuilder>("Chest UI Builder");

    void OnGUI()
    {
        GUILayout.Label("보관함(상자/냉장고) 팝업 UI 빌더", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);
        pathPopup = EditorGUILayout.TextField("Popup 저장 경로", pathPopup);
        pathSlot  = EditorGUILayout.TextField("Slot 저장 경로", pathSlot);
        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "생성 후 Window > Asset Management > Addressables > Groups 에서\n" +
            "UI_Popup_Chest.prefab을 Addressable로 등록하고 주소를 \"UI_Popup_Chest\"로 지정하세요.",
            MessageType.Info);
        EditorGUILayout.Space(6);
        GUI.backgroundColor = new Color(0.8f, 0.6f, 0.1f);
        if (GUILayout.Button("UI 생성 + 프리팹 저장", GUILayout.Height(36)))
            Build();
        GUI.backgroundColor = Color.white;
    }

    void Build()
    {
        EnsureDir(pathSlot);
        EnsureDir(pathPopup);

        var slotGO = MakeSlot();
        PrefabUtility.SaveAsPrefabAsset(slotGO, pathSlot);
        DestroyImmediate(slotGO);
        AssetDatabase.Refresh();

        var slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(pathSlot);
        if (slotPrefab == null)
        {
            Debug.LogError("[ChestUIBuilder] 슬롯 프리팹 로드 실패.");
            return;
        }

        var panelGO = MakePanel(slotPrefab);
        PrefabUtility.SaveAsPrefabAssetAndConnect(panelGO, pathPopup, InteractionMode.AutomatedAction);

        AssetDatabase.Refresh();
        Selection.activeGameObject = panelGO;
        EditorUtility.DisplayDialog("완료",
            "UI_Popup_Chest 생성 완료!\nAddressables에 \"UI_Popup_Chest\" 키로 등록하세요.", "확인");
    }

    // ── 슬롯 1칸 ──────────────────────────────────────────────
    GameObject MakeSlot()
    {
        var root = new GameObject("StorageSlot");
        var rootRT = root.AddComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(SlotSize, SlotSize);

        var bg = MakeImg(root, "Background", cSlot);
        SetRect(RT(bg), V(0, 0), V(1, 1), V(.5f, .5f), V(0, 0), V(0, 0));

        var border = root.AddComponent<Outline>();
        border.effectColor = cBorder;
        border.effectDistance = new Vector2(1, -1);

        var icon = MakeImg(root, "Icon", Color.white);
        icon.preserveAspect = true;
        SetRect(RT(icon), V(0, 0), V(1, 1), V(.5f, .5f), V(6, 6), V(-6, -6));

        var stackBG = Child(root, "StackBG");
        stackBG.AddComponent<Image>().color = new Color(0, 0, 0, 0.55f);
        SetRect(RT(stackBG), V(1, 0), V(1, 0), V(1, 0), V(-22, 0), V(0, 16));

        var stackTxt = MakeTMP(root, "StackText", 12f, cCream);
        stackTxt.alignment = TextAlignmentOptions.BottomRight;
        stackTxt.fontStyle = FontStyles.Bold;
        SetRect(RT(stackTxt), V(0, 0), V(1, 1), V(1, 0), V(0, 0), V(-3, 1));

        var comp = root.AddComponent<StorageSlotUI>();
        WireField(comp, "iconImage", icon);
        WireField(comp, "stackText", stackTxt);
        WireField(comp, "stackBG", stackBG);
        return root;
    }

    // ── 팝업 패널 ─────────────────────────────────────────────
    GameObject MakePanel(GameObject slotPrefab)
    {
        var root = new GameObject("UI_Popup_Chest");
        var rootRT = root.AddComponent<RectTransform>();
        rootRT.anchorMin = rootRT.anchorMax = V(.5f, .5f);
        rootRT.pivot = V(.5f, .5f);
        rootRT.sizeDelta = V(PanelW, PanelH);
        rootRT.anchoredPosition = V(0, 120); // 화면 중앙보다 살짝 위 — 항상 떠 있는 인벤 바와 안 겹치게

        root.AddComponent<Image>().color = cBg;
        root.AddComponent<CanvasGroup>();

        // 타이틀 바
        var titleBar = Child(root, "TitleBar");
        titleBar.AddComponent<Image>().color = cTitleBar;
        SetRect(RT(titleBar), V(0, 1), V(1, 1), V(.5f, 1), V(0, -TitleBarH), V(0, 0));

        var titleTxt = MakeTMP(titleBar, "titleText", 16f, cCream);
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.alignment = TextAlignmentOptions.MidlineLeft;
        titleTxt.text = "상자";
        SetRect(RT(titleTxt), V(0, 0), V(1, 1), V(0, .5f), V(Pad, 0), V(-40, 0));

        // 닫기 버튼
        var closeGO = Child(titleBar, "CloseButton");
        var closeImg = closeGO.AddComponent<Image>();
        closeImg.color = cAmber;
        var closeBtn = closeGO.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        SetRect(RT(closeGO), V(1, .5f), V(1, .5f), V(1, .5f), V(-28, -12), V(-4, 12));

        var closeTxt = MakeTMP(closeGO, "X", 14f, Color.black);
        closeTxt.text = "X";
        closeTxt.fontStyle = FontStyles.Bold;
        closeTxt.alignment = TextAlignmentOptions.Center;
        SetRect(RT(closeTxt), V(0, 0), V(1, 1), V(.5f, .5f), V(0, 0), V(0, 0));

        // 슬롯 그리드
        var grid = Child(root, "SlotGrid");
        SetRect(RT(grid), V(0, 0), V(1, 1), V(.5f, .5f), V(Pad, Pad), V(-Pad, -Pad - TitleBarH));

        var glg = grid.AddComponent<GridLayoutGroup>();
        glg.cellSize = V(SlotSize, SlotSize);
        glg.spacing = V(SlotGap, SlotGap);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = Columns;
        glg.childAlignment = TextAnchor.UpperLeft;

        var slotUIs = new StorageSlotUI[Columns * Rows];
        for (int i = 0; i < slotUIs.Length; i++)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab, grid.transform);
            slotUIs[i] = instance.GetComponent<StorageSlotUI>();
        }

        var popup = root.AddComponent<UI_Popup_Chest>();
        WireField(popup, "titleText", titleTxt);
        WireField(popup, "closeButton", closeBtn);

        var so = new SerializedObject(popup);
        var slotsProp = so.FindProperty("slotUIs");
        slotsProp.arraySize = slotUIs.Length;
        for (int i = 0; i < slotUIs.Length; i++)
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotUIs[i];
        so.ApplyModifiedProperties();

        return root;
    }

    // ── 헬퍼 (CraftingUIBuilder와 동일 패턴) ───────────────────
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
            tmp.font = koreanFont; // 기본 LiberationSans SDF는 한글 글리프가 없어서 네모로 뜬다
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
        if (prop == null) { Debug.LogWarning($"[ChestUIBuilder] 필드 없음: {field}"); return; }
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
