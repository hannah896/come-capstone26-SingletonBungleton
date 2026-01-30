using UnityEngine;

public sealed class PageCalculator
{
    #region Fields

    private float[] _pagePositions;
    private int _pageCount;
    
    public float[] PagePositions => _pagePositions;
    public int PageCount => _pageCount;

    #endregion

    public void Initialize(int pageCount)
    {
        _pageCount = Mathf.Max(1, pageCount);

        CalculatePositions();
    }

    private void CalculatePositions()
    {
        _pagePositions = new float[_pageCount];

        if (_pageCount <= 1)
        {
            _pagePositions[0] = 0f;
            return;
        }

        for (var index = 0; index < _pageCount; ++index)
        {
            _pagePositions[index] = (float)index / (_pageCount - 1);
        }
    }

    #region Public Methods

    public int GetNearestPageIndex(float scrollValue)
    {
        var nearestIndex = 0;
        var minDistance = float.MaxValue;

        for (var index = 0; index < _pageCount; ++index)
        {
            var distance = Mathf.Abs(scrollValue - _pagePositions[index]);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestIndex = index;
            }
        }

        return nearestIndex;
    }
    
    public float GetPagePosition(int pageIndex)
    {
        return pageIndex >= 0 && pageIndex < _pageCount
            ? _pagePositions[pageIndex]
            : 0f;
    }
    
    public float GetInterPageProgress(float currentValue)
    {
        for (var index = 0; index < _pageCount - 1; ++index)
        {
            var startPos = _pagePositions[index];
            var endPos = _pagePositions[index + 1];

            if (currentValue >= startPos && currentValue <= endPos)
            {
                return (currentValue - startPos) / (endPos - startPos);
            }
        }

        return currentValue <= _pagePositions[0] ? 0f : 1f;
    }

    #endregion
}