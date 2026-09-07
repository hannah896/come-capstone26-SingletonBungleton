using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class MapViewportGeometryTests
{
    // 기본 Assembly-CSharp는 asmdef에서 직접 참조할 수 없어 기존 테스트처럼 리플렉션을 사용한다.
    private static Type GeometryType
    {
        get
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType("MapViewportGeometry");
                if (type != null) return type;
            }
            throw new InvalidOperationException("MapViewportGeometry was not compiled.");
        }
    }

    [TestCase(1024f, 512f, false)]
    [TestCase(512f, 1024f, false)]
    [TestCase(1024f, 512f, true)]
    [TestCase(512f, 1024f, true)]
    public void ContentSize_PreservesAspectAndFitsOrCoversViewport(float width, float height, bool fill)
    {
        Vector2 viewport = new Vector2(800f, 600f);
        Vector2 content = Calculate("ContentSize", viewport, new Vector2(width, height), 1f, fill);
        Assert.That(content.x / content.y, Is.EqualTo(width / height).Within(0.0001f));
        if (fill)
        {
            Assert.That(content.x, Is.GreaterThanOrEqualTo(viewport.x));
            Assert.That(content.y, Is.GreaterThanOrEqualTo(viewport.y));
        }
        else
        {
            Assert.That(content.x, Is.LessThanOrEqualTo(viewport.x));
            Assert.That(content.y, Is.LessThanOrEqualTo(viewport.y));
        }
    }

    [TestCase(1.2f)]
    [TestCase(0.5f)]
    public void Zoom_KeepsTheSameMapPointUnderCursor(float ratio)
    {
        Vector2 content = new Vector2(800f, 600f);
        Vector2 center = new Vector2(0.4f, 0.6f);
        Vector2 cursor = new Vector2(40f, -25f);
        Vector2 point = center + new Vector2(cursor.x / content.x, cursor.y / content.y);
        Vector2 nextCenter = Calculate("ZoomCenter", center, cursor, content, ratio);
        Vector2 projected = Vector2.Scale(point - nextCenter, content * ratio);
        AssertVector(projected, cursor);
    }

    [Test]
    public void Zoom_InThenOutRestoresCenter()
    {
        Vector2 original = new Vector2(0.3f, 0.7f);
        Vector2 cursor = new Vector2(-35f, 42f);
        Vector2 content = new Vector2(1000f, 800f);
        Vector2 zoomed = Calculate("ZoomCenter", original, cursor, content, 2f);
        Vector2 restored = Calculate("ZoomCenter", zoomed, cursor, content * 2f, 0.5f);
        AssertVector(restored, original);
    }

    [TestCase(-100f, -100f)]
    [TestCase(-100f, 100f)]
    [TestCase(100f, -100f)]
    [TestCase(100f, 100f)]
    public void Pan_ClampsToMapEdgesWithoutExposingEmptySpace(float x, float y)
    {
        Vector2 viewport = new Vector2(200f, 100f);
        Vector2 content = new Vector2(800f, 400f);
        Vector2 center = Calculate("ClampCenter", new Vector2(x, y), viewport, content);
        Vector2 offset = Vector2.Scale(new Vector2(0.5f, 0.5f) - center, content);
        Assert.That(offset.x - content.x / 2f, Is.LessThanOrEqualTo(-viewport.x / 2f));
        Assert.That(offset.x + content.x / 2f, Is.GreaterThanOrEqualTo(viewport.x / 2f));
        Assert.That(offset.y - content.y / 2f, Is.LessThanOrEqualTo(-viewport.y / 2f));
        Assert.That(offset.y + content.y / 2f, Is.GreaterThanOrEqualTo(viewport.y / 2f));
    }

    [Test]
    public void UnzoomedWorldMap_RemainsCenteredInLetterboxedViewport()
    {
        Vector2 viewport = new Vector2(800f, 600f);
        Vector2 content = Calculate("ContentSize", viewport, new Vector2(1024f, 1024f), 1f, false);
        AssertVector(Calculate("ClampCenter", new Vector2(-10f, 10f), viewport, content), new Vector2(0.5f, 0.5f));
    }

    [Test]
    public void ViewportResize_PreservesExploredMapCenterWhenAwayFromEdges()
    {
        Vector2 center = new Vector2(0.4f, 0.6f);
        Vector2 viewport = new Vector2(600f, 400f);
        Vector2 content = Calculate("ContentSize", viewport, new Vector2(1024f, 1024f), 4f, false);
        AssertVector(Calculate("ClampCenter", center, viewport, content), center);
    }

    private static Vector2 Calculate(string method, params object[] args) =>
        (Vector2)GeometryType.GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);

    private static void AssertVector(Vector2 actual, Vector2 expected)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
    }
}
