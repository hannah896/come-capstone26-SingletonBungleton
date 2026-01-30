using System;
using UnityEngine;

[Serializable]
public class ArrowData
{
    public Vector2Int index;
    // Start 부분에서 End로 이동. 즉, 최종 End가 화살표 끝부분.
    public Direction startDirection = Direction.None;
    public Direction endDirection = Direction.None;
    public ColorType color = ColorType.Black;
}