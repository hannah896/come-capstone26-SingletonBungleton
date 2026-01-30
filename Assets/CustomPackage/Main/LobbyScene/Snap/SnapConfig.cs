using System;
using UnityEngine;

[Serializable]
public sealed class SnapConfig
{
    [Range(0.1f, 2f)] public float Speed;
    [Range(0.01f, 0.1f)] public float DragThreshold;
    [Range(0.1f, 2f)] public float VelocityThreshold;
    public AnimationCurve Curve;

    public static SnapConfig Default => new SnapConfig
    {
        Speed = 0.25f,
        DragThreshold = 0.02f,
        VelocityThreshold = 0.5f,
        Curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
    };
}