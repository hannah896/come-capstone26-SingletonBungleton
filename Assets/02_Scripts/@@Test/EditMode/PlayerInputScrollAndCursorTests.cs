using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerInputScrollAndCursorTests : InputTestFixture
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private GameObject _root;
    private GameObject _stubMainGo;
    private FieldInfo _mainInstanceField;
    private FieldInfo _mainInitializedField;
    private object _prevMainInstance;
    private bool _prevMainInitialized;
    private PropertyInfo _gameProcessingProp;
    private object _prevGameProcessing;
    private EventSystem _prevEventSystem;
    private object _actions;
    private object _handler;

    [SetUp]
    public override void Setup()
    {
        base.Setup();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // PlayerFirstPersonCameraController / UI_Hud_Player가 EditMode에서 Main.Instance 초기화를 유발해 DontDestroyOnLoad에 걸리지 않게 스텁한다.
        Type mainType = FindType("Main");
        _mainInstanceField = mainType.GetField("_instance", StaticMembers);
        _mainInitializedField = mainType.GetField("_initialized", StaticMembers);
        _prevMainInstance = _mainInstanceField.GetValue(null);
        _prevMainInitialized = (bool)_mainInitializedField.GetValue(null);

        _stubMainGo = new GameObject("TestMainStub");
        _stubMainGo.SetActive(false);
        object mainStub = _stubMainGo.AddComponent(mainType);
        _mainInstanceField.SetValue(null, mainStub);
        _mainInitializedField.SetValue(null, true);

        Type gameSceneType = FindType("GameScene");
        Type processingType = FindType("GameProcessing");
        _gameProcessingProp = gameSceneType.GetProperty("GameProcessing", StaticMembers);
        _prevGameProcessing = _gameProcessingProp.GetValue(null);
        _gameProcessingProp.SetValue(null, Enum.Parse(processingType, "Processing"));

        _prevEventSystem = EventSystem.current;
        _root = new GameObject("PlayerInputScrollAndCursorTestsRoot", typeof(RectTransform));
    }

    [TearDown]
    public override void TearDown()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (_handler != null) Invoke(_handler, "Disconnect");
        if (_actions != null)
        {
            Invoke(_actions, "Disable");
            UnityEngine.Object.DestroyImmediate((UnityEngine.Object)Property(_actions, "asset"));
        }
        _handler = _actions = null;

        if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        if (_stubMainGo != null) UnityEngine.Object.DestroyImmediate(_stubMainGo);

        EventSystem.current = _prevEventSystem;
        if (_gameProcessingProp != null) _gameProcessingProp.SetValue(null, _prevGameProcessing);
        if (_mainInstanceField != null) _mainInstanceField.SetValue(null, _prevMainInstance);
        if (_mainInitializedField != null) _mainInitializedField.SetValue(null, _prevMainInitialized);

        base.TearDown();
    }

    [Test]
    public void ScrollWheel_BlocksOverScrollableAndNonInventoryUI_AllowsOverWorldAndInventory()
    {
        SetupUiCanvasAndInput(
            out object manager,
            out object inputData,
            out Mouse mouse,
            out Vector2 scrollRectPos,
            out Vector2 plainUiPos,
            out Vector2 inventoryPos,
            out Vector2 nestedScrollPos,
            out Vector2 worldPos);

        // 크래프팅·기타 UI·인벤토리 내부 ScrollRect에서는 퀵슬롯 스크롤이 차단되고,
        // 인벤토리 일반 영역과 3D 월드(및 커서 잠금 상태)에서는 허용된다.
        Assert.That(TriggerScrollAndReadDelta(mouse, inputData, scrollRectPos), Is.EqualTo(0),
            "ScrollRect 위에서는 퀵슬롯 스크롤이 차단되어야 한다.");
        Assert.That(TriggerScrollAndReadDelta(mouse, inputData, plainUiPos), Is.EqualTo(0),
            "인벤토리가 아닌 일반 UI 위에서는 퀵슬롯 스크롤이 차단되어야 한다.");
        Assert.That(TriggerScrollAndReadDelta(mouse, inputData, nestedScrollPos), Is.EqualTo(0),
            "인벤토리 하위라도 ScrollRect 위에서는 퀵슬롯 스크롤이 차단되어야 한다.");
        Assert.That(TriggerScrollAndReadDelta(mouse, inputData, inventoryPos), Is.EqualTo(-1),
            "UI_Panel_PlayerInventory 위에서는 퀵슬롯 스크롤이 허용되어야 한다.");
        Assert.That(TriggerScrollAndReadDelta(mouse, inputData, worldPos), Is.EqualTo(-1),
            "UI가 없는 3D 월드 위에서는 퀵슬롯 스크롤이 허용되어야 한다.");

        // 우클릭 시점 회전 중 커서가 화면 중앙에 잠겨 있으면 마우스 좌표와 무관하게 퀵슬롯 스크롤을 허용한다.
        Assert.That((bool)Invoke(manager, "IsPointerOverUI", scrollRectPos), Is.True);
        Cursor.lockState = CursorLockMode.Locked;
        Assert.That(TriggerScrollAndReadDelta(mouse, inputData, scrollRectPos), Is.EqualTo(-1),
            "CursorLockMode.Locked 상태에서는 UI 위치 위에서도 퀵슬롯 스크롤이 허용되어야 한다.");
    }

    [Test]
    public void RotateView_HidesCursorOnWorldRightClick_IgnoresUIAndRestoresOnReleaseOrDisconnect()
    {
        SetupUiCanvasAndInput(
            out object _,
            out object inputData,
            out Mouse mouse,
            out Vector2 _,
            out Vector2 plainUiPos,
            out Vector2 _,
            out Vector2 _,
            out Vector2 worldPos);

        // UI 위에서 우클릭 시작 시 시야 회전을 시작하지 않는다.
        Set(mouse.position, plainUiPos);
        Press(mouse.rightButton);
        Assert.That((bool)Property(inputData, "RotateViewHeld"), Is.False,
            "UI 위 우클릭에서는 RotateViewHeld가 켜지지 않아야 한다.");
        Release(mouse.rightButton);

        // 3D 월드에서 우클릭 시작 시 RotateViewHeld가 활성화된다.
        Set(mouse.position, worldPos);
        Press(mouse.rightButton);
        Assert.That((bool)Property(inputData, "RotateViewHeld"), Is.True,
            "월드 위 우클릭에서는 RotateViewHeld가 켜져야 한다.");

        var bodyGo = new GameObject("PlayerBody");
        bodyGo.transform.SetParent(_root.transform, false);
        var eyeGo = new GameObject("EyePivot");
        eyeGo.transform.SetParent(bodyGo.transform, false);

        var camGo = new GameObject("PlayerFirstPersonCamera");
        camGo.transform.SetParent(_root.transform, false);
        camGo.SetActive(false);
        var controller = camGo.AddComponent(FindType("PlayerFirstPersonCameraController"));
        controller.GetType().GetField("eyePivot", Members).SetValue(controller, eyeGo.transform);
        Invoke(controller, "SetLocalView", true);
        Invoke(controller, "Bind", inputData, bodyGo.transform);
        Invoke(controller, "SetMouseSensitivity", 2f);

        // 진입 프레임: 커서 숨김/잠금 및 첫 프레임 델타 무시
        PropertySetter(inputData, "LookInput", new Vector2(80f, 40f));
        Invoke(controller, "OnLateUpdateLoop", 0.016f);
        Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
        Assert.That(Cursor.visible, Is.False);
        Assert.That((bool)Field(controller, "isCursorHiddenForRotate"), Is.True);
        Assert.That(Quaternion.Angle(bodyGo.transform.rotation, Quaternion.identity), Is.LessThan(0.001f));
        Assert.That(Quaternion.Angle(eyeGo.transform.localRotation, Quaternion.identity), Is.LessThan(0.001f));

        // 워프 델타 억제 프레임(Locked 직후 1프레임 스킵)
        PropertySetter(inputData, "LookInput", new Vector2(150f, -60f));
        Invoke(controller, "OnLateUpdateLoop", 0.016f);
        Assert.That(Quaternion.Angle(bodyGo.transform.rotation, Quaternion.identity), Is.LessThan(0.001f));
        Assert.That(Quaternion.Angle(eyeGo.transform.localRotation, Quaternion.identity), Is.LessThan(0.001f));

        // 3번째 프레임부터 정상 회전 적용
        PropertySetter(inputData, "LookInput", new Vector2(10f, 5f));
        Invoke(controller, "OnLateUpdateLoop", 0.016f);
        Assert.That(Quaternion.Angle(bodyGo.transform.rotation, Quaternion.identity), Is.GreaterThan(1f));
        Assert.That(Quaternion.Angle(eyeGo.transform.localRotation, Quaternion.identity), Is.GreaterThan(1f));

        // 우클릭 해제 시 커서 복구
        Release(mouse.rightButton);
        Assert.That((bool)Property(inputData, "RotateViewHeld"), Is.False);
        Invoke(controller, "OnLateUpdateLoop", 0.016f);
        Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
        Assert.That(Cursor.visible, Is.True);
        Assert.That((bool)Field(controller, "isCursorHiddenForRotate"), Is.False);

        // 포커스 이탈(OnApplicationFocus(false)) 시 RotateViewHeld 해제 및 커서 복구
        PropertySetter(inputData, "RotateViewHeld", true);
        Invoke(controller, "OnLateUpdateLoop", 0.016f);
        Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
        Invoke(controller, "OnApplicationFocus", false);
        Assert.That((bool)Property(inputData, "RotateViewHeld"), Is.False);
        Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
        Assert.That(Cursor.visible, Is.True);

        // SetLocalView(false) 및 OnDisable()에서도 커서 복구
        PropertySetter(inputData, "RotateViewHeld", true);
        Invoke(controller, "OnLateUpdateLoop", 0.016f);
        Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
        Invoke(controller, "SetLocalView", false);
        Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
        Assert.That(Cursor.visible, Is.True);

        Invoke(controller, "SetLocalView", true);
        Invoke(controller, "OnLateUpdateLoop", 0.016f);
        Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
        Invoke(controller, "OnDisable");
        Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
        Assert.That(Cursor.visible, Is.True);

        // Disconnect() 시 홀드/룩 상태 초기화
        PropertySetter(inputData, "RotateViewHeld", true);
        PropertySetter(inputData, "LookInput", new Vector2(12f, -4f));
        Invoke(_handler, "Disconnect");
        Assert.That((bool)Property(inputData, "RotateViewHeld"), Is.False);
        Assert.That((Vector2)Property(inputData, "LookInput"), Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void HudFocusCrosshair_DoesNotBlockRaycasts()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/03_Prefabs/UI/Player/UI_Hud_Player.prefab");
        Assert.That(prefab, Is.Not.Null);

        var instance = UnityEngine.Object.Instantiate(prefab, _root.transform, false);
        var hud = instance.GetComponent(FindType("UI_Hud_Player"));
        Assert.That(hud, Is.Not.Null);

        var focusUi = Field(hud, "UI_Image_Focus");
        Assert.That(focusUi, Is.Not.Null);
        Invoke(focusUi, "Initialize");
        var focusImage = (Image)Property(focusUi, "Image");
        Assert.That(focusImage, Is.Not.Null);
        Assert.That(focusImage.raycastTarget, Is.False,
            "UI_Hud_Player.prefab의 중앙 크로스헤어 Image.raycastTarget은 false여야 한다.");

        focusImage.raycastTarget = true;
        Invoke(hud, "ResolveReferences");
        Assert.That(focusImage.raycastTarget, Is.False,
            "ResolveReferences 호출 후에도 크로스헤어 raycastTarget을 false로 유지해야 한다.");
    }

    private void SetupUiCanvasAndInput(
        out object manager,
        out object inputData,
        out Mouse mouse,
        out Vector2 scrollRectPos,
        out Vector2 plainUiPos,
        out Vector2 inventoryPos,
        out Vector2 nestedScrollPos,
        out Vector2 worldPos)
    {
        var eventSystemGo = new GameObject("TestEventSystem", typeof(EventSystem));
        eventSystemGo.transform.SetParent(_root.transform, false);
        EventSystem.current = eventSystemGo.GetComponent<EventSystem>();

        var canvasGo = new GameObject("TestOverlayCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(_root.transform, false);
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        float screenW = Mathf.Max(200f, Screen.width);
        float screenH = Mathf.Max(200f, Screen.height);
        Vector2 boxSize = new Vector2(Mathf.Max(12f, screenW * 0.14f), Mathf.Max(12f, screenH * 0.18f));
        scrollRectPos = new Vector2(screenW * 0.15f, screenH * 0.25f);
        plainUiPos = new Vector2(screenW * 0.38f, screenH * 0.25f);
        inventoryPos = new Vector2(screenW * 0.62f, screenH * 0.25f);
        nestedScrollPos = new Vector2(screenW * 0.85f, screenH * 0.25f);
        worldPos = new Vector2(screenW * 0.50f, screenH * 0.80f);

        CreateUiHitTarget("CraftingScrollPanel", canvasGo.transform, scrollRectPos, boxSize, withScrollRect: true);
        CreateUiHitTarget("PlainUiPanel", canvasGo.transform, plainUiPos, boxSize, withScrollRect: false);

        var inventoryRoot = new GameObject("PlayerInventoryPanel", typeof(RectTransform));
        inventoryRoot.transform.SetParent(canvasGo.transform, false);
        inventoryRoot.AddComponent(FindType("UI_Panel_PlayerInventory"));
        CreateUiHitTarget("InventoryHitArea", inventoryRoot.transform, inventoryPos, boxSize, withScrollRect: false);
        CreateUiHitTarget("NestedInventoryScroll", inventoryRoot.transform, nestedScrollPos, boxSize, withScrollRect: true);

        Canvas.ForceUpdateCanvases();

        manager = Activator.CreateInstance(FindType("InputManager"));
        _actions = Activator.CreateInstance(FindType("InputSystem_Actions"));
        manager.GetType().GetField("_input", Members).SetValue(manager, _actions);

        inputData = Activator.CreateInstance(FindType("PlayerInputData"));
        _handler = Activator.CreateInstance(FindType("InputActions_PlayerInputHandler"), manager);
        Invoke(_handler, "Bind", null, inputData);
        Invoke(_handler, "Connect");
        Invoke(_actions, "Enable");

        mouse = InputSystem.AddDevice<Mouse>();
        Set(mouse.position, worldPos);
        InputSystem.Update();

        // 레이캐스트 전제조건 검증: 4곳의 UI 좌표는 히트하고 월드 좌표는 비어 있어야 한다.
        Assert.That(TryGetTopUIHit(manager, scrollRectPos), Is.True, "ScrollRect UI 히트 확인");
        Assert.That(TryGetTopUIHit(manager, plainUiPos), Is.True, "일반 UI 히트 확인");
        Assert.That(TryGetTopUIHit(manager, inventoryPos), Is.True, "인벤토리 UI 히트 확인");
        Assert.That(TryGetTopUIHit(manager, nestedScrollPos), Is.True, "인벤토리 하위 ScrollRect 히트 확인");
        Assert.That(TryGetTopUIHit(manager, worldPos), Is.False, "월드 빈 좌표에는 UI 히트가 없어야 한다.");
    }

    private static GameObject CreateUiHitTarget(
        string name,
        Transform parent,
        Vector2 screenPos,
        Vector2 size,
        bool withScrollRect)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = screenPos;
        rect.sizeDelta = size;
        go.GetComponent<Image>().raycastTarget = true;
        if (withScrollRect) go.AddComponent<ScrollRect>();
        return go;
    }

    private int TriggerScrollAndReadDelta(Mouse mouse, object inputData, Vector2 screenPos)
    {
        Set(mouse.scroll, Vector2.zero);
        PropertySetter(inputData, "QuickSlotScrollDelta", 0);

        Set(mouse.position, screenPos);
        Set(mouse.scroll, new Vector2(0f, 120f));
        int delta = (int)Property(inputData, "QuickSlotScrollDelta");

        Set(mouse.scroll, Vector2.zero);
        PropertySetter(inputData, "QuickSlotScrollDelta", 0);
        return delta;
    }

    private static bool TryGetTopUIHit(object manager, Vector2 screenPos)
    {
        var args = new object[] { screenPos, default(RaycastResult) };
        MethodInfo method = manager.GetType().GetMethod("TryGetTopUIHit", Members);
        return (bool)method.Invoke(manager, args);
    }

    private static Type FindType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static object Field(object target, string name) => target.GetType().GetField(name, Members).GetValue(target);
    private static object Property(object target, string name) => target.GetType().GetProperty(name, Members).GetValue(target);
    private static void PropertySetter(object target, string name, object value) =>
        target.GetType().GetProperty(name, Members).SetValue(target, value);

    private static object Invoke(object target, string name, params object[] args)
    {
        MethodInfo[] methods = target.GetType().GetMethods(Members);
        foreach (MethodInfo method in methods)
        {
            if (method.Name != name) continue;
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != args.Length) continue;

            bool match = true;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (args[i] == null)
                {
                    if (parameters[i].ParameterType.IsValueType
                        && Nullable.GetUnderlyingType(parameters[i].ParameterType) == null)
                    {
                        match = false;
                        break;
                    }
                    continue;
                }

                if (!parameters[i].ParameterType.IsInstanceOfType(args[i]))
                {
                    match = false;
                    break;
                }
            }

            if (match) return method.Invoke(target, args);
        }

        throw new MissingMethodException(target.GetType().FullName, name);
    }
}
