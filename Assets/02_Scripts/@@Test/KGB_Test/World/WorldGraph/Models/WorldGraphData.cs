using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// WorldGraphDirector가 
/// </summary>
[System.Serializable]
public class WorldGraphData
{
    private List<Node> _nodes = new();
    public List<Node> Nodes
    {
        get => _nodes;
        set => _nodes = value;
    }

    private List<NodeConnection> _nodeConnections = new();
    public List<NodeConnection> NodeConnections
    {
        get => _nodeConnections;
        set => _nodeConnections = value;
    }

    private bool _isLooped = false;
    public bool IsLooped
    {
        get => _isLooped;
        set => _isLooped = value;
    }

    private Dictionary<Node, List<Node>> _adjacencyList = new();
    public Dictionary<Node, List<Node>> AdjacencyList
    {
        get => _adjacencyList;
        set => _adjacencyList = value;
    }

    public void Clear()
    {
        _nodes.Clear();
        _nodeConnections.Clear();
        _adjacencyList.Clear();
    }

    public int GetChildCount(Node parentNode)
    {
        return _nodeConnections.Count(conn => conn.ParentNode == parentNode);
    }

    public Node GetParentNode(Node childNode)
    {
        var connection = _nodeConnections.FirstOrDefault(conn => conn.ChildNode == childNode);
        return connection != null ? connection.ParentNode : null;
    }


    public NodeConnection CreateConnection(Node parent, Node child)
    {
        if (AreConnected(parent, child)) return null;

        NodeConnection connection = new NodeConnection(parent, child);
        _nodeConnections.Add(connection);

        _adjacencyList[parent].Add(child);
        _adjacencyList[child].Add(parent);

        // 깊이 업데이트
        if (parent.Depth >= 0 && child.Depth < 0)
        {
            child.Depth = parent.Depth + 1;
        }
        else if (child.Depth >= 0 && parent.Depth < 0)
        {
            parent.Depth = child.Depth + 1;
        }

        return connection;
    }

    public bool AreConnected(Node a, Node b)
    {
        return _adjacencyList.ContainsKey(a) && _adjacencyList[a].Contains(b);
    }

    public void RebuildAdjacency()
    {
        _adjacencyList.Clear();
        foreach (var node in _nodes)
        {
            if (!_adjacencyList.ContainsKey(node))
                _adjacencyList[node] = new List<Node>();
        }

        foreach (var conn in _nodeConnections)
        {
            // 안전장치: 혹시 Nodes에 없는 노드가 연결에 있다면 건너뜀
            if (_adjacencyList.ContainsKey(conn.ParentNode) && _adjacencyList.ContainsKey(conn.ChildNode))
            {
                _adjacencyList[conn.ParentNode].Add(conn.ChildNode);
                _adjacencyList[conn.ChildNode].Add(conn.ParentNode);
            }
        }
    }

    public void UpdateNodeIndices()
    {
        for (int i = 0; i < _nodes.Count; i++)
        {
            _nodes[i].Index = i;
        }
    }
}


[System.Serializable]
public class Node
{
    public int Index = -1;
    public int Depth = -1;

    public Vector2 Position;
    public Vector2 Velocity;
    public Vector2 Force;

    public RegionData RegionData;
    public BiomeData BiomeData // RegionData에서 BiomeData 참조
    {
        get => RegionData != null ? RegionData.BiomeData : null;
    }

    public List<POIData> AllocatedPOIDatas; 


    // 소유 타일 목록
    public List<Vector2Int> OwnedTiles = new();
}

[System.Serializable]
public class NodeConnection
{
    public Node ParentNode;
    public Node ChildNode;

    public NodeConnection(Node a, Node b)
    {
        ParentNode = a;
        ChildNode = b;
    }
}