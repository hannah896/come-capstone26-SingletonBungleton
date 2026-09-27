using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class PlacementControllerTests : InputTestFixture
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly List<UnityEngine.Object> createdObjects = new();
    private Component inventory;
    private Component controller;
    private ScriptableObject structure;
    private Mouse testMouse;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        // 비활성 오브젝트로 실제 게임 초기화와 미리보기 생성을 피한다.
        var player = Track(new GameObject("PlacementController_Test"));
        player.SetActive(false);
        inventory = player.AddComponent(FindType("PlayerInventory"));
        Invoke(inventory, "SetSlotCount", 3);
        structure = CreateItem("Structure", true);
        Invoke(inventory, "AddItem", structure, 2);
        controller = player.AddComponent(FindType("PlacementController"));
        SetField(controller, "playerInventory", inventory);
        Invoke(controller, "BindEvents");
        Assert.That(IsActive, Is.True, "초기 선택 슬롯에서 배치가 시작되어야 한다.");
    }

    [TearDown]
    public override void TearDown()
    {
        if (controller != null) Invoke(controller, "UnbindEvents");
        if (testMouse != null) InputSystem.RemoveDevice(testMouse);
        for (int i = createdObjects.Count - 1; i >= 0; i--)
            if (createdObjects[i] != null) UnityEngine.Object.DestroyImmediate(createdObjects[i]);
        createdObjects.Clear();
        base.TearDown();
    }

    [Test]
    public void SelectEmptySlot_CancelsPlacementWithoutConsumingItem()
    {
        Invoke(inventory, "SelectSlot", 1);
        AssertCanceledWithoutConsumption();
    }

    [Test]
    public void SelectNonPlaceableItem_CancelsPlacementWithoutConsumingItem()
    {
        Invoke(inventory, "AddItem", CreateItem("Resource", false), 1);
        Invoke(inventory, "SelectSlot", 1);
        AssertCanceledWithoutConsumption();
    }

    [Test]
    public void SelectAnotherStructure_ChangesActiveItemAndResetsRotation()
    {
        var other = CreateItem("OtherStructure", true);
        Invoke(inventory, "AddItem", other, 1);
        SetField(controller, "currentRotation", Quaternion.Euler(0f, 90f, 0f));
        Invoke(inventory, "SelectSlot", 1);
        Assert.That(IsActive, Is.True);
        Assert.That(GetField(controller, "activeItemData"), Is.SameAs(other));
        Assert.That(GetField(controller, "currentRotation"), Is.EqualTo(Quaternion.identity));
        Assert.That(Invoke(inventory, "GetItemCount", structure), Is.EqualTo(2));
        Assert.That(Invoke(inventory, "GetItemCount", other), Is.EqualTo(1));
    }

    [TestCase(true, true, true)]
    [TestCase(true, true, false)]
    [TestCase(false, true, false)]
    [TestCase(true, false, false)]
    public void UpdatePlacement_KeepsPlacementRegardlessOfRightClick(bool hasCamera, bool rightClick, bool hasGround)
    {
        testMouse = InputSystem.AddDevice<Mouse>();
        testMouse.MakeCurrent();
        if (hasCamera)
        {
            var cameraObject = Track(new GameObject("PlacementCamera_Test"));
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.pixelRect = new Rect(0f, 0f, 100f, 100f);
            SetField(controller, "placementCamera", camera);
        }
        SetField(controller, "groundLayerMask", (LayerMask)(hasGround ? 1 : 0));
        if (hasGround)
        {
            var ground = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            ground.transform.position = new Vector3(0f, 0f, 10f);
            ground.transform.localScale = new Vector3(10f, 10f, 1f);
            Physics.SyncTransforms();
        }
        InputSystem.QueueStateEvent(testMouse, new MouseState { position = new Vector2(50f, 50f) });
        InputSystem.Update();
        if (rightClick)
        {
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = new Vector2(50f, 50f) }.WithButton(MouseButton.Right));
            InputSystem.Update();
            Assert.That(testMouse.rightButton.wasPressedThisFrame, Is.True);
        }

        object[] hitArgs = { Vector3.zero };
        Assert.That(Invoke(controller, "TryGetPlacementWorldPosition", hitArgs), Is.EqualTo(hasGround));
        Invoke(controller, "UpdatePlacement");
        Assert.That(IsActive, Is.True, "우클릭이나 지면 미검출로 배치가 취소되면 안 된다.");
        Assert.That(GetField(controller, "activeItemData"), Is.SameAs(structure));
        Assert.That(Invoke(inventory, "GetItemCount", structure), Is.EqualTo(2));
    }

    [Test]
    public void MiddleClick_TogglesPreviewBetweenPointerAndCrosshair()
    {
        ConfigureAimTest(new Vector3(20f, 20f, 1f));
        var pointer = new Vector2(85f, 45f);
        UpdateMouse(pointer);
        Invoke(controller, "UpdatePlacement");
        AssertPreviewAt(new Vector3(2.5f, -2.5f, 9.5f));

        UpdateMouse(pointer, true);
        Invoke(controller, "UpdatePlacement");
        AssertPreviewAt(new Vector3(0f, 0f, 9.5f));

        // 버튼을 계속 누르거나 포인터를 움직여도 중앙 배치가 유지된다.
        UpdateMouse(new Vector2(20f, 100f), true);
        Invoke(controller, "UpdatePlacement");
        AssertPreviewAt(new Vector3(0f, 0f, 9.5f));

        UpdateMouse(pointer);
        UpdateMouse(pointer, true);
        Invoke(controller, "UpdatePlacement");
        AssertPreviewAt(new Vector3(2.5f, -2.5f, 9.5f));
        Assert.That(IsActive, Is.True);
        Assert.That(Invoke(inventory, "GetItemCount", structure), Is.EqualTo(2));
    }

    [Test]
    public void MiddleClick_WhenPointerMissesGround_CanSwitchToCrosshairHit()
    {
        ConfigureAimTest(Vector3.one);
        var pointer = new Vector2(85f, 45f);
        UpdateMouse(pointer);
        object[] hitArgs = { Vector3.zero };
        Assert.That(Invoke(controller, "TryGetPlacementWorldPosition", hitArgs), Is.False);

        UpdateMouse(pointer, true);
        Invoke(controller, "UpdatePlacement");
        Assert.That(Invoke(controller, "TryGetPlacementWorldPosition", hitArgs), Is.True);
        AssertPreviewAt(new Vector3(0f, 0f, 9.5f));
    }

    [Test]
    public void CrosshairMode_IsKeptWhenSwitchingSlotsAndReenteringPlacement()
    {
        ConfigureAimTest(new Vector3(20f, 20f, 1f));
        var pointer = new Vector2(85f, 45f);
        UpdateMouse(pointer);
        UpdateMouse(pointer, true);
        Invoke(controller, "UpdatePlacement");
        Invoke(inventory, "SelectSlot", 1);
        Assert.That(IsActive, Is.False);
        Invoke(inventory, "SelectSlot", 0);
        UpdateMouse(pointer);
        Invoke(controller, "UpdatePlacement");
        AssertPreviewAt(new Vector3(0f, 0f, 9.5f));
        Assert.That(IsActive, Is.True);
    }

    private void ConfigureAimTest(Vector3 groundSize)
    {
        testMouse = InputSystem.AddDevice<Mouse>();
        testMouse.MakeCurrent();
        var camera = Track(new GameObject("PlacementAimCamera_Test")).AddComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.pixelRect = new Rect(10f, 20f, 100f, 100f);
        camera.aspect = 1f;
        SetField(controller, "placementCamera", camera);
        SetField(controller, "groundLayerMask", (LayerMask)(1 << 3));
        SetField(structure, "placementSnapToGrid", false);
        var ground = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        ground.layer = 3;
        ground.transform.position = new Vector3(0f, 0f, 10f);
        ground.transform.localScale = groundSize;
        Physics.SyncTransforms();
    }

    private void UpdateMouse(Vector2 position, bool middleClick = false)
    {
        var state = new MouseState { position = position };
        if (middleClick) state = state.WithButton(MouseButton.Middle);
        InputSystem.QueueStateEvent(testMouse, state);
        InputSystem.Update();
    }

    private void AssertPreviewAt(Vector3 expected)
    {
        // currentPosition은 프리뷰 갱신과 실제 설치에 공통으로 전달된다.
        Assert.That(Vector3.Distance((Vector3)GetField(controller, "currentPosition"), expected), Is.LessThan(0.001f));
    }

    private void AssertCanceledWithoutConsumption()
    {
        Assert.That(IsActive, Is.False);
        Assert.That(GetField(controller, "activeItemData"), Is.Null);
        Assert.That(Invoke(inventory, "GetItemCount", structure), Is.EqualTo(2));
    }

    private bool IsActive => (bool)controller.GetType().GetProperty("IsActive").GetValue(controller);

    private ScriptableObject CreateItem(string name, bool placeable)
    {
        var item = Track(ScriptableObject.CreateInstance(FindType("ItemDataSO")));
        item.name = name;
        SetField(item, "isPlaceable", placeable);
        if (placeable)
        {
            var prefab = Track(new GameObject(name + "_Prefab"));
            prefab.SetActive(false);
            SetField(item, "placementPrefab", prefab);
        }
        return item;
    }

    private T Track<T>(T value) where T : UnityEngine.Object { createdObjects.Add(value); return value; }
    private static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object GetField(object target, string name) => target.GetType().GetField(name, InstanceMembers).GetValue(target);
    private static void SetField(object target, string name, object value) => target.GetType().GetField(name, InstanceMembers).SetValue(target, value);
    private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethods(InstanceMembers)
        .Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);
}
