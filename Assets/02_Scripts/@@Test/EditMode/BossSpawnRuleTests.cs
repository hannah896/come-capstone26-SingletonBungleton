using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;

public class BossSpawnRuleTests
{
    private ScriptableObject _rule;
    private object _director;
    private object _state;

    [SetUp]
    public void SetUp()
    {
        _rule = ScriptableObject.CreateInstance(FindType("BossSpawnRule"));
        Set(_rule, "ruleId", "full-moon-boss");
        Set(_rule, "bossMonsterKeys", new string[8]);
        Type directorType = FindType("WorldMonsterSpawnDirector");

        // GameObject 없이 상태를 구성하여 월드 매니저나 실제 몬스터를 초기화하지 않습니다.
        _director = FormatterServices.GetUninitializedObject(directorType);
        InitializeCollection("_initial");
        InitializeCollection("_sources");
        InitializeCollection("_periodic");
        InitializeCollection("_active");
        InitializeCollection("_dormantMembers");
        InitializeCollection("_players");
        _state = Activator.CreateInstance(directorType.GetNestedType("BossState", BindingFlags.NonPublic), true);
        Set(_state, "Rule", _rule);
        PrivateField("_boss").SetValue(_director, _state);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(_rule);
        _rule = null;
        _director = null;
        _state = null;
    }

    [TestCase(5, 0)]
    [TestCase(13, 1)]
    [TestCase(21, 2)]
    [TestCase(61, 7)]
    [TestCase(69, 0)]
    [TestCase(125, 7)]
    [TestCase(133, 0)]
    public void FullMoon_SelectsOneOfEightBosses_AndRepeatsAfterEightMoons(int day, int expectedIndex)
    {
        Assert.That(GetBossIndex(day), Is.EqualTo(expectedIndex));
        var keys = new[] { "Boss1", "Boss2", "Boss3", "Boss4", "Boss5", "Boss6", "Boss7", "Boss8" };
        Set(_rule, "bossMonsterKeys", keys);
        Assert.That(GetMonsterKey(day), Is.EqualTo(keys[expectedIndex]));
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(4)]
    [TestCase(6)]
    [TestCase(12)]
    [TestCase(14)]
    public void NonFullMoonDay_DoesNotSelectOrQueueBoss(int day)
    {
        Assert.That(GetBossIndex(day), Is.EqualTo(-1));
        Assert.That(GetMonsterKey(day), Is.Null.Or.Empty);

        Call(_director, "QueueFullMoon", day);
        Assert.That(Get(_state, "PendingFullMoonDay"), Is.EqualTo(0));
    }

    [Test]
    public void EmptyBossSlot_DoesNotChangeLaterBossOrder()
    {
        Set(_rule, "bossMonsterKeys", new[] { "", "Devil", "", "", "", "", "", "Treeguard" });

        Assert.That(GetMonsterKey(5), Is.Null.Or.Empty);
        Assert.That(GetMonsterKey(13), Is.EqualTo("Devil"));
        Assert.That(GetMonsterKey(61), Is.EqualTo("Treeguard"));
        Assert.That(GetMonsterKey(69), Is.Null.Or.Empty);
    }

    [Test]
    public void QueueFullMoon_AfterTimeSkip_KeepsOnlyLatestMoon()
    {
        Call(_director, "QueueFullMoon", 5);
        Call(_director, "QueueFullMoon", 21);
        Call(_director, "QueueFullMoon", 13);
        Call(_director, "UpdateBoss");

        Assert.That(Get(_state, "PendingFullMoonDay"), Is.EqualTo(21));
        Assert.That(Get(_state, "LastFullMoonDay"), Is.EqualTo(0));
    }

    [Test]
    public void SelectedBlankBossSlot_RetainsPendingMoonForRetry()
    {
        Set(_rule, "bossMonsterKeys", new[] { "Devil", "", "", "", "", "", "", "" });
        Call(_director, "QueueFullMoon", 13);

        Call(_director, "UpdateBoss");
        Call(_director, "UpdateBoss");

        Assert.That(Get(_state, "PendingFullMoonDay"), Is.EqualTo(13));
        Assert.That(Get(_state, "LastFullMoonDay"), Is.EqualTo(0));
        Assert.That(Get(_state, "Spawning"), Is.False);
    }

