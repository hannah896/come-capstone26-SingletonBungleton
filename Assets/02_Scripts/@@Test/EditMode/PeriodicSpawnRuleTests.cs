using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;

public class PeriodicSpawnRuleTests
{
    private ScriptableObject _rule;
    private object _director;
    private object _state;

    [SetUp]
    public void SetUp()
    {
        _rule = ScriptableObject.CreateInstance(FindType("PeriodicSpawnRule"));
        Set(_rule, "monsterKey", string.Empty);
        Type directorType = FindType("WorldMonsterSpawnDirector");

        // 빈 몬스터 키에서 중단하여 실제 생성 없이 검사 시각만 검증합니다.
        // GameObject를 사용하면 OnDestroy의 Shutdown이 Main을 초기화하므로 피합니다.
        _director = FormatterServices.GetUninitializedObject(directorType);
        FieldInfo periodicField = directorType.GetField("_periodic", BindingFlags.Instance | BindingFlags.NonPublic);
        var periodic = (IDictionary)Activator.CreateInstance(periodicField.FieldType);
        periodicField.SetValue(_director, periodic);
        _state = Activator.CreateInstance(directorType.GetNestedType("PeriodicState", BindingFlags.NonPublic), true);
        Set(_state, "Rule", _rule);
        periodic.Add("test-rule", _state);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(_rule);
        _rule = null;
        _director = null;
        _state = null;
    }

    [TestCase(3, 0, 4320f, 0f)]
    [TestCase(3, 2, 4320f, 2880f)]
    [TestCase(0, -2, 1440f, 0f)]
    public void DaySettings_ConvertToWorldClockTime_AndClampInvalidDays(
        int intervalDays, int firstDelayDays, float expectedInterval, float expectedDelay)
    {
        Set(_rule, "intervalDays", intervalDays);
        Set(_rule, "firstDelayDays", firstDelayDays);

        Assert.That(GetProperty(_rule, "IntervalInGameSeconds"), Is.EqualTo(expectedInterval));
        Assert.That(GetProperty(_rule, "FirstDelayInGameSeconds"), Is.EqualTo(expectedDelay));
    }

    [Test]
    public void Interval_ChecksAtDeadline_AndSchedulesThreeGameDaysLater()
    {
        Set(_rule, "intervalDays", 3);
        const float firstDeadline = 2880f;
        Set(_state, "NextTime", firstDeadline);

        UpdatePeriodic(firstDeadline - 1f);
        Assert.That(Get(_state, "NextTime"), Is.EqualTo(firstDeadline), "A check must not run before the game-time deadline.");

        UpdatePeriodic(firstDeadline);
        Assert.That(Get(_state, "NextTime"), Is.EqualTo(7200f), "At the deadline, the next check must be three game days later.");

        UpdatePeriodic(firstDeadline + 1f);
        Assert.That(Get(_state, "NextTime"), Is.EqualTo(7200f), "Repeated updates must not consume another interval early.");
    }

    [Test]
    public void Interval_AfterSkippingManyDays_SchedulesOneCheckFromCurrentTime()
    {
        Set(_rule, "intervalDays", 3);
        Set(_state, "NextTime", 1440f);
        const float jumpedTime = 14400f;

        UpdatePeriodic(jumpedTime);

        Assert.That(Get(_state, "NextTime"), Is.EqualTo(18720f));
        UpdatePeriodic(jumpedTime);
        Assert.That(Get(_state, "NextTime"), Is.EqualTo(18720f), "Skipped dates must not replay multiple overdue checks.");
    }

    private void UpdatePeriodic(float now) => Call(_director, "UpdatePeriodic", now);
    private static object Call(object target, string method, params object[] args)
        => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static object Get(object target, string field) => target.GetType().GetField(field).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field).SetValue(target, value);
    private static object GetProperty(object target, string property) => target.GetType().GetProperty(property).GetValue(target);
    private static Type FindType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(name);
            if (type != null) return type;
        }
        throw new InvalidOperationException($"Cannot find type: {name}");
    }
}
