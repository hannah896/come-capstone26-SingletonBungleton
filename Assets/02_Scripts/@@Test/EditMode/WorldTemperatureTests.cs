using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class WorldTemperatureTests
{
    private readonly List<UnityEngine.Object> _created = new();
    private object _logic;
    private object _graph;
    private IList _nodes;
    private int[,] _territory;
    private MethodInfo _tryGetTemperature;

    [SetUp]
    public void SetUp()
    {
        _logic = Activator.CreateInstance(FindType("WorldLogicData"), new Vector2Int(3, 2), 8);
        _graph = Activator.CreateInstance(FindType("WorldGraphData"));
        _nodes = (IList)GetProperty(_graph, "Nodes");
        _territory = (int[,])GetProperty(_logic, "TerritoryWorld");
        _nodes.Add(CreateNode(25f));
        _nodes.Add(CreateNode(-15f));
        _territory[1, 0] = 1;
        _territory[2, 1] = 1;
        _tryGetTemperature = FindType("WorldTemperature").GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "TryGetTemperature" && method.GetParameters().Length == 4);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [TestCase(0.99f, 0.99f, 25f)]
    [TestCase(1f, 0.99f, -15f)]
    [TestCase(2.99f, 1.99f, -15f)]
    public void Query_SelectsTheOwningTileRegardlessOfHeightOrLoadedChunks(float x, float z, float expected)
    {
        AssertTemperature(_logic, _graph, new Vector3(x, 1000f, z), true, expected);
        AssertTemperature(_logic, _graph, new Vector3(x, -1000f, z), true, expected);
    }

    [TestCase(-0.01f, 0f)]
    [TestCase(0f, -0.01f)]
    [TestCase(3f, 0f)]
    [TestCase(0f, 2f)]
    [TestCase(float.NaN, 0f)]
    [TestCase(0f, float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity, 0f)]
    public void Query_InvalidHorizontalPositionReturnsDefault(float x, float z)
    {
        AssertTemperature(_logic, _graph, new Vector3(x, 0f, z), false, 20f);
    }

    [TestCase(-1)]
    [TestCase(-2)]
    [TestCase(2)]
    public void Query_OceanOrInvalidRegionReturnsDefault(int region)
    {
        _territory[0, 0] = region;
        AssertTemperature(_logic, _graph, Vector3.zero, false, 20f);
    }

    [TestCase("Logic")]
    [TestCase("Graph")]
    [TestCase("Territory")]
    [TestCase("Nodes")]
    [TestCase("Node")]
    [TestCase("Region")]
    [TestCase("Biome")]
    public void Query_MissingWorldDataReturnsDefault(string missing)
    {
        object logic = _logic;
        object graph = _graph;
        switch (missing)
        {
            case "Logic": logic = null; break;
            case "Graph": graph = null; break;
            case "Territory": SetProperty(_logic, "TerritoryWorld", null); break;
            case "Nodes": SetProperty(_graph, "Nodes", null); break;
            case "Node": _nodes[0] = null; break;
            case "Region": SetField(_nodes[0], "RegionData", null); break;
            case "Biome":
                SetField(_nodes[0].GetType().GetField("RegionData").GetValue(_nodes[0]), "BiomeData", null);
                break;
        }
        AssertTemperature(logic, graph, Vector3.zero, false, 20f);
    }

    [Test]
    public void Query_UsesCurrentDataAfterRegionAndBiomeChanges()
    {
        AssertTemperature(_logic, _graph, Vector3.zero, true, 25f);
        _territory[0, 0] = 1;
        AssertTemperature(_logic, _graph, Vector3.zero, true, -15f);
        _nodes[1] = CreateNode(40f);
        AssertTemperature(_logic, _graph, Vector3.zero, true, 40f);
    }

    private object CreateNode(float temperature)
    {
        ScriptableObject biome = CreateAsset("BiomeData");
        SetField(biome, "BaseTemperature", temperature);
        ScriptableObject region = CreateAsset("RegionData");
        SetField(region, "BiomeData", biome);
        object node = Activator.CreateInstance(FindType("Node"));
        SetField(node, "RegionData", region);
        return node;
    }

    private ScriptableObject CreateAsset(string typeName)
    {
        ScriptableObject asset = ScriptableObject.CreateInstance(FindType(typeName));
        _created.Add(asset);
        return asset;
    }

    private void AssertTemperature(object logic, object graph, Vector3 position, bool success, float expected)
    {
        object[] args = { logic, graph, position, 999f };
        Assert.That(_tryGetTemperature.Invoke(null, args), Is.EqualTo(success));
        Assert.That(args[3], Is.EqualTo(expected), "조회 실패 시에도 이전 값 대신 기본 기온을 반환해야 합니다.");
    }

    private static Type FindType(string name)
        => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static object GetProperty(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static void SetProperty(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
    private static void SetField(object target, string name, object value) => target.GetType().GetField(name).SetValue(target, value);
}