    [Test]
    public void LivingDormantBoss_ConsumesCurrentMoon_EvenWhenSelectedSlotIsBlank()
    {
        AddDormantBoss("full-moon-boss", "Devil");
        Call(_director, "QueueFullMoon", 13);

        Call(_director, "UpdateBoss");

        Assert.That(Get(_state, "LastFullMoonDay"), Is.EqualTo(13));
        Assert.That(Get(_state, "Spawning"), Is.False);
        Assert.That(((IList)PrivateField("_dormantMembers").GetValue(_director)).Count, Is.EqualTo(1));
    }

    [Test]
    public void DormantBossFromAnotherRule_DoesNotConsumePendingMoon()
    {
        AddDormantBoss("other-boss-rule", "Devil");
        Call(_director, "QueueFullMoon", 13);

        Call(_director, "UpdateBoss");

        Assert.That(Get(_state, "LastFullMoonDay"), Is.EqualTo(0));
    }

    [Test]
    public void BossSpawnInProgress_DoesNotConsumeNewMoonUntilSpawnCompletes()
    {
        AddDormantBoss("full-moon-boss", "Devil");
        Set(_state, "Spawning", true);
        Call(_director, "QueueFullMoon", 13);

        Call(_director, "UpdateBoss");
        Assert.That(Get(_state, "LastFullMoonDay"), Is.EqualTo(0));

        Set(_state, "Spawning", false);
        Call(_director, "UpdateBoss");
        Assert.That(Get(_state, "LastFullMoonDay"), Is.EqualTo(13));
    }

    [Test]
    public void CaptureAndJsonRoundTrip_PreservePendingMoonAndExactDormantBossKind()
    {
        Set(_state, "LastFullMoonDay", 5);
        Set(_state, "PendingFullMoonDay", 13);
        AddDormantBoss("full-moon-boss", "Devil");

        object captured = Call(_director, "Capture");
        object restored = JsonUtility.FromJson(JsonUtility.ToJson(captured), captured.GetType());
        object boss = Get(restored, "boss");
        Assert.That(Get(boss, "ruleId"), Is.EqualTo("full-moon-boss"));
        Assert.That(Get(boss, "lastFullMoonDay"), Is.EqualTo(5));
        Assert.That(Get(boss, "pendingFullMoonDay"), Is.EqualTo(13));
        object member = ((IList)Get(restored, "members"))[0];
        Assert.That(Get(member, "bossRuleId"), Is.EqualTo("full-moon-boss"));
        Assert.That(Get(member, "monsterKey"), Is.EqualTo("Devil"));
        Assert.That(Get(member, "sourceId"), Is.EqualTo(0));
        Assert.That(Get(member, "periodicRuleId"), Is.Null.Or.Empty);
        Assert.That(Get(member, "position"), Is.EqualTo(new Vector3(10f, 2f, 20f)));
        Assert.That(Get(member, "hp"), Is.EqualTo(27f));
    }

    [Test]
    public void LegacyPeriodicBoss_RestoresAsBoss_WithOriginalFirstBossKind()
    {
        Set(_rule, "ruleId", "full-moon-devil");
        Set(_rule, "bossMonsterKeys", new[] { "Devil", "Treeguard", "", "", "", "", "", "" });
        object saved = Create("MonsterSpawnSaveData");
        object legacy = Create("PeriodicSpawnSaveData");
        Set(legacy, "ruleId", "full-moon-devil");
        Set(legacy, "lastFullMoonDay", 5);
        Set(legacy, "pendingFullMoonDay", 13);
        ((IList)Get(saved, "periodic")).Add(legacy);
        object member = AddSavedMember(saved, 0, "full-moon-devil", null, null);
        Set(member, "hp", 23f);

        Call(_director, "Restore", saved);

        Assert.That(Get(_state, "LastFullMoonDay"), Is.EqualTo(5));
        Assert.That(Get(_state, "PendingFullMoonDay"), Is.EqualTo(13));
        object restoredMember = ((IList)PrivateField("_dormantMembers").GetValue(_director))[0];
        Assert.That(Get(restoredMember, "BossId"), Is.EqualTo("full-moon-devil"));
        Assert.That(Get(restoredMember, "PeriodicId"), Is.Null.Or.Empty);
        Assert.That(Get(restoredMember, "MonsterKey"), Is.EqualTo("Devil"), "기존 개체를 다음 만월의 보스 종류로 바꾸면 안 됩니다.");
        Assert.That(Get(restoredMember, "Position"), Is.EqualTo(new Vector3(10f, 2f, 20f)));
        Assert.That(Get(restoredMember, "Hp"), Is.EqualTo(23f));
    }

