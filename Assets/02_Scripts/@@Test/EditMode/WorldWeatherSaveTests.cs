using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class WorldWeatherSaveTests
{
    [Test]
    public void WholeWorldJsonRoundTrip_RestoresMixedRegionalWeatherAndFuturePeriods()
    {
        object world = CreateWorld();
        Validate(world);
        string json = JsonUtility.ToJson(world);
        object restoredWorld = JsonUtility.FromJson(json, world.GetType());
        Validate(restoredWorld);
        Assert.That(Field<string>(restoredWorld, "generationId"), Is.EqualTo(Field<string>(world, "generationId")));
        Assert.That(Field<int>(restoredWorld, "seed"), Is.EqualTo(Field<int>(world, "seed")));
        Assert.That(Field<double>(restoredWorld, "clockSeconds"), Is.EqualTo(Field<double>(world, "clockSeconds")));

        IList originalRegions = Field<IList>(world, "regions");
        IList restoredRegions = Field<IList>(restoredWorld, "regions");
        for (int i = 0; i < originalRegions.Count; i++)
        {
            object first = CreateState(i);
            object second = CreateState(i);
            Call(first, "Restore", originalRegions[i]);
            Call(second, "Restore", restoredRegions[i]);
            Assert.That(JsonUtility.ToJson(Call(second, "Capture")), Is.EqualTo(JsonUtility.ToJson(Call(first, "Capture"))));
            Call(first, "AdvanceTo", 1440d * 200d);
            Call(second, "AdvanceTo", 1440d * 200d);
            Assert.That(JsonUtility.ToJson(Call(second, "Capture")), Is.EqualTo(JsonUtility.ToJson(Call(first, "Capture"))));
        }
    }

    [Test]
    public void MissingWeatherFromLegacySave_IsAccepted()
    {
        Assert.DoesNotThrow(() => Validate(null));
    }

    [Test]
    public void EmptyWorldWithValidIdentity_IsAccepted()
    {
        object world = CreateWorld();
        SetField(world, "regionCount", 0);
        Field<IList>(world, "regions").Clear();
        Assert.DoesNotThrow(() => Validate(world));
    }

    [TestCase("version", -1)]
    [TestCase("version", 2)]
    [TestCase("regionCount", -1)]
    [TestCase("regionCount", 4097)]
    public void InvalidEnvelopeInteger_IsRejected(string field, int value)
    {
        object world = CreateWorld();
        SetField(world, field, value);
        Assert.Catch<ArgumentException>(() => Validate(world));
    }

    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(-1d)]
    public void InvalidWorldClock_IsRejected(double time)
    {
        object world = CreateWorld();
        SetField(world, "clockSeconds", time);
        Assert.Catch<ArgumentException>(() => Validate(world));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not-a-world-guid")]
    public void InvalidGenerationIdentity_IsRejected(string identity)
    {
        object world = CreateWorld();
        SetField(world, "generationId", identity);
        Assert.Catch<ArgumentException>(() => Validate(world));
    }

    [TestCase("Duplicate")]
    [TestCase("Missing")]
    [TestCase("OutOfRange")]
    [TestCase("NullRegion")]
    [TestCase("NullList")]
    public void MissingOrDuplicateRegions_AreRejected(string invalid)
    {
        object world = CreateWorld();
        IList regions = Field<IList>(world, "regions");
        switch (invalid)
        {
            case "Duplicate": SetField(regions[1], "regionIndex", 0); break;
            case "Missing": regions.RemoveAt(1); break;
            case "OutOfRange": SetField(regions[1], "regionIndex", 4); break;
            case "NullRegion": regions[1] = null; break;
            case "NullList": SetField(world, "regions", null); break;
        }
        Assert.Catch<ArgumentException>(() => Validate(world));
    }

    [TestCase("moisture", double.NaN)]
    [TestCase("moisture", double.PositiveInfinity)]
    [TestCase("moisture", -1d)]
    [TestCase("moisture", 101d)]
    [TestCase("moisture", 33d)]
    [TestCase("phaseStartSeconds", double.NaN)]
    [TestCase("phaseStartSeconds", -1d)]
    [TestCase("phaseDurationSeconds", 0d)]
    [TestCase("phaseDurationSeconds", double.PositiveInfinity)]
    [TestCase("lastUpdateSeconds", -1d)]
    [TestCase("lastUpdateSeconds", double.NaN)]
    public void InvalidRegionalNumbersOrMoistureTimeMismatch_AreRejected(string field, double value)
    {
        object world = CreateWorld();
        SetField(Field<IList>(world, "regions")[0], field, value);
        Assert.Catch<ArgumentException>(() => Validate(world));
    }

    [Test]
    public void FutureRegionalTimestamp_IsRejected()
    {
        object world = CreateWorld();
        object region = Field<IList>(world, "regions")[0];
        SetField(region, "lastUpdateSeconds", Field<double>(world, "clockSeconds") + 1d);
        Assert.Catch<ArgumentException>(() => Validate(world));
    }

    [TestCase("Random")]
    [TestCase("Kind")]
    public void InvalidRandomStateOrPrecipitationKind_IsRejected(string invalid)
    {
        object world = CreateWorld();
        object region = Field<IList>(world, "regions")[0];
        SetField(region, invalid == "Random" ? "randomState" : "precipitationKind", invalid == "Random" ? (object)0u : 3);
        Assert.Catch<ArgumentException>(() => Validate(world));
    }

    [TestCase("Raining")]
    [TestCase("Moisture")]
    [TestCase("Duration")]
    public void DisabledRegionCannotContainRainOrMoisture(string invalid)
    {
        object world = CreateWorld();
        object desert = Field<IList>(world, "regions")[3];
        if (invalid == "Raining") SetField(desert, "isPrecipitating", true);
        if (invalid == "Moisture") SetField(desert, "moisture", 1d);
        if (invalid == "Duration") SetField(desert, "phaseDurationSeconds", 1440d);
        Assert.Catch<ArgumentException>(() => Validate(world));
    }

    private static object CreateWorld()
    {
        object world = Activator.CreateInstance(FindType("WorldWeatherSaveData"));
        SetField(world, "seed", 879431);
        SetField(world, "regionCount", 4);
        SetField(world, "generationId", Guid.NewGuid().ToString("N"));
        double clock = 360d + 1440d * 2.125d;
        SetField(world, "clockSeconds", clock);
        IList regions = Field<IList>(world, "regions");
        for (int region = 0; region < 4; region++)
        {
            object state = CreateState(region);
            Call(state, "AdvanceTo", clock);
            regions.Add(Call(state, "Capture"));
        }
        return world;
    }

    private static object CreateState(int region)
    {
        object settings = Activator.CreateInstance(FindType("BiomeWeatherSettings"));
        string kind = region == 3 ? "None" : region == 2 ? "Snow" : "Rain";
        SetField(settings, "Precipitation", Enum.Parse(FindType("PrecipitationKind"), kind));
        SetField(settings, "MinDryDays", region == 0 ? 2f : 1f);
        SetField(settings, "MaxDryDays", region == 0 ? 2f : 1.5f);
        SetField(settings, "MinWetDays", 0.5f);
        SetField(settings, "MaxWetDays", 1f);
        return Activator.CreateInstance(FindType("RegionWeatherState"), region, settings, 879431u, 360d, 1440d);
    }

    private static void Validate(object data)
    {
        try { FindType("WorldWeatherSaveData").GetMethod("Validate").Invoke(null, new[] { data }); }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static object Call(object target, string name, params object[] args)
        => target.GetType().GetMethod(name).Invoke(target, args);

    private static Type FindType(string name)
        => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name).GetValue(target);
    private static void SetField(object target, string name, object value) => target.GetType().GetField(name).SetValue(target, value);
}
