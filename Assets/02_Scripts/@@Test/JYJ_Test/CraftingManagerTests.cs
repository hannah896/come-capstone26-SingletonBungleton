using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class CraftingManagerTests
{
    private static readonly Type CraftingManagerType = FindType("CraftingManager");
    private static readonly Type PlayerInventoryType = FindType("PlayerInventory");
    private static readonly Type ItemDataSOType    = FindType("ItemDataSO");
    private static readonly Type RecipeDataSOType  = FindType("RecipeDataSO");
    private static readonly Type RecipeIngredientType = FindType("RecipeIngredient");

    private readonly List<UnityEngine.Object> createdObjects = new();
    private GameObject craftingManagerObject;
    private GameObject inventoryObject;
    private Component craftingManager;
    private Component inventory;

    [SetUp]
    public void SetUp()
    {
        craftingManagerObject = new GameObject("CraftingManager_Test");
        craftingManagerObject.AddComponent(CraftingManagerType);
        craftingManager = craftingManagerObject.GetComponent(CraftingManagerType);

        inventoryObject = new GameObject("PlayerInventory_Test");
        inventory = inventoryObject.AddComponent(PlayerInventoryType);
        Invoke(inventory, "SetSlotCount", 20);

        Invoke(craftingManager, "Bind", inventory);
    }

    [TearDown]
    public void TearDown()
    {
        if (craftingManagerObject != null)
            UnityEngine.Object.DestroyImmediate(craftingManagerObject);
        if (inventoryObject != null)
            UnityEngine.Object.DestroyImmediate(inventoryObject);

        foreach (var obj in createdObjects)
            if (obj != null)
                UnityEngine.Object.DestroyImmediate(obj);

        createdObjects.Clear();
    }

    // ─── 케이스 1: 재료 충분 → 제작 성공, 재료 소모, 결과 추가 ───────────────
    [Test]
    public void Craft_WhenEnoughIngredients_ConsumesAndAddsResult()
    {
        var stone = CreateItem("Stone", isStackable: true,  maxStack: 64);
        var axe   = CreateItem("Axe",   isStackable: false, maxStack: 1);
        var recipe = CreateRecipe(axe, resultAmount: 1, (stone, 3));

        Invoke(inventory, "AddItem", stone, 5);
        Assert.That(Invoke(inventory, "GetItemCount", stone), Is.EqualTo(5), "사전조건: 돌 5개");

        bool crafted = (bool)Invoke(craftingManager, "Craft", recipe);

        Assert.That(crafted, Is.True,  "Craft() 반환값: true");
        Assert.That(Invoke(inventory, "GetItemCount", stone), Is.EqualTo(2), "돌 3개 소모 → 2개 남음");
        Assert.That(Invoke(inventory, "GetItemCount", axe),   Is.EqualTo(1), "도끼 1개 추가됨");
    }

    // ─── 케이스 2: 재료 부족 → 제작 실패, 인벤토리 변화 없음 ─────────────────
    [Test]
    public void Craft_WhenInsufficientIngredients_FailsAndInventoryUnchanged()
    {
        var stone = CreateItem("Stone", isStackable: true,  maxStack: 64);
        var axe   = CreateItem("Axe",   isStackable: false, maxStack: 1);
        var recipe = CreateRecipe(axe, resultAmount: 1, (stone, 3));

        Invoke(inventory, "AddItem", stone, 2); // 필요량 3개보다 1개 부족

        bool canCraft = (bool)Invoke(craftingManager, "CanCraft", recipe);
        bool crafted  = (bool)Invoke(craftingManager, "Craft", recipe);

        Assert.That(canCraft, Is.False, "CanCraft(): false");
        Assert.That(crafted,  Is.False, "Craft(): false");
        Assert.That(Invoke(inventory, "GetItemCount", stone), Is.EqualTo(2), "돌 개수 그대로");
        Assert.That(Invoke(inventory, "GetItemCount", axe),   Is.EqualTo(0), "도끼 추가 안 됨");
    }

    // ─── 케이스 3: 인벤토리 꽉 참 → 제작 실패, 재료 반환 ──────────────────
    [Test]
    public void Craft_WhenInventoryFull_FailsAndReturnsIngredients()
    {
        // 슬롯 1개짜리 인벤토리 → 재료(branch x2)로 꽉 채움
        // branch 1개 소모 후 torch 추가 시도 → 빈 슬롯 없음(branch x1이 있음) → 실패 → branch 반환
        Invoke(inventory, "SetSlotCount", 1);

        var branch = CreateItem("Branch", isStackable: true,  maxStack: 64);
        var torch  = CreateItem("Torch",  isStackable: false, maxStack: 1);
        var recipe = CreateRecipe(torch, resultAmount: 1, (branch, 1));

        Invoke(inventory, "AddItem", branch, 2);
        Assert.That(Invoke(inventory, "GetItemCount", branch), Is.EqualTo(2), "사전조건: 나뭇가지 2개");

        bool crafted = (bool)Invoke(craftingManager, "Craft", recipe);

        Assert.That(crafted, Is.False, "인벤토리 꽉 참 → Craft(): false");
        Assert.That(Invoke(inventory, "GetItemCount", branch), Is.EqualTo(2), "재료 반환 → 나뭇가지 2개 그대로");
        Assert.That(Invoke(inventory, "GetItemCount", torch),  Is.EqualTo(0), "횃불 추가 안 됨");
    }

    // ─── 헬퍼 ────────────────────────────────────────────────────────────────

    private ScriptableObject CreateItem(string itemName, bool isStackable, int maxStack)
    {
        var item = ScriptableObject.CreateInstance(ItemDataSOType);
        createdObjects.Add(item);
        SetField(item, "itemID",       itemName);
        SetField(item, "itemName",     itemName);
        SetField(item, "isStackable",  isStackable);
        SetField(item, "maxStack",     maxStack);
        return item;
    }

    private ScriptableObject CreateRecipe(ScriptableObject resultItem, int resultAmount,
        params (ScriptableObject item, int amount)[] ingredients)
    {
        var recipe = ScriptableObject.CreateInstance(RecipeDataSOType);
        createdObjects.Add(recipe);
        SetField(recipe, "resultItem",   resultItem);
        SetField(recipe, "resultAmount", resultAmount);

        Array arr = Array.CreateInstance(RecipeIngredientType, ingredients.Length);
        for (int i = 0; i < ingredients.Length; i++)
        {
            object ing = Activator.CreateInstance(RecipeIngredientType);
            SetStructField(ing, "itemData", ingredients[i].item);
            SetStructField(ing, "amount",   ingredients[i].amount);
            arr.SetValue(ing, i);
        }
        SetField(recipe, "ingredients", arr);

        return recipe;
    }

    private static object Invoke(object target, string methodName, params object[] args)
    {
        MethodInfo[] methods = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
        MethodInfo method = null;
        foreach (var m in methods)
        {
            if (m.Name != methodName) continue;
            if (m.GetParameters().Length != args.Length) continue;
            method = m;
            break;
        }
        Assert.That(method, Is.Not.Null, $"Method not found: {methodName}");
        return method.Invoke(target, args);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, $"Field not found: {fieldName}");
        field.SetValue(target, value);
    }

    private static void SetStructField(object boxedStruct, string fieldName, object value)
    {
        FieldInfo field = boxedStruct.GetType().GetField(fieldName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, $"Struct field not found: {fieldName}");
        field.SetValue(boxedStruct, value);
    }

    private static Type FindType(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = assembly.GetType(typeName);
            if (t != null) return t;
        }
        throw new InvalidOperationException($"Type not found: {typeName}");
    }
}