    [Test]
    public void LegacyPeriodicBoss_WithMultipleLivingMembers_RejectsAmbiguousBossRestore()
    {
        Set(_rule, "ruleId", "full-moon-devil");
        Set(_rule, "bossMonsterKeys", new[] { "Devil", "Treeguard", "", "", "", "", "", "" });
        object saved = Create("MonsterSpawnSaveData");
        object legacy = Create("PeriodicSpawnSaveData");
        Set(legacy, "ruleId", "full-moon-devil");
        Set(legacy, "lastFullMoonDay", 5);
        ((IList)Get(saved, "periodic")).Add(legacy);
        AddSavedMember(saved, 0, "full-moon-devil", null, null);
        AddSavedMember(saved, 0, "full-moon-devil", null, null);

        var error = Assert.Throws<TargetInvocationException>(() => Call(_director, "Restore", saved));
        Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [TestCase(false)]
    [TestCase(true)]
    public void JsonRoundTrip_WithoutConfiguredBossState_DoesNotBlockValidationOrRestore(bool emptyBossObject)
    {
        PrivateField("_boss").SetValue(_director, null);
        object saved = Create("MonsterSpawnSaveData");
        // Unity의 인라인 직렬화가 null을 기본 객체로 표현하는 경우도 포함합니다.
        if (emptyBossObject) Set(saved, "boss", Create("BossSpawnSaveData"));
        object restored = JsonUtility.FromJson(JsonUtility.ToJson(saved), saved.GetType());

        Assert.DoesNotThrow(() =>
        {
            ValidateSpawns(restored);
            Call(_director, "Restore", restored);
        });
    }

    [Test]
    public void BossRestore_PreservesSavedMonsterKind_WhenPendingMoonSelectsDifferentBoss()
    {
        Set(_rule, "bossMonsterKeys", new[] { "Devil", "Treeguard", "", "", "", "", "", "" });
        object saved = Create("MonsterSpawnSaveData");
        object boss = Create("BossSpawnSaveData");
        Set(boss, "ruleId", "full-moon-boss");
        Set(boss, "lastFullMoonDay", 5);
        Set(boss, "pendingFullMoonDay", 13);
        Set(saved, "boss", boss);
        AddSavedMember(saved, 0, null, "full-moon-boss", "Devil");

        Call(_director, "Restore", saved);

        Assert.That(Get(_state, "LastFullMoonDay"), Is.EqualTo(5));
        Assert.That(Get(_state, "PendingFullMoonDay"), Is.EqualTo(13));
        object restoredMember = ((IList)PrivateField("_dormantMembers").GetValue(_director))[0];
        Assert.That(Get(restoredMember, "MonsterKey"), Is.EqualTo("Devil"));
        Assert.That(Get(restoredMember, "BossId"), Is.EqualTo("full-moon-boss"));
    }

    [Test]
    public void PeriodicRestore_KeepsOrdinaryDeadline_WithoutChangingBossSchedule()
    {
        Type stateType = _director.GetType().GetNestedType("PeriodicState", BindingFlags.NonPublic);
        object periodicState = Activator.CreateInstance(stateType, true);
        ((IDictionary)PrivateField("_periodic").GetValue(_director)).Add("interval-wave", periodicState);
        Set(_state, "LastFullMoonDay", 5);
        Set(_state, "PendingFullMoonDay", 13);
        object saved = Create("MonsterSpawnSaveData");
        object periodic = Create("PeriodicSpawnSaveData");
        Set(periodic, "ruleId", "interval-wave");
        Set(periodic, "nextSpawnTime", 4320f);
        ((IList)Get(saved, "periodic")).Add(periodic);

        Call(_director, "Restore", saved);

        Assert.That(Get(periodicState, "NextTime"), Is.EqualTo(4320f));
        Assert.That(Get(_state, "LastFullMoonDay"), Is.EqualTo(5));
        Assert.That(Get(_state, "PendingFullMoonDay"), Is.EqualTo(13));
    }

    [TestCase(0, null, null)]
    [TestCase(42, "interval-wave", null)]
    [TestCase(42, null, "full-moon-boss")]
    [TestCase(0, "interval-wave", "full-moon-boss")]
    [TestCase(42, "interval-wave", "full-moon-boss")]
    public void SaveValidation_RejectsMissingOrMultipleMonsterOrigins(int sourceId, string periodicId, string bossId)
    {
        object data = Create("MonsterSpawnSaveData");
        AddSavedMember(data, sourceId, periodicId, bossId, "Devil");

        var error = Assert.Throws<TargetInvocationException>(() => ValidateSpawns(data));
        Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [TestCase(42, null, null)]
    [TestCase(0, "interval-wave", null)]
    [TestCase(0, null, "full-moon-boss")]
    public void SaveValidation_AcceptsOneMonsterOrigin(int sourceId, string periodicId, string bossId)
    {
        object data = Create("MonsterSpawnSaveData");
        AddSavedMember(data, sourceId, periodicId, bossId, "Devil");

        Assert.DoesNotThrow(() => ValidateSpawns(data));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void SaveValidation_RejectsBossWithoutExactMonsterKey(string key)
    {
        object data = Create("MonsterSpawnSaveData");
        AddSavedMember(data, 0, null, "full-moon-boss", key);

        var error = Assert.Throws<TargetInvocationException>(() => ValidateSpawns(data));
        Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    private void AddDormantBoss(string bossId, string monsterKey)
    {
        Type memberType = _director.GetType().GetNestedType("MemberState", BindingFlags.NonPublic);
        object member = Activator.CreateInstance(memberType, true);
        Set(member, "BossId", bossId);
        Set(member, "MonsterKey", monsterKey);
        Set(member, "Position", new Vector3(10f, 2f, 20f));
        Set(member, "Hp", 27f);
        ((IList)PrivateField("_dormantMembers").GetValue(_director)).Add(member);
    }

    private void InitializeCollection(string name)
    {
        FieldInfo field = PrivateField(name);
        field.SetValue(_director, Activator.CreateInstance(field.FieldType));
    }

    private FieldInfo PrivateField(string name) => _director.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static int GetBossIndex(int day) => (int)FindType("BossSpawnRule").GetMethod("GetBossIndex").Invoke(null, new object[] { day });
    private string GetMonsterKey(int day) => (string)_rule.GetType().GetMethod("GetMonsterKey").Invoke(_rule, new object[] { day });
    private static object AddSavedMember(object data, int sourceId, string periodicId, string bossId, string key)
    {
        if (!string.IsNullOrWhiteSpace(bossId) && Get(data, "boss") == null)
        {
            object boss = Create("BossSpawnSaveData");
            Set(boss, "ruleId", bossId);
            Set(data, "boss", boss);
        }
        object member = Create("SpawnedMonsterSaveData");
        Set(member, "sourceId", sourceId);
        Set(member, "periodicRuleId", periodicId);
        Set(member, "bossRuleId", bossId);
        Set(member, "monsterKey", key);
        Set(member, "position", new Vector3(10f, 2f, 20f));
        Set(member, "hp", 27f);
        ((IList)Get(data, "members")).Add(member);
        return member;
    }

    private static void ValidateSpawns(object data) => FindType("WorldSaveAdapter")
        .GetMethod("ValidateMonsterSpawns", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { data });
    private static object Create(string name) => Activator.CreateInstance(FindType(name));
    private static object Call(object target, string method, params object[] args)
        => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    private static object Get(object target, string field) => target.GetType().GetField(field).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field).SetValue(target, value);
    private static Type FindType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(name);
            if (type != null) return type;
        }
        throw new InvalidOperationException($"타입을 찾을 수 없습니다: {name}");
    }
}
