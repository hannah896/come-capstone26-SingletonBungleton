using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class MoonRankPresentationTests
{
    private static Type ViewType => FindType("UI_Panel_MoonRank");

    [TestCase(1, 5)]
    [TestCase(4, 5)]
    [TestCase(5, 5)]
    [TestCase(6, 13)]
    [TestCase(13, 13)]
    [TestCase(22, 29)]
    [TestCase(61, 61)]
    [TestCase(62, 69)]
    public void TargetDate_KeepsTodayOnFullMoon_AndOtherwisePreviewsNext(int day, int expected)
    {
        Assert.That(ViewType.GetMethod("GetTargetFullMoonDay").Invoke(null, new object[] { day }), Is.EqualTo(expected));
    }

    [Test]
    public void Display_FollowsSchedule_Rewind_AndLiveRuleChanges()
    {
        var rule = ScriptableObject.CreateInstance(FindType("BossSpawnRule"));
        var root = new GameObject("MoonRankTest", typeof(RectTransform));
        root.SetActive(false);
        try
        {
            string[] keys = { "A", "B", "C", "A", "B", "C", "A", "B" };
            float[] multipliers = { 1f, 1f, 1f, 1.5f, 1.5f, 1.5f, 2f, 2f };
            rule.GetType().GetField("bossMonsterKeys").SetValue(rule, keys);
            rule.GetType().GetField("bossStatMultipliers").SetValue(rule, multipliers);
            Component view = root.AddComponent(ViewType);
            Component graphic = root.GetComponent(FindType("UI_MoonRankGraphic"));

            Check(22, 29, 1.5f, 2, "다음 만월 보스");
            Check(29, 29, 1.5f, 2, "이번 만월 보스");
            Check(46, 53, 2f, 3, "다음 만월 보스");
            Check(61, 61, 2f, 3, "이번 만월 보스");
            Check(62, 69, 1f, 1, "다음 만월 보스");
            Check(5, 5, 1f, 1, "이번 만월 보스");

            multipliers[0] = 1.75f;
            Check(5, 5, 1.75f, 2, "이번 만월 보스");
            keys[0] = "";
            ViewType.GetMethod("SetState").Invoke(view, new object[] { rule, 5 });
            Assert.That(ViewType.GetProperty("HasBoss").GetValue(view), Is.False);
            Assert.That(graphic.GetType().GetProperty("Rank").GetValue(graphic), Is.EqualTo(0));
            ViewType.GetMethod("SetState").Invoke(view, new object[] { null, 5 });
            Assert.That(graphic.GetType().GetProperty("Rank").GetValue(graphic), Is.EqualTo(0));

            void Check(int day, int target, float multiplier, int rank, string heading)
            {
                ViewType.GetMethod("SetState").Invoke(view, new object[] { rule, day });
                Assert.That(ViewType.GetProperty("TargetFullMoonDay").GetValue(view), Is.EqualTo(target));
                Assert.That(ViewType.GetProperty("DisplayedMultiplier").GetValue(view), Is.EqualTo(multiplier));
                Assert.That(graphic.GetType().GetProperty("Rank").GetValue(graphic), Is.EqualTo(rank));
                string tooltip = (string)ViewType.GetField("_tooltipText", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                Assert.That(tooltip, Does.StartWith(heading).And.Contain($"×{multiplier:0.##}"));
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(rule);
        }
    }

    [Test]
    public void HudPrefab_ReplacesGauge_WithTextlessRaycastablePlaque_AndKeepsMoonPhase()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/UI/World/UI_Hud_WorldState.prefab");
        Transform bottom = prefab.transform.Find("WorldStateContent/Bottom");
        Assert.That(bottom.Find("UI_MoonSpiritFill"), Is.Null);
        Assert.That(bottom.Find("UI_MoonShape"), Is.Not.Null);
        Transform plaque = bottom.Find("UI_MoonRank");
        Assert.That(plaque, Is.Not.Null);
        Assert.That(plaque.childCount, Is.Zero, "원 장식이나 상시 텍스트를 추가하지 않는다.");
        Assert.That(((RectTransform)plaque).sizeDelta, Is.EqualTo(new Vector2(77f, 24f)));
        Assert.That(plaque.GetComponent(ViewType), Is.Not.Null);
        Assert.That(plaque.GetComponent<Graphic>().raycastTarget, Is.True);
        Assert.That(plaque.GetComponents<Component>(), Has.None.Null);
        Assert.That(bottom.GetComponent<HorizontalLayoutGroup>().spacing, Is.GreaterThanOrEqualTo(0f));
    }

    private static Type FindType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(name);
            if (type != null) return type;
        }
        throw new InvalidOperationException(name);
    }
}
