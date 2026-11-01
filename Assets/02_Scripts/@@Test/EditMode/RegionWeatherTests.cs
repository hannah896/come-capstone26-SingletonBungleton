using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class RegionWeatherTests
{
    private const double DaySeconds = 1440d;

    [Test]
    public void FixedPeriods_ChargeToCapacityThenDrainWithoutChargingDuringRain()
    {
        object state = CreateState(CreateSettings("Rain", 2f, 2f, 1f, 1f), startSeconds: 100d, secondsPerDay: 10d);
        AssertState(state, false, 0d);
        Assert.That(Property<double>(state, "PhaseDurationSeconds"), Is.EqualTo(20d));

        Assert.That(Call(state, "AdvanceTo", 110d), Is.False);
        AssertState(state, false, 50d);
        Assert.That(Call(state, "AdvanceTo", 120d), Is.True);
        AssertState(state, true, 100d);
        Assert.That(Property<double>(state, "PhaseStartSeconds"), Is.EqualTo(120d));
        Assert.That(Property<double>(state, "PhaseDurationSeconds"), Is.EqualTo(10d));
        Assert.That(Call(state, "AdvanceTo", 125d), Is.False);
        AssertState(state, true, 50d);
        Assert.That(Call(state, "AdvanceTo", 130d), Is.True);
        AssertState(state, false, 0d);
        Assert.That(Property<double>(state, "PhaseDurationSeconds"), Is.EqualTo(20d));
    }

    [TestCase("Rain", 1f, 1.5f, 1.5f, 3.5f)]
    [TestCase("Rain", 10f, 15f, 0.5f, 1f)]
    [TestCase("Snow", 1.5f, 2.5f, 0.5f, 1f)]
    public void RandomPeriods_RemainInsideConfiguredRangesAndChooseAgainForEachPhase(
        string kind, float minDry, float maxDry, float minWet, float maxWet)
    {
        object state = CreateState(CreateSettings(kind, minDry, maxDry, minWet, maxWet), seed: 315387u);
        var dryDurations = new HashSet<double>();
        var wetDurations = new HashSet<double>();
        for (int phase = 0; phase < 100; phase++)
        {
            bool wet = Property<bool>(state, "IsPrecipitating");
            double duration = Property<double>(state, "PhaseDurationSeconds");
            Assert.That(duration / DaySeconds, Is.InRange(wet ? minWet : minDry, wet ? maxWet : maxDry));
            (wet ? wetDurations : dryDurations).Add(duration);
            Assert.That(state.GetType().GetProperty("Kind").GetValue(state).ToString(), Is.EqualTo(kind));

            // 같은 단계 안에서는 수분만 바뀌고 기간과 난수 상태는 유지되어야 합니다.
            uint random = Property<uint>(state, "RandomState");
            double start = Property<double>(state, "PhaseStartSeconds");
            Call(state, "AdvanceTo", start + duration * 0.5d);
            Assert.That(Property<double>(state, "PhaseDurationSeconds"), Is.EqualTo(duration));
            Assert.That(Property<uint>(state, "RandomState"), Is.EqualTo(random));
            Call(state, "AdvanceTo", start + duration);
        }
        Assert.That(dryDurations.Count, Is.GreaterThan(20));
        Assert.That(wetDurations.Count, Is.GreaterThan(20));
    }

    [Test]
    public void EqualSeedsAndInputs_ProduceTheSameSchedule()
    {
        object settings = CreateSettings("Rain", 1f, 1.5f, 1.5f, 3.5f);
        object first = CreateState(settings, seed: 49281u);
        object second = CreateState(settings, seed: 49281u);
        foreach (double time in new[] { 0d, 1d, 1234d, 123456d, 999999d })
        {
            Call(first, "AdvanceTo", time);
            Call(second, "AdvanceTo", time);
            AssertEquivalent(first, second);
        }
    }

    [Test]
    public void Regions_HaveIndependentMoistureAndRandomSchedulesWithSharedConfiguration()
    {
        object settings = CreateSettings("Rain", 1f, 1.5f, 1.5f, 3.5f);
        object first = CreateState(settings, regionIndex: 0, seed: 701u);
        object second = CreateState(settings, regionIndex: 1, seed: 701u);
        Assert.That(Property<double>(first, "PhaseDurationSeconds"),
            Is.Not.EqualTo(Property<double>(second, "PhaseDurationSeconds")));
        Call(first, "AdvanceTo", Property<double>(first, "PhaseDurationSeconds") * 0.5d);
        AssertState(first, false, 50d);
        AssertState(second, false, 0d);
        Assert.That(Field<float>(settings, "MinDryDays"), Is.EqualTo(1f));
    }

    [Test]
    public void LargeTimeSkip_MatchesManySmallUpdatesAcrossMultipleCycles()
    {
        object settings = CreateSettings("Snow", 1.5f, 2.5f, 0.5f, 1f);
        object direct = CreateState(settings, seed: 0x1398AB72u, startSeconds: 360d);
        object stepped = CreateState(settings, seed: 0x1398AB72u, startSeconds: 360d);
        double end = 360d + 2000d * DaySeconds + 123.125d;
        Call(direct, "AdvanceTo", end);
        for (double time = 360d; time < end; time += 731.25d)
            Call(stepped, "AdvanceTo", time);
        Call(stepped, "AdvanceTo", end);
        AssertEquivalent(direct, stepped);
    }

    [Test]
    public void RepeatedTimestamp_DoesNotConsumeRandomNumbersOrChangeState()
    {
        object state = CreateState(CreateSettings("Rain", 1f, 1.5f, 1.5f, 3.5f));
        Call(state, "AdvanceTo", 1300d);
        object before = Call(state, "Capture");
        for (int i = 0; i < 100; i++) Assert.That(Call(state, "AdvanceTo", 1300d), Is.False);
        AssertSnapshotsEqual(before, Call(state, "Capture"));
    }

    [Test]
    public void RestoreMidRain_PreservesMoistureAndAllFutureRandomPeriods()
    {
        object settings = CreateSettings("Rain", 1f, 1.5f, 1.5f, 3.5f);
        object original = CreateState(settings, seed: 34282u);
        Call(original, "AdvanceTo", Property<double>(original, "PhaseDurationSeconds"));
        double middle = Property<double>(original, "PhaseStartSeconds") +
            Property<double>(original, "PhaseDurationSeconds") * 0.37d;
        Call(original, "AdvanceTo", middle);
        object restored = CreateState(settings, seed: 1u);
        Call(restored, "Restore", Call(original, "Capture"));
        AssertEquivalent(original, restored);
        foreach (double time in new[] { middle + 100d, middle + 9999d, middle + 300000d })
        {
            Call(original, "AdvanceTo", time);
            Call(restored, "AdvanceTo", time);
            AssertEquivalent(original, restored);
        }
    }

    [Test]
    public void Capture_ReturnsAnIndependentCopyOfTheRuntimeState()
    {
        object state = CreateState(CreateSettings("Rain", 1f, 1f, 1f, 1f));
        Call(state, "AdvanceTo", DaySeconds * 0.5d);
        object captured = Call(state, "Capture");
        SetField(captured, "moisture", 0d);
        AssertState(state, false, 50d);
        Assert.That(Field<double>(Call(state, "Capture"), "moisture"), Is.EqualTo(50d));
    }

    [Test]
    public void Configuration_IsCopiedSoAssetEditsDoNotChangeAnExistingCycle()
    {
        object settings = CreateSettings("Rain", 1f, 1f, 2f, 2f);
        object state = CreateState(settings);
        SetField(settings, "MinWetDays", 99f);
        SetField(settings, "MaxWetDays", 99f);
        Call(state, "AdvanceTo", DaySeconds);
        Assert.That(Property<double>(state, "PhaseDurationSeconds"), Is.EqualTo(2d * DaySeconds));
    }

    [Test]
    public void Desert_DisabledPrecipitationRemainsDryAfterLongTimeAndRestores()
    {
        object settings = CreateSettings("None", 0f, 0f, 0f, 0f);
        object state = CreateState(settings, startSeconds: 360d);
        Call(state, "AdvanceTo", 10000d * DaySeconds);
        AssertState(state, false, 0d);
        Assert.That(Property<double>(state, "PhaseDurationSeconds"), Is.EqualTo(0d));
        object restored = CreateState(settings);
        Call(restored, "Restore", Call(state, "Capture"));
        AssertEquivalent(state, restored);
    }

    [TestCase("MinDryDays", 0f)]
    [TestCase("MinDryDays", -1f)]
    [TestCase("MinWetDays", float.NaN)]
    [TestCase("MaxWetDays", float.PositiveInfinity)]
    [TestCase("MinDryDays", 2f)]
    public void InvalidEnabledSettings_AreRejected(string field, float value)
    {
        object settings = CreateSettings("Rain", 1f, 1.5f, 0.5f, 1f);
        SetField(settings, field, value);
        Assert.Catch<ArgumentException>(() => Call(settings, "Validate"));
    }

    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(-1d)]
    public void InvalidOrBackwardWorldTime_IsRejectedWithoutChangingState(double time)
    {
        object state = CreateState(CreateSettings("Rain", 1f, 1f, 1f, 1f));
        Call(state, "AdvanceTo", 10d);
        object before = Call(state, "Capture");
        Assert.Throws<ArgumentOutOfRangeException>(() => Call(state, "AdvanceTo", time));
        AssertSnapshotsEqual(before, Call(state, "Capture"));
    }

    [TestCase("moisture", 101d)]
    [TestCase("moisture", -1d)]
    [TestCase("moisture", 42d)]
    [TestCase("phaseDurationSeconds", 0d)]
    [TestCase("phaseDurationSeconds", double.NaN)]
    [TestCase("lastUpdateSeconds", -1d)]
    public void InvalidRestore_IsRejectedAtomically(string field, double value)
    {
        object state = CreateState(CreateSettings("Rain", 1f, 1f, 1f, 1f));
        Call(state, "AdvanceTo", 100d);
        object before = Call(state, "Capture");
        object invalid = Call(state, "Capture");
        SetField(invalid, field, value);
        Assert.Catch<ArgumentException>(() => Call(state, "Restore", invalid));
        AssertSnapshotsEqual(before, Call(state, "Capture"));
    }

    [Test]
    public void RestoreWithWrongRegionOrZeroRandomState_IsRejected()
    {
        object state = CreateState(CreateSettings("Rain", 1f, 1f, 1f, 1f));
        object invalid = Call(state, "Capture");
        SetField(invalid, "regionIndex", 31);
        Assert.Catch<ArgumentException>(() => Call(state, "Restore", invalid));
        invalid = Call(state, "Capture");
        SetField(invalid, "randomState", 0u);
        Assert.Catch<ArgumentException>(() => Call(state, "Restore", invalid));
    }

    private static object CreateSettings(string kind, float minDry, float maxDry, float minWet, float maxWet)
    {
        object settings = Activator.CreateInstance(FindType("BiomeWeatherSettings"));
        SetField(settings, "Precipitation", Enum.Parse(FindType("PrecipitationKind"), kind));
        SetField(settings, "MinDryDays", minDry);
        SetField(settings, "MaxDryDays", maxDry);
        SetField(settings, "MinWetDays", minWet);
        SetField(settings, "MaxWetDays", maxWet);
        return settings;
    }

    private static object CreateState(object settings, int regionIndex = 0, uint seed = 17u,
        double startSeconds = 0d, double secondsPerDay = DaySeconds)
        => Activator.CreateInstance(FindType("RegionWeatherState"), regionIndex, settings, seed, startSeconds, secondsPerDay);

    private static void AssertState(object state, bool wet, double moisture)
    {
        Assert.That(Property<bool>(state, "IsPrecipitating"), Is.EqualTo(wet));
        Assert.That(Property<double>(state, "Moisture"), Is.EqualTo(moisture).Within(1e-8d));
    }

    private static void AssertEquivalent(object first, object second)
        => AssertSnapshotsEqual(Call(first, "Capture"), Call(second, "Capture"));

    private static void AssertSnapshotsEqual(object first, object second)
    {
        foreach (FieldInfo field in first.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            object expected = field.GetValue(first);
            object actual = field.GetValue(second);
            if (expected is double value)
                Assert.That((double)actual, Is.EqualTo(value).Within(1e-8d), field.Name);
            else
                Assert.That(actual, Is.EqualTo(expected), field.Name);
        }
    }

    private static Type FindType(string name)
        => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static T Property<T>(object target, string name) => (T)target.GetType().GetProperty(name).GetValue(target);
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name).GetValue(target);
    private static void SetField(object target, string name, object value) => target.GetType().GetField(name).SetValue(target, value);

    private static object Call(object target, string name, params object[] args)
    {
        try { return target.GetType().GetMethod(name).Invoke(target, args); }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
