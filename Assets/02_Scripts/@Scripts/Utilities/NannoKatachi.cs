using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 죽지도 않고 돌아왔다 난노카타치4
public static class NannoKatachi {

    public static readonly Vector2Int[] Directions = new[]
        { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    //
    // #region BlockKatachi
    //
    // public static string GetKatachiKey(List<Vector2Int> indexes) {
    //     // #1. 상대 좌표로 변환.
    //     int minX = indexes.Min(i => i.x);
    //     int minY = indexes.Min(i => i.y);
    //     Vector2Int origin = new(minX, minY);
    //     HashSet<Vector2Int> relatives = indexes.Select(i => i - origin).ToHashSet();
    //     
    //     // #2. KatachiData 찾기.
    //     List<BlockKatachiData> dataList = Main.Data.GetAll<BlockKatachiData>();
    //     foreach (BlockKatachiData data in dataList) {
    //         if (data.Parts.Count != indexes.Count) continue;
    //
    //         HashSet<Vector2Int> parts = data.Parts.Select(p => new Vector2Int(p.X, p.Y)).ToHashSet();
    //
    //         if (parts.SetEquals(relatives)) return data.Key;
    //     }
    //
    //     return "";
    // }
    //
    // #endregion
    //
    // #region Gimmick Object Position
    //
    // // 캔디잼 프로젝트에서 들고 온 악마의 메서드.
    // public static Vector2 GetArrowPosition(this List<Cell> cells, Orientation orientation) {
    //     if (orientation == Orientation.Horizontal) {
    //         var rows = cells.GroupBy(c => c.Index.y).Select(g => new { Row = g.Key, Count = g.Count() }).ToList();
    //         int maxCount = rows.Max(g => g.Count);
    //         var bestRows = rows.Where(g => g.Count == maxCount).Select(g => g.Row).OrderBy(r => r).ToList();
    //
    //         float y;
    //         if (bestRows.Count > 1 && bestRows.Last() - bestRows.First() == bestRows.Count - 1)
    //             y = (bestRows.First() + bestRows.Last()) * 0.5f;
    //         else y = bestRows.Max();
    //
    //         float x = (float)cells.Where(c => bestRows.Contains(c.Index.y)).Average(c => c.Index.x);
    //         
    //         return new(x, y);
    //     }
    //     else {
    //         var columns = cells.GroupBy(c => c.Index.x).Select(g => new { Column = g.Key, Count = g.Count() }).ToList();
    //         int maxCount = columns.Max(g => g.Count);
    //         var bestColumns = columns.Where(g => g.Count == maxCount).Select(g => g.Column).OrderBy(c => c).ToList();
    //
    //         float x;
    //         if (bestColumns.Count > 1 && bestColumns.Last() - bestColumns.First() == bestColumns.Count - 1)
    //             x = (bestColumns.First() + bestColumns.Last()) * 0.5f;
    //         else x = bestColumns.Max();
    //
    //         float y = (float)cells.Where(c => bestColumns.Contains(c.Index.x)).Average(c => c.Index.y);
    //
    //         return new(x, y);
    //     }
    // }
    //
    // // // ;;;;;;;;;;;;;;;;
    // // public static void SetButterflyPosition(this SkeletonAnimation[] spines, Box box) {
    // //     string katachiKey = box.Object.KatachiKey;
    // //
    // //     switch (katachiKey) {
    // //         case "J1":
    // //             spines[0].gameObject.SetActive(true);
    // //             spines[0].transform.localPosition = new(0, -1);
    // //             SetSkin(spines[0], "Block1");
    // //             spines[1].gameObject.SetActive(true);
    // //             spines[1].transform.localPosition = new(0.5f, 0);
    // //             SetSkin(spines[1], "Block1");
    // //             break;
    // //         case "J2":
    // //             spines[0].gameObject.SetActive(true);
    // //             spines[0].transform.localPosition = new(0, -0.5f);
    // //             SetSkin(spines[0], "Block1");
    // //             spines[1].gameObject.SetActive(true);
    // //             spines[1].transform.localPosition = new(-1f, 0.5f);
    // //             SetSkin(spines[1], "Block1");
    // //             break;
    // //         case "J3":
    // //             spines[0].gameObject.SetActive(true);
    // //             spines[0].transform.localPosition = new(0, 1);
    // //             SetSkin(spines[0], "Block1");
    // //             spines[1].gameObject.SetActive(true);
    // //             spines[1].transform.localPosition = new(-0.5f, 0);
    // //             SetSkin(spines[1], "Block1");
    // //             break;
    // //         case "J4":
    // //             spines[0].gameObject.SetActive(true);
    // //             spines[0].transform.localPosition = new(0, 0.5f);
    // //             SetSkin(spines[0], "Block1");
    // //             spines[1].gameObject.SetActive(true);
    // //             spines[1].transform.localPosition = new(1f, -0.5f);
    // //             SetSkin(spines[1], "Block1");
    // //             break;
    // //         case "L1":
    // //             spines[0].gameObject.SetActive(true);
    // //             spines[0].transform.localPosition = new(0, -1);
    // //             SetSkin(spines[0], "Block1");
    // //             spines[1].gameObject.SetActive(true);
    // //             spines[1].transform.localPosition = new(-0.5f, 0);
    // //             SetSkin(spines[1], "Block1");
    // //             break;
    // //         case "L2":
    // //             spines[0].gameObject.SetActive(true);
    // //             spines[0].transform.localPosition = new(0, 0.5f);
    // //             SetSkin(spines[0], "Block1");
    // //             spines[1].gameObject.SetActive(true);
    // //             spines[1].transform.localPosition = new(-1f, -0.5f);
    // //             SetSkin(spines[1], "Block1");
    // //             break;
    // //         case "L3":
    // //             spines[0].gameObject.SetActive(true);
    // //             spines[0].transform.localPosition = new(0, 1);
    // //             SetSkin(spines[0], "Block1");
    // //             spines[1].gameObject.SetActive(true);
    // //             spines[1].transform.localPosition = new(0.5f, 0);
    // //             SetSkin(spines[1], "Block1");
    // //             break;
    // //         case "L4":
    // //             spines[0].gameObject.SetActive(true);
    // //             spines[0].transform.localPosition = new(0, -0.5f);
    // //             SetSkin(spines[0], "Block1");
    // //             spines[1].gameObject.SetActive(true);
    // //             spines[1].transform.localPosition = new(1f, 0.5f);
    // //             SetSkin(spines[1], "Block1");
    // //             break;
    // //         case "N2":
    // //             spines[0].gameObject.SetActive(true);
    // //             spines[0].transform.localPosition = new(0, 0);
    // //             SetSkin(spines[0], "Block1");
    // //             spines[1].gameObject.SetActive(false);
    // //             break;
    // //         case "N3":
    // //             spines[0].gameObject.SetActive(true);
    // //             spines[0].transform.localPosition = new(0, 0);
    // //             SetSkin(spines[0], "Block1");
    // //             spines[1].gameObject.SetActive(false);
    // //             break;
    // //         case "O1":
    // //             spines[0].gameObject.SetActive(true);
    // //             spines[0].transform.localPosition = new(0, 0);
    // //             SetSkin(spines[0], "Block1");
    // //             spines[1].gameObject.SetActive(false);
    // //             break;
    // //         case "O2":
    // //             spines[0].gameObject.SetActive(true);
    // //             spines[0].transform.localPosition = new(0, 0);
    // //             SetSkin(spines[0], "Block1");
    // //             spines[1].gameObject.SetActive(false);
    // //             break;
    // //         case "P":
    // //             spines[0].gameObject.SetActive(true);
    // //             spines[0].transform.localPosition = new(0, 0);
    // //             SetSkin(spines[0], "Block1");
    // //             spines[1].gameObject.SetActive(false);
    // //             break;
    // //         case "r1":
    // //             spines[0].gameObject.SetActive(true);
    // //             spines[0].transform.localPosition = new(0, -0.5f);
    // //             SetSkin(spines[0], "Block1");
    // //             spines[1].gameObject.SetActive(false);
    // //             break;
    // //         case "r2":
    // //             spines[0].gameObject.SetActive(true);
    // //             spines[0].transform.localPosition = new(0, 0.5f);
    // //             SetSkin(spines[0], "Block1");
    // //             spines[1].gameObject.SetActive(false);
    // //             break;
    // //         case "r3":
    // //             spines[0].gameObject.SetActive(true);
    // //             spines[0].transform.localPosition = new(0, 0.5f);
    // //             SetSkin(spines[0], "Block1");
    // //             spines[1].gameObject.SetActive(false);
    // //             break;
    // //         case "r4":
    // //             spines[0].gameObject.SetActive(true);
    // //             spines[0].transform.localPosition = new(0, -0.5f);
    // //             SetSkin(spines[0], "Block1");
    // //             spines[1].gameObject.SetActive(false);
    // //             break;
    // //         case "T1":
    // //             spines[0].gameObject.SetActive(true);
    // //             spines[0].transform.localPosition = new(0, 0.5f);
    // //             SetSkin(spines[0], "Block1");
    // //             spines[1].gameObject.SetActive(false);
    // //             break;
    // //         case "T2":
    // //             spines[0].gameObject.SetActive(true);
    // //             spines[0].transform.localPosition = new(0.5f, 0);
    // //             SetSkin(spines[0], "Block1");
    // //             spines[1].gameObject.SetActive(false);
    // //             break;
    // //         case "T3":
    // //             spines[0].gameObject.SetActive(true);
    // //             spines[0].transform.localPosition = new(0, -0.5f);
    // //             SetSkin(spines[0], "Block1");
    // //             spines[1].gameObject.SetActive(false);
    // //             break;
    // //         case "T4":
    // //             spines[0].gameObject.SetActive(true);
    // //             spines[0].transform.localPosition = new(-0.5f, 0);
    // //             SetSkin(spines[0], "Block1");
    // //             spines[1].gameObject.SetActive(false);
    // //             break;
    // //         case "Z2":
    // //             spines[0].gameObject.SetActive(true);
    // //             spines[0].transform.localPosition = new(0, 0);
    // //             SetSkin(spines[0], "Block1");
    // //             spines[1].gameObject.SetActive(false);
    // //             break;
    // //         case "Z3":
    // //             spines[0].gameObject.SetActive(true);
    // //             spines[0].transform.localPosition = new(0, 0);
    // //             SetSkin(spines[0], "Block1");
    // //             spines[1].gameObject.SetActive(false);
    // //             break;
    // //         default:
    // //             spines[0].gameObject.SetActive(true);
    // //             spines[0].transform.localPosition = new(0, 0);
    // //             SetSkin(spines[0], "Block1");
    // //             spines[1].gameObject.SetActive(false);
    // //             break;
    // //     }
    // // }
    // //
    // // public static Vector2 GetObjectPosition(this List<Cell> cells) {
    // //     if (cells == null || cells.Count == 0) return Vector2.zero;
    // //     
    // //     // #1. Cell이 하나인 경우: 해당 Cell 위치 그대로 반환.
    // //     if (cells.Count == 1) return cells[0].Position;
    // //
    // //     HashSet<Vector2Int> indexes = cells.Select(c => c.Index).ToHashSet();
    // //     int minX = cells.Min(c => c.X), maxX = cells.Max(c => c.X);
    // //     int minY = cells.Min(c => c.Y), maxY = cells.Max(c => c.Y);
    // //     int width = maxX - minX + 1, height = maxY - minY + 1;
    // //     // #2. 정사각형 모양인 경우 중앙 반환.
    // //     if (width * height == cells.Count) return cells.GetCenter();
    // //     
    // //     // #3. 가로, 세로가 같은 '+' 모양인 경우: Cells의 정중앙 위치 반환.
    // //     if (width == height) {
    // //         foreach (Vector2Int index in indexes) {
    // //             if (indexes.Contains(index + Vector2Int.up) && indexes.Contains(index + Vector2Int.left) &&
    // //                 indexes.Contains(index + Vector2Int.down) && indexes.Contains(index + Vector2Int.right))
    // //                 return cells.GetCenter();
    // //         }
    // //     }
    // //     
    // //     // #4. 직선 모양인 경우: 정중앙 위치 반환.
    // //     if (width == 1 && cells.Select(c => c.Y).ToList().IsRenzoku()) return cells.GetCenter();
    // //     if (height == 1 && cells.Select(c=>c.X).ToList().IsRenzoku()) return cells.GetCenter();
    // //     
    // //     // #5. 무게 중김에 가장 가까운 위치 반환.
    // //     var averageX = cells.Average(c => c.X);
    // //     var averageY = cells.Average(c => c.Y);
    // //     return cells.OrderBy(c => (c.X - averageX) * (c.X - averageX) + (c.Y - averageY) * (c.Y - averageY))
    // //         .FirstOrDefault()?.Position ?? cells.GetCenter();
    // // }
    // //
    // // private static void SetSkin(SkeletonAnimation spine, string skinKey) {
    // //     spine.Skeleton.SetSkin($"{skinKey}");
    // //     spine.Skeleton.SetToSetupPose();
    // //     spine.AnimationState.Apply(spine.Skeleton);
    // // }
    // //
    // #endregion
    //
    // #region View Model
    //
    // public static Dictionary<Cell, BlockViewData> GetViewData(this List<Cell> cells) {
    //     Dictionary<Cell, BlockViewData> dataList = new();
    //     
    //     List<Vector2Int> relativeIndexes = GetRelativeIndexes(cells);
    //
    //     foreach (Cell cell in cells) {
    //         Vector2Int relativeIndex = cell.Index - cells[0].Index;
    //         byte directionInfo = GetDirectionInfo(relativeIndex, relativeIndexes);
    //         
    //         dataList[cell] = GetViewData(directionInfo);
    //     }
    //
    //     return dataList;
    // }
    //
    // private static List<Vector2Int> GetRelativeIndexes(List<Cell> cells) {
    //     List<Vector2Int> indexes = new();
    //     Cell origin = cells[0];
    //     foreach (Cell cell in cells) indexes.Add(cell.Index - origin.Index);
    //     return indexes;
    // }
    //
    // private static byte GetDirectionInfo(Vector2Int index, List<Vector2Int> indexes) {
    //     Vector2Int[] directions =
    //         { new(1, 0), new(1, -1), new(0, -1), new(-1, -1), new(-1, 0), new(-1, 1), new(0, 1), new(1, 1) };
    //     byte directionInfo = 0;
    //     
    //     // #1. 정방향 검사. 
    //     for (int i = 0; i < 4; i++) {
    //         Vector2Int targetIndex = index + directions[i * 2];
    //         if (indexes.Contains(targetIndex)) directionInfo |= (byte)(1 << (i * 2));
    //     }
    //
    //     // #2. 대각선 검사. 
    //     for (int i = 0; i < 4; i++) {
    //         byte comp = 5;
    //         comp = comp.RotateShift(i * 2);
    //         if ((directionInfo & comp) != comp) continue;
    //         Vector2Int targetIndex = index + directions[i * 2 + 1];
    //         if (indexes.Contains(targetIndex)) directionInfo |= (byte)(1 << (i * 2 + 1));
    //     }
    //
    //     return directionInfo;
    // }
    //
    // private static BlockViewData GetViewData(byte directionInfo) {
    //     byte[] modelComps = new byte[] {
    //         0b_0000_0000, // 0
    //         0b_0000_0001, // 1
    //         0b_0001_0001, // 2
    //         0b_0000_0111, // 3
    //         0b_0000_0101, // 4
    //         0b_0001_1111, // 5
    //         0b_0001_1101, // 6
    //         0b_0001_0111, // 7
    //         0b_0001_0101, // 8
    //         0b_1111_1111, // 9
    //         0b_1111_1101, // 10
    //         0b_0111_1101, // 11
    //         0b_1101_1101, // 12
    //         0b_1101_0101, // 13
    //         0b_0101_0101, // 14
    //     };
    //     int modelIndex = 0;
    //     int rotation = -1;
    //     for (int i = 0; i < modelComps.Length; i++) {
    //         rotation = GetViewRotation(modelComps[i], directionInfo);
    //         if (rotation != -1) {
    //             modelIndex = i;
    //             break;
    //         }
    //     }
    //
    //     if (rotation == -1) {
    //         modelIndex = 0;
    //         rotation = 0;
    //     }
    //
    //     return new(modelIndex, rotation);
    // }
    //
    // private static int GetViewRotation(byte target, byte directionInfo) {
    //     for (int i = 0; i < 4; i++) if (target.RotateShift(i * 2) == directionInfo) return i;
    //     return -1;
    // }
    //
    // #endregion
    //
    // #region Bounds
    //
    // public static IndexBounds GetTargetBounds(this Block block) {
    //     IReadOnlyList<Cell> cells = block?.TargetCells;
    //     if (cells == null || cells.Count == 0) return new(0, 0, 0, 0);
    //
    //     return new(
    //         cells.Min(c => c.Index.x),
    //         cells.Max(c => c.Index.x),
    //         cells.Min(c => c.Index.y),
    //         cells.Max(c => c.Index.y));
    // }
    //
    // public static IndexBounds GetBounds(this Wall wall) {
    //     IReadOnlyList<Cell> cells = wall?.Cells;
    //     if (cells == null || cells.Count == 0) return new(0, 0, 0, 0);
    //     
    //     return new(
    //         cells.Min(c => c.Index.x),
    //         cells.Max(c => c.Index.x),
    //         cells.Min(c => c.Index.y),
    //         cells.Max(c => c.Index.y));
    // }
    //
    // #endregion
    //
    // #region Math
    //
    // private static bool IsRenzoku(this List<int> values) {
    //     if (values.Count <= 1) return true;
    //     List<int> list = values.OrderBy(v => v).ToList();
    //     for (int i = 1; i < list.Count; i++) {
    //         if (list[i] != list[i - 1] + 1) return false;
    //     }
    //
    //     return true;
    // }
    //
    // public static List<Cell> GetMostRenzokuGroup(this List<Cell> cells, Orientation orientation, int index = -1) {
    //     if (cells == null || cells.Count == 0) {
    //         Debug.LogWarning($"[NannoKatachi] GetMostRenzokuGroup(): Cells is null or empty!");
    //         return new();
    //     }
    //
    //     List<List<Cell>> allGroups = new();
    //     Func<Cell, int> keySelector = orientation == Orientation.Horizontal ? c => c.Y : c => c.X;
    //
    //     if (index != -1)
    //         cells = cells.Where(c => orientation == Orientation.Horizontal ? c.Y == index : c.X == index).ToList();
    //
    //     var lines = cells.GroupBy(keySelector).OrderBy(g => g.Key);
    //     foreach (var line in lines) allGroups.AddRange(GetRenzokuGroups(line.ToList(), orientation));
    //
    //     return allGroups.OrderByDescending(g => g.Count).FirstOrDefault() ?? new();
    // }
    //
    // private static List<List<Cell>> GetRenzokuGroups(List<Cell> cells, Orientation orientation) {
    //     List<List<Cell>> allGroups = new();
    //     if (cells == null || cells.Count == 0) return allGroups;
    //     Func<Cell, int> keySelector = orientation == Orientation.Horizontal ? c => c.X : c => c.Y;
    //     
    //     // #1. 셀 정렬.
    //     cells = cells.OrderBy(keySelector).ToList();
    //     
    //     // #2. 연속되는 값끼리 묶어 서브 그룹 생성.
    //     List<Cell> currentGroup = new();
    //     int prevValue = int.MinValue;
    //     foreach (Cell cell in cells) {
    //         if (currentGroup.Count == 0
    //             || orientation == Orientation.Horizontal && cell.X == prevValue + 1
    //             || orientation == Orientation.Vertical && cell.Y == prevValue + 1) {
    //             currentGroup.Add(cell);
    //         }
    //         else {
    //             allGroups.Add(new(currentGroup));
    //             currentGroup.Clear();
    //             currentGroup.Add(cell);
    //         }
    //         prevValue = orientation == Orientation.Horizontal ? cell.X : cell.Y;
    //     }
    //     
    //     // #3. 마지막 그룹 추가.
    //     if (currentGroup.Count > 0) allGroups.Add(currentGroup);
    //
    //     return allGroups;
    // }
    //
    // #endregion
    //
    // public static Direction GetDirection(this Cell from, Cell to) {
    //     if (from == null || to == null || from == to) return Direction.None;
    //
    //     Vector2Int fromIndex = from.Index;
    //     Vector2Int toIndex = to.Index;
    //     int dx = toIndex.x - fromIndex.x;
    //     int dy = toIndex.y - fromIndex.y;
    //     if (dx == dy) return Direction.None;
    //
    //     if (dx == 0) return dy > 0 ? Direction.Top : Direction.Bottom;
    //     if (dy == 0) return dx > 0 ? Direction.Right : Direction.Left;
    //     return Direction.None;
    // }
    //
    // public static Vector3 GetCenter(this List<Cell> cells) {
    //     float minX = float.MaxValue;
    //     float maxX = float.MinValue;
    //     float minY = float.MaxValue;
    //     float maxY = float.MinValue;
    //     foreach (Cell cell in cells) {
    //         float x = cell.Position.x;
    //         float y = cell.Position.y;
    //         if (x < minX) minX = x;
    //         if (x > maxX) maxX = x;
    //         if (y < minY) minY = y;
    //         if (y > maxY) maxY = y;
    //     }
    //
    //     return new((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
    // }
    //
    // public static bool IsAdjacentTo(this Block blockA, Block blockB) {
    //     return blockA.GetCells().Any(cellA =>
    //         blockB.GetCells().Any(cellB =>
    //             Mathf.Abs(cellA.Index.x - cellB.Index.x) + Mathf.Abs(cellA.Index.y - cellB.Index.y) == 1));
    // }
    
}

public struct BlockViewData {
    public int ViewIndex;
    public int Rotation;

    public BlockViewData(int viewIndex, int rotation) {
        ViewIndex = viewIndex;
        Rotation = rotation;
    }
}

public struct IndexBounds {
    public int XMin { get; }
    public int XMax { get; }
    public int YMin { get; }
    public int YMax { get; }

    public IndexBounds(int xMin, int xMax, int yMin, int yMax) {
        XMin = xMin;
        XMax = xMax;
        YMin = yMin;
        YMax = yMax;
    }

    public bool OverlapsX(IndexBounds other) => Overlaps1D(XMin, XMax, other.XMin, other.XMax);
    public bool OverlapsY(IndexBounds other) => Overlaps1D(YMin, YMax, other.YMin, other.YMax);

    private bool Overlaps1D(int thisMin, int thisMax, int otherMin, int otherMax)
        => thisMin <= otherMax && otherMin <= thisMax;
}

