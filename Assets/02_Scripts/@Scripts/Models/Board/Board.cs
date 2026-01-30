using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 인 게임 내의 그리드에서 좌표값의 데이터
/// </summary>
public class Board {

    #region Const.

    public const float MarginTop = 0.25f;
    public const float MarginBottom = 0.25f;
    public const float MarginLeft = 0.25f;
    public const float MarginRight = 0.25f;
    
    public readonly Vector2 ArrowSize = Vector2.one;

    #endregion
    
    #region Properties
    
    public Vector2Int Size { get; }
    public Vector2Int Min { get; }
    public Vector2Int Max { get; }
    public Vector2 Center { get; }
    public BoardObject Object { get; private set; }
    
    #endregion

    #region Fields

    #endregion

    #region Indexer

    #endregion

    #region Constructor

    public Board(StageData data) {
        int minX = int.MaxValue;
        int maxX = int.MinValue;
        int minY = int.MaxValue;
        int maxY = int.MinValue;
        
        
        // #3. 크기 및 위치 설정.
        Min = new(minX, minY);
        Max = new(maxX, maxY);
        Size = new(maxX - minX + 1, maxY - minY + 1);
        Center = (Min + Max) * ArrowSize * 0.5f;
    }

    public void GenerateObject() {
        Object = new GameObject("Board").AddComponent<BoardObject>();
        Object.Set(this);
    }

    #endregion
}