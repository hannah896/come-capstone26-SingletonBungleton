//using Cysharp.Threading.Tasks;
//using System;
//using System.Collections.Generic;
//using System.Threading;
//using UnityEngine;

//public readonly struct ObjPatternContext
//{
//    public System.Random Random { get; }
//    public CancellationToken CancellationToken { get; }

//    public ObjPatternContext(System.Random random, CancellationToken cancellationToken)
//    {
//        Random = random;
//        CancellationToken = cancellationToken;
//    }
//}

//public interface IDisposePatternStrategy
//{
//    UniTask<List<Vector2Int>> TakePositionsAsync(
//        ObjPatternContext context,
//        List<Vector2Int> remainingPositions,
//        Vector2Int center,
//        PlacementRule rule,
//        int extraCount);
//}

//internal static class DisposePatternUtils
//{
//    public static Vector2Int TakeRandomPosition(System.Random random, List<Vector2Int> positions)
//    {
//        int index = random.Next(0, positions.Count);
//        Vector2Int pos = positions[index];
//        positions[index] = positions[positions.Count - 1];
//        positions.RemoveAt(positions.Count - 1);
//        return pos;
//    }

//    public static bool TryTakeNearestPosition(
//        List<Vector2Int> remainingPositions,
//        Vector2Int target,
//        int maxDistance,
//        out Vector2Int taken)
//    {
//        int maxSqrDistance = maxDistance * maxDistance;
//        int bestIndex = -1;
//        int bestSqr = int.MaxValue;

//        for (int i = 0; i < remainingPositions.Count; i++)
//        {
//            Vector2Int pos = remainingPositions[i];
//            int dx = pos.x - target.x;
//            int dy = pos.y - target.y;
//            int sqr = (dx * dx) + (dy * dy);

//            if (sqr <= maxSqrDistance && sqr < bestSqr)
//            {
//                bestSqr = sqr;
//                bestIndex = i;
//            }
//        }

//        if (bestIndex >= 0)
//        {
//            taken = remainingPositions[bestIndex];
//            remainingPositions[bestIndex] = remainingPositions[remainingPositions.Count - 1];
//            remainingPositions.RemoveAt(remainingPositions.Count - 1);
//            return true;
//        }

//        taken = default;
//        return false;
//    }

//    public static Vector2 EvaluateQuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float t)
//    {
//        float invT = 1f - t;
//        return (invT * invT * start) + (2f * invT * t * control) + (t * t * end);
//    }

//    public static Vector2 GetRandomDirection2D(System.Random random)
//    {
//        float angle = (float)random.NextDouble() * Mathf.PI * 2f;
//        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
//    }

//    public static UniTask<List<Vector2Int>> TakeClusterPositions(
//        System.Random random,
//        List<Vector2Int> remainingPositions,
//        Vector2Int center,
//        float radius,
//        int maxCount)
//    {
//        List<Vector2Int> result = new List<Vector2Int>();
//        if (maxCount <= 0 || radius <= 0f) return UniTask.FromResult(result);

//        float sqrRadius = radius * radius;
//        List<Vector2Int> candidates = new List<Vector2Int>();

//        for (int i = 0; i < remainingPositions.Count; i++)
//        {
//            Vector2Int pos = remainingPositions[i];
//            float dx = pos.x - center.x;
//            float dy = pos.y - center.y;
//            if ((dx * dx) + (dy * dy) <= sqrRadius)
//            {
//                candidates.Add(pos);
//            }
//        }

//        if (candidates.Count == 0) return UniTask.FromResult(result);

//        int takeCount = Mathf.Min(maxCount, candidates.Count);
//        for (int i = 0; i < takeCount; i++)
//        {
//            int index = random.Next(0, candidates.Count);
//            Vector2Int pos = candidates[index];
//            candidates[index] = candidates[candidates.Count - 1];
//            candidates.RemoveAt(candidates.Count - 1);

//            if (remainingPositions.Remove(pos))
//            {
//                result.Add(pos);
//            }
//        }

//        return UniTask.FromResult(result);
//    }
//}

//sealed class DefaultPatternStrategy : IDisposePatternStrategy
//{
//    public UniTask<List<Vector2Int>> TakePositionsAsync(
//        ObjPatternContext context,
//        List<Vector2Int> remainingPositions,
//        Vector2Int center,
//        PlacementRule rule,
//        int extraCount)
//    {
//        List<Vector2Int> result = new List<Vector2Int>();

//        for (int i = 0; i < extraCount && remainingPositions.Count > 0; i++)
//        {
//            result.Add(DisposePatternUtils.TakeRandomPosition(context.Random, remainingPositions));
//        }

//        return UniTask.FromResult(result);
//    }
//}


