using System;
using UnityEngine;

[Serializable]
public sealed class DragState
{
    public bool IsDragging { get; set; }
    public float StartValue { get; set; }
    public float EndValue { get; set; }
    public Vector2 StartPosition { get; set; }
    public Vector2 LastPosition { get; set; }
    
    public float DragDistance => EndValue - StartValue;
    public float DragVelocity => (LastPosition.x - StartPosition.x) / Screen.width;

    public void Reset()
    {
        IsDragging = false;
        StartValue = 0f;
        EndValue = 0f;
        StartPosition = Vector2.zero;
        LastPosition = Vector2.zero;
    }
}