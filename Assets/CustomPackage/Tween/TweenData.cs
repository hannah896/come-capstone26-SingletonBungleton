using System;
using DG.Tweening;
using UnityEngine;

public class TweenData
{
    [SerializeField] private TweenSO _tweenSo;
    [SerializeField] private bool _isJoin;
    [SerializeField] private Ease _ease = Ease.Linear;
}

[Serializable]
public class TweenDatas
{
    public Transform SelectTr;
    public Ease Ease = Ease.Linear;
    public float EndValue = 0.9f;
    public float Duration = 0.9f;
    public bool IsRelative = false;
    public bool IsSpeedBased = false;
}