using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;

public class WorldClockDayBoundaryTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private object _clock;
    private object _timer;
    private readonly List<int> _dayEvents = new List<int>();

    [SetUp]
    public void SetUp()
    {
        // Awake/OnDestroy의 매니저 초기화 없이 실제 시계와 타이머 로직만 검증한다.
        _clock = FormatterServices.GetUninitializedObject(FindType("WorldClock"));
        _timer = Activator.CreateInstance(FindType("NyoTimer"), "test-clock", 1440f, false, true, true);
        _clock.GetType().GetField("_dayTimer", Members).SetValue(_clock, _timer);
        _timer.GetType().GetProperty("Current").SetValue(_timer, 1080f);
        ConnectTimer("OnTimeEnd", "HandleDayEnded");
        ConnectTimer("OnSecond", "CheckTimeFlow");
        Call(_clock, "CheckTimeFlow", _timer);
        _dayEvents.Clear();
        _clock.GetType().GetEvent("OnDayPassed").AddEventHandler(_clock, new Action<int>(_dayEvents.Add));
    }

    [Test]
    public void LocalTimer_KeepsDateAtMidnight_AndAdvancesOnceAtSunrise()
    {
        Call(_clock, "SyncTime", 0, 1439f);
        Call(_timer, "OnUpdate", 1f);
        AssertDate(1, 0);
        Assert.That(Get<int>("DaysPassed"), Is.EqualTo(1), "Transport date still wraps at midnight.");
        Assert.That(Get<float>("TotalInGameSeconds"), Is.EqualTo(1440f));
        Assert.That(_dayEvents, Is.Empty);

        Call(_timer, "OnUpdate", 359f);
        AssertDate(1, 0);
        Call(_timer, "OnUpdate", 1f);
        AssertDate(2, 1);
        Call(_timer, "OnUpdate", 1f);
        Assert.That(_dayEvents, Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public void SkipTime_ChangesDateAtSunrise_InsteadOfMidnight()
    {
        Call(_clock, "SyncTime", 0, 1439f);
        Call(_clock, "SkipTime", 1f);
        AssertDate(1, 0);
        Assert.That(_dayEvents, Is.Empty);
        Call(_clock, "SkipTime", 359f);
        AssertDate(1, 0);
        Call(_clock, "SkipTime", 1f);
        AssertDate(2, 1);
        Assert.That(_dayEvents, Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public void SyncTime_DetectsSunrise_WithoutTransportDateChanging()
    {
        Call(_clock, "SyncTime", 1, 359f);
        AssertDate(1, 0);
        Assert.That(_dayEvents, Is.Empty);
        Call(_clock, "SyncTime", 1, 360f);
        AssertDate(2, 1);
        Call(_clock, "SyncTime", 1, 360f);
        Assert.That(_dayEvents, Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public void SkipTime_ReportsEveryCrossedSunrise()
    {
        Call(_clock, "SkipTime", 1440f * 3f);
        AssertDate(4, 3);
        Assert.That(_dayEvents, Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(Get<float>("TotalInGameSeconds"), Is.EqualTo(1440f * 3f + 360f));
    }

    [Test]
    public void FullMoon_StartsOnFifthSunrise_AndLastsThroughFollowingNight()
    {
        Call(_clock, "SyncTime", 4, 359f);
        AssertDate(4, 3);
        _dayEvents.Clear();
        Call(_clock, "SyncTime", 4, 360f);
        AssertDate(5, 4);
        Assert.That(_dayEvents, Is.EqualTo(new[] { 4 }), "Boss date must advance on the same sunrise.");
        Call(_clock, "SyncTime", 5, 359f);
        AssertDate(5, 4);
        Assert.That(_dayEvents.Count, Is.EqualTo(1));
        Call(_clock, "SyncTime", 5, 360f);
        AssertDate(6, 5);
    }

    [TestCase(1799f, 1, 0)]
    [TestCase(1800f, 2, 1)]
    [TestCase(6119f, 4, 3)]
    [TestCase(6120f, 5, 4)]
    public void LoadTime_PreservesSavedTimestamp_AndRefreshesDateOnce(float saved, int day, int phase)
    {
        Call(_clock, "LoadTime", saved);
        AssertDate(day, phase);
        Assert.That(Call(_clock, "SaveTime"), Is.EqualTo(saved));
        Assert.That(_dayEvents, Is.EqualTo(new[] { day - 1 }));
    }

    [Test]
    public void SyncTime_RewindRefreshesDateOnce()
    {
        Call(_clock, "SyncTime", 4, 360f);
        _dayEvents.Clear();
        Call(_clock, "SyncTime", 2, 359f);
        AssertDate(2, 1);
        Call(_clock, "SyncTime", 2, 359f);
        Assert.That(_dayEvents, Is.EqualTo(new[] { 1 }));
    }

    private void AssertDate(int day, int phase)
    {
        Assert.That(Get<int>("CurrentDay"), Is.EqualTo(day));
        Assert.That(Convert.ToInt32(Get<object>("CurrentMoonPhase")), Is.EqualTo(phase));
    }

    private T Get<T>(string property) => (T)_clock.GetType().GetProperty(property).GetValue(_clock);

    private void ConnectTimer(string eventName, string method)
    {
        EventInfo timerEvent = _timer.GetType().GetEvent(eventName);
        timerEvent.AddEventHandler(_timer, Delegate.CreateDelegate(
            timerEvent.EventHandlerType, _clock, _clock.GetType().GetMethod(method, Members)));
    }

    private static object Call(object target, string method, params object[] args)
        => target.GetType().GetMethod(method, Members).Invoke(target, args);

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
