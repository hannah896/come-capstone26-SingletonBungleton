using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2D 공간을 격자로 나누어 데이터를 관리하는 범용 클래스
/// (맵 생성, 충돌 감지, 시야 처리 등에 재사용 가능)
/// </summary>
public class SpatialGrid<T>
{
    private int _width;
    private int _height;
    private int _cellSize;
    private int _gridW, _gridH;

    // 실제 데이터를 담을 버킷 
    private List<T>[] _buckets;

    public SpatialGrid(int worldWidth, int worldHeight, int cellSize)
    {
        _width = worldWidth;
        _height = worldHeight;
        _cellSize = cellSize;

        _gridW = Mathf.CeilToInt(_width / (float)_cellSize);
        _gridH = Mathf.CeilToInt(_height / (float)_cellSize);

        _buckets = new List<T>[_gridW * _gridH];
        for (int i = 0; i < _buckets.Length; i++)
        {
            _buckets[i] = new List<T>();
        }
    }

    /// <summary>
    /// 데이터를 특정 좌표에 등록 (1칸에만)
    /// </summary>
    public void Add(T item, Vector2 position)
    {
        int index = GetGridIndex(position);
        if (index != -1)
        {
            _buckets[index].Add(item);
        }
    }

    /// <summary>
    /// [핵심] 데이터를 좌표 주변 9칸(3x3)에 모두 등록 (경계선 문제 해결용)
    /// - 읽기 성능을 위해 쓰기 공간을 희생하는 전략
    /// </summary>
    public void AddToNeighbors(T item, Vector2 position)
    {
        int gx = Mathf.FloorToInt(position.x / _cellSize);
        int gy = Mathf.FloorToInt(position.y / _cellSize);

        // 중복 추가 방지를 위한 로컬 체크가 필요하다면 HashSet 사용 고려
        // 여기서는 리스트의 Contains를 쓰거나, 상위에서 관리

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int nx = gx + dx;
                int ny = gy + dy;

                // 맵 범위 체크
                if (nx >= 0 && nx < _gridW && ny >= 0 && ny < _gridH)
                {
                    int index = ny * _gridW + nx;
                    // 중복 방지 (성능을 위해 필요시 제거 가능)
                    if (!_buckets[index].Contains(item))
                    {
                        _buckets[index].Add(item);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 해당 좌표가 속한 셀의 데이터 목록을 반환
    /// </summary>
    public List<T> GetItemsAt(Vector2 position)
    {
        int index = GetGridIndex(position);
        if (index != -1) return _buckets[index];
        return null;
    }

    /// <summary>
    /// 해당 좌표가 속한 셀의 데이터 목록 반환 (좌표 x,y 버전)
    /// </summary>
    public List<T> GetItemsAt(int x, int y)
    {
        int gx = Mathf.FloorToInt(x / _cellSize);
        int gy = Mathf.FloorToInt(y / _cellSize);

        if (gx < 0 || gx >= _gridW || gy < 0 || gy >= _gridH) return null;

        return _buckets[gy * _gridW + gx];
    }

    // 내부 인덱스 계산
    private int GetGridIndex(Vector2 pos)
    {
        int gx = Mathf.FloorToInt(pos.x / _cellSize);
        int gy = Mathf.FloorToInt(pos.y / _cellSize);

        if (gx < 0 || gx >= _gridW || gy < 0 || gy >= _gridH) return -1;

        return gy * _gridW + gx;
    }

    public void Clear()
    {
        for (int i = 0; i < _buckets.Length; i++) _buckets[i].Clear();
    }
}