////sealed class LinePatternStrategy : IDisposePatternStrategy
////{
////    public UniTask<List<Vector2Int>> TakePositionsAsync(
////        ObjPatternContext context,
////        List<Vector2Int> remainingPositions,
////        Vector2Int center,
////        ObjectRule rule,
////        int extraCount)
////    {
////        List<Vector2Int> result = new List<Vector2Int>();
////        if (extraCount <= 0) return UniTask.FromResult(result);

////        float angle = (float)context.Random.NextDouble() * Mathf.PI * 2f;
////        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
////        float step = Mathf.Max(1f, rule.patternLength / Mathf.Max(1, extraCount));
////        int searchRadius = Mathf.CeilToInt(rule.clusterRadius);

////        for (int i = 1; i <= extraCount; i++)
////        {
////            Vector2 pos = new Vector2(center.x, center.y) + (direction * step * i);
////            Vector2Int target = new Vector2Int(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y));

////            if (DisposePatternUtils.TryTakeNearestPosition(remainingPositions, target, searchRadius, out Vector2Int taken))
////            {
////                result.Add(taken);
////            }
////        }

////        return UniTask.FromResult(result);
////    }
////}

////sealed class CurvePatternStrategy : IDisposePatternStrategy
////{
////    public UniTask<List<Vector2Int>> TakePositionsAsync(
////        ObjPatternContext context,
////        List<Vector2Int> remainingPositions,
////        Vector2Int center,
////        ObjectRule rule,
////        int extraCount)
////    {
////        List<Vector2Int> result = new List<Vector2Int>();
////        if (extraCount <= 0) return UniTask.FromResult(result);

////        float angle = (float)context.Random.NextDouble() * Mathf.PI * 2f;
////        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
////        float length = Mathf.Max(1f, rule.patternLength);

////        Vector2 start = new Vector2(center.x, center.y);
////        Vector2 end = start + (direction * length);

////        float curveRadians = rule.curveAngle * Mathf.Deg2Rad;
////        Vector2 controlDirection = new Vector2(
////            (direction.x * Mathf.Cos(curveRadians)) - (direction.y * Mathf.Sin(curveRadians)),
////            (direction.x * Mathf.Sin(curveRadians)) + (direction.y * Mathf.Cos(curveRadians))
////        );
////        Vector2 control = start + (controlDirection * (length * 0.5f));

////        int searchRadius = Mathf.CeilToInt(rule.clusterRadius);

////        for (int i = 1; i <= extraCount; i++)
////        {
////            float t = (float)i / (extraCount + 1);
////            Vector2 pos = DisposePatternUtils.EvaluateQuadraticBezier(start, control, end, t);
////            Vector2Int target = new Vector2Int(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y));

////            if (DisposePatternUtils.TryTakeNearestPosition(remainingPositions, target, searchRadius, out Vector2Int taken))
////            {
////                result.Add(taken);
////            }
////        }

////        return UniTask.FromResult(result);
////    }
////}

////sealed class BranchingPatternStrategy : IDisposePatternStrategy
////{
////    public async UniTask<List<Vector2Int>> TakePositionsAsync(
////        ObjPatternContext context,
////        List<Vector2Int> remainingPositions,
////        Vector2Int center,
////        ObjectRule rule,
////        int extraCount)
////    {
////        List<Vector2Int> result = new List<Vector2Int>();
////        if (extraCount <= 0) return result;

////        List<Vector2Int> frontier = new List<Vector2Int> { center };
////        float step = Mathf.Max(1f, rule.patternLength / Mathf.Max(1, extraCount));
////        int maxAttempts = extraCount * 6;
////        int searchRadius = Mathf.CeilToInt(rule.clusterRadius);

////        while (result.Count < extraCount && frontier.Count > 0 && maxAttempts > 0)
////        {
////            context.CancellationToken.ThrowIfCancellationRequested();

////            int index = context.Random.Next(0, frontier.Count);
////            Vector2Int basePos = frontier[index];

////            Vector2 direction = DisposePatternUtils.GetRandomDirection2D(context.Random);
////            Vector2 next = new Vector2(basePos.x, basePos.y) + (direction * step);
////            Vector2Int target = new Vector2Int(Mathf.RoundToInt(next.x), Mathf.RoundToInt(next.y));

////            if (DisposePatternUtils.TryTakeNearestPosition(remainingPositions, target, searchRadius, out Vector2Int taken))
////            {
////                result.Add(taken);
////                frontier.Add(taken);
////            }
////            else
////            {
////                frontier.RemoveAt(index);
////            }

////            maxAttempts--;

////            if ((maxAttempts % 8) == 0)
////                await UniTask.Yield(context.CancellationToken);
////        }

////        return result;
////    }
////}