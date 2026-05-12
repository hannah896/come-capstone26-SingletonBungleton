using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class PlayerInventoryTests
{
    private static readonly Type PlayerInventoryType = FindType("PlayerInventory");
    private static readonly Type ItemDataType = FindType("ItemDataSO");

    private readonly List<UnityEngine.Object> createdObjects = new();
    private GameObject playerObject;
    private Component inventory;

    [SetUp]
    public void SetUp()
    {
        playerObject = new GameObject("PlayerInventory_Test");
        inventory = playerObject.AddComponent(PlayerInventoryType);
        Invoke(inventory, "SetSlotCount", 3);
    }

    [TearDown]
    public void TearDown()
    {
        if (playerObject != null)
            UnityEngine.Object.DestroyImmediate(playerObject);

        for (int i = 0; i < createdObjects.Count; i++)
            if (createdObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(createdObjects[i]);

        createdObjects.Clear();
    }

    [Test]
    public void AddItem_StacksIntoPlayerSlots()
    {
        ScriptableObject branch = CreateItem("Branch", true, 10);

        object[] args = { branch, 25, 0 };
        bool added = (bool)Invoke(inventory, "AddItem", args);

        Assert.That(added, Is.True);
        Assert.That(args[2], Is.EqualTo(0));
        Assert.That(Invoke(inventory, "GetItemCount", branch), Is.EqualTo(25));
        Assert.That(GetSlot(0), Is.EqualTo(branch));
        Assert.That(GetStack(0), Is.EqualTo(10));
        Assert.That(GetStack(1), Is.EqualTo(10));
        Assert.That(GetStack(2), Is.EqualTo(5));
    }

    [Test]
    public void AddItem_WhenPlayerInventoryFull_ReturnsRemainingAmount()
    {
        ScriptableObject branch = CreateItem("Branch", true, 10);

        object[] args = { branch, 31, 0 };
        bool added = (bool)Invoke(inventory, "AddItem", args);

        Assert.That(added, Is.False);
        Assert.That(args[2], Is.EqualTo(1));
        Assert.That(Invoke(inventory, "GetItemCount", branch), Is.EqualTo(30));
    }

    [Test]
    public void RemoveItem_WhenInsufficient_DoesNotChangePlayerSlots()
    {
        ScriptableObject stone = CreateItem("Stone", true, 10);
        Invoke(inventory, "AddItem", stone, 5);

        bool removed = (bool)Invoke(inventory, "RemoveItem", stone, 6);

        Assert.That(removed, Is.False);
        Assert.That(Invoke(inventory, "GetItemCount", stone), Is.EqualTo(5));
    }

    [Test]
    public void Toggle_ChangesPlayerInventoryOpenState()
    {
        Assert.That(GetProperty(inventory, "IsOpen"), Is.False);

        Invoke(inventory, "Toggle");

        Assert.That(GetProperty(inventory, "IsOpen"), Is.True);
    }

    private ScriptableObject CreateItem(string itemName, bool isStackable, int maxStack)
    {
        var item = ScriptableObject.CreateInstance(ItemDataType);
        createdObjects.Add(item);
        SetField(item, "itemID", itemName);
        SetField(item, "itemName", itemName);
        SetField(item, "isStackable", isStackable);
        SetField(item, "maxStack", maxStack);
        return item;
    }

    private object GetSlot(int index)
    {
        return ((IList)GetField(inventory, "slots"))[index];
    }

    private int GetStack(int index)
    {
        return (int)((IList)GetField(inventory, "stackCounts"))[index];
    }

    private static object Invoke(object target, string methodName, params object[] args)
    {
        MethodInfo method = null;
        MethodInfo[] methods = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
        for (int i = 0; i < methods.Length; i++)
        {
            if (methods[i].Name != methodName) continue;
            if (methods[i].GetParameters().Length != args.Length) continue;

            method = methods[i];
            break;
        }

        Assert.That(method, Is.Not.Null, $"Method not found: {methodName}");
        return method.Invoke(target, args);
    }

    private static object GetField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, $"Field not found: {fieldName}");
        return field.GetValue(target);
    }

    private static object GetProperty(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null, $"Property not found: {propertyName}");
        return property.GetValue(target);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, $"Field not found: {fieldName}");
        field.SetValue(target, value);
    }

    private static Type FindType(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName);
            if (type != null)
                return type;
        }

        throw new InvalidOperationException($"Type not found: {typeName}");
    }
}
