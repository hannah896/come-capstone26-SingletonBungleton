
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class NestedScrollRect : ScrollRect
{
    #region Fields

    private ScrollRect _parent;
    private bool _draggingParent;

    #endregion

    protected override void Awake()
    {
        base.Awake();
        _parent = transform.parent.GetComponentInParent<ScrollRect>();
    }

    public override void OnInitializePotentialDrag(PointerEventData eventData)
    {
        base.OnInitializePotentialDrag(eventData);
        _parent?.OnInitializePotentialDrag(eventData);
    }

    public override void OnBeginDrag(PointerEventData eventData)
    {
        if (IsPotentialParentDrag(eventData.delta))
        {
            _parent.OnBeginDrag(eventData);
            _draggingParent = true;
        }
        else
        {
            base.OnBeginDrag(eventData);
        }
    }

    public override void OnDrag(PointerEventData eventData)
    {
        if (_draggingParent)
        {
            _parent.OnDrag(eventData);
        }
        else
        {
            base.OnDrag(eventData);
        }
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        base.OnEndDrag(eventData);
        if (_parent && _draggingParent)
        {
            _draggingParent = false;
            _parent.OnEndDrag(eventData);
        }
    }

    private bool IsPotentialParentDrag(Vector2 inputDelta)
    {
        if (_parent)
        {
            return _parent.horizontal switch
            {
                true when !_parent.vertical => Mathf.Abs(inputDelta.x) > Mathf.Abs(inputDelta.y),
                false when _parent.vertical => Mathf.Abs(inputDelta.x) < Mathf.Abs(inputDelta.y),
                _ => true
            };
        }

        return false;
    }
}
