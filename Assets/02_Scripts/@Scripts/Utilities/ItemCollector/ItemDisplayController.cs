using System;
using DG.Tweening;
using UnityEngine;

public class ItemDisplayController {

    #region Fields

    private readonly ItemDisplay _display;

    private Sequence _sequence;
    private Tween _tween;

    private bool _isMoving;

    #endregion

    #region Constructor

    public ItemDisplayController(ItemDisplay itemDisplay) => _display = itemDisplay;

    #endregion

    public void MoveTo(Vector3 startPosition, Vector3 offset, float duration, float curveHeight,
        CollectorMoveDirection direction, Action cbOnCompleted = null) {
        if (_isMoving) {
            _tween?.Kill();
            _sequence?.Kill();
        }

        _isMoving = true;

        _display.transform.position = startPosition;
        _display.transform.localScale = Vector3.zero;
        Vector3 targetPosition = startPosition + offset;
        switch (direction) {
            case CollectorMoveDirection.Top: targetPosition.y -= 0.5f; break;
            case CollectorMoveDirection.Bottom: targetPosition.y += 0.5f; break;
        }

        Appear(targetPosition, () => {
            MoveTo(curveHeight, duration, direction, () => {
                _isMoving = false;
                _display.Destroy();
                cbOnCompleted?.Invoke();
            });
        });
    }

    private void Appear(Vector3 targetPosition, Action cbOnCompleted = null) {
        const float duration = 0.3f;
        _sequence?.Kill();
        _sequence = DOTween.Sequence()
            .Append(_display.transform.DOScale(1.25f, duration).SetEase(Ease.OutBack))
            .Join(_display.transform.DOMove(targetPosition, duration).SetEase(Ease.OutQuad))
            .OnComplete(() => cbOnCompleted?.Invoke());
    }

    private void MoveTo(float curveHeight, float duration, CollectorMoveDirection direction, Action cbOnCompleted = null) {
        Vector3 startPos = _display.transform.position;
        Vector3 targetPos = ItemReceiver.GetPosition(_display.Item.Type);

        Vector3 dir = (targetPos - startPos).normalized;

        // 무조건 X축 기준으로 좌/우 커브
        Vector3 side = Vector3.up; 

        float distance = Vector3.Distance(startPos, targetPos);

        Vector3 curveOffset = direction switch {
            CollectorMoveDirection.Right  => side * curveHeight * distance * 0.5f,
            CollectorMoveDirection.Left => -side * curveHeight * distance * 0.5f,
            CollectorMoveDirection.Bottom   => Vector3.up * curveHeight * distance * 0.2f,
            CollectorMoveDirection.Top=> Vector3.down * curveHeight * distance * 0.2f,
            _ => Vector3.zero
        };

        Vector3 controlPoint = Vector3.Lerp(startPos, targetPos, 0.5f) + curveOffset;

        _sequence?.Kill();
        _sequence = DOTween.Sequence();
        _tween = _display.transform.DOPath(
                new[] { startPos, controlPoint, targetPos },
                duration, PathType.CatmullRom)
            .SetEase(Ease.InOutQuad);

        _sequence.Append(_tween)
            .Join(_display.transform.DOScale(1f, duration * 0.5f).SetEase(Ease.InOutSine))
            .OnComplete(() => cbOnCompleted?.Invoke());
    }
    
}

public abstract class ItemDisplay : Entity {

    public CollectorItem Item { get; protected set; }

    public static event Action<CollectorItemType, int> OnItemReceived;

    protected virtual void OnDisable() {
        OnItemReceived?.Invoke(Item.Type, Item.Amount);
    }

    public virtual void Set(CollectorItem item, Vector3 position) {
        Initialize();
        Item = item;
    }

    public void Destroy() {
        Main.Object.Destroy(this);
    }

}