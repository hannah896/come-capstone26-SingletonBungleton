using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class StartSofaTests
{
    private const string PrefabPath = "Assets/03_Prefabs/Objects/Structure/StartSofa.prefab";
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly List<GameObject> created = new();
    private Type sofaType;
    private Component sofa;
    private BoxCollider seat;
    private Transform cushion;

    [SetUp]
    public void SetUp() => sofaType = FindType("StartSofa");

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
            if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    [Test]
    public void Prefab_ReferencesAnimatedCushionAndStableRootCollider()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        Component component = prefab.GetComponent(sofaType);
        Assert.That(component, Is.Not.Null, "제단 컴포넌트가 프리팹에 포함되어야 한다.");
        Transform visual = prefab.transform.Find("P_PROP_sofa_cushion_interior_01");
        BoxCollider collider = prefab.GetComponent<BoxCollider>();
        Assert.That(visual, Is.Not.Null);
        Assert.That(collider, Is.Not.Null);
        Assert.That(GetField(component, "cushion"), Is.SameAs(visual));
        Assert.That(GetField(component, "seatCollider"), Is.SameAs(collider));
        Assert.That(collider.enabled && !collider.isTrigger, Is.True);
        Assert.That(visual.GetComponent<BoxCollider>().enabled, Is.False,
            "쿠션을 늘려도 물리 충돌체 크기는 변하지 않아야 한다.");
        Assert.That(GameObjectUtility.GetStaticEditorFlags(visual.gameObject), Is.EqualTo((StaticEditorFlags)0));
        Assert.That(visual.localScale.y, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(collider.size.x, Is.EqualTo(2.529481f).Within(0.0001f));
        Assert.That(collider.size.y, Is.EqualTo(0.12737912f).Within(0.0001f));
        Assert.That(collider.size.z, Is.EqualTo(2.6026512f).Within(0.0001f));
    }

    [Test]
    public void DemolitionAndDamage_RejectRequestsWithoutRemovingAltar()
    {
        CreateSofa();
        object context = Activator.CreateInstance(FindType("DamageContext"), new object[]
        {
            null, Vector3.zero, int.MaxValue, "Tool_Hammer_Gold", Enum.ToObject(FindType("ActionType"), 0), null
        });
        Assert.That(Invoke(sofa, "CanDemolish"), Is.False);
        Assert.That(Invoke(sofa, "CanDamage", context), Is.False);
        Invoke(sofa, "ApplyDamage", context);
        Invoke(sofa, "Demolish");
        Assert.That(sofa != null && sofa.gameObject != null, Is.True);
        Assert.That(cushion.localScale.y, Is.EqualTo(0.5f));
    }

    [Test]
    public void SpawnPosition_IsCenteredAboveSeatByFallHeight()
    {
        CreateSofa();
        Vector3 position = (Vector3)Invoke(sofa, "GetSpawnPosition");
        Bounds bounds = seat.bounds;
        Assert.That(position.x, Is.EqualTo(bounds.center.x).Within(0.0001f));
        Assert.That(position.z, Is.EqualTo(bounds.center.z).Within(0.0001f));
        Assert.That(position.y - bounds.max.y, Is.EqualTo((float)Property(sofa, "FallHeight")).Within(0.0001f));
    }

    [Test]
    public void ReturnPosition_EntireScaledOffsetCapsuleClearsPositiveXEdge()
    {
        CreateSofa();
        Component player = CreatePlayer();
        CharacterController controller = player.GetComponent<CharacterController>();
        Vector3 position = (Vector3)Invoke(sofa, "GetSafeReturnPosition", player);
        Vector3 centerOffset = player.transform.TransformVector(controller.center);
        float radius = controller.radius * Mathf.Max(Mathf.Abs(player.transform.lossyScale.x), Mathf.Abs(player.transform.lossyScale.z));
        float feetOffset = (float)sofaType.GetMethod("GetFeetOffset").Invoke(null, new object[] { player });
        Assert.That(position.x + centerOffset.x - radius - seat.bounds.max.x, Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(position.z + centerOffset.z, Is.EqualTo(seat.bounds.center.z).Within(0.0001f));
        Assert.That(position.y + feetOffset, Is.EqualTo(3.03f).Within(0.0001f));
    }

    [Test]
    public void Bounce_StretchesVisualWithoutChangingSeatBounds()
    {
        CreateSofa();
        Bounds before = seat.bounds;
        Vector3 originalScale = cushion.localScale;
        Invoke(sofa, "PlayBounce");
        Assert.That(cushion.localScale.y, Is.EqualTo(5f));
        Assert.That(cushion.localScale.x, Is.EqualTo(originalScale.x));
        Assert.That(cushion.localScale.z, Is.EqualTo(originalScale.z));
        AssertVector(seat.bounds.center, before.center);
        AssertVector(seat.bounds.size, before.size);
        Assert.That((float)Property(sofa, "StretchLift"), Is.GreaterThan(0f));
    }

    [Test]
    public void Intro_FallsThenTravelsPositiveXAndEndsAtSafeLanding()
    {
        CreateSofa();
        Component player = CreatePlayer();
        object intro = Activator.CreateInstance(FindType("PlayerSpawnIntro"), player, sofa);
        Vector3 start = player.transform.position;
        Vector3 landing = (Vector3)Invoke(sofa, "GetSafeReturnPosition", player);
        float fallDuration = (float)Property(sofa, "FallDuration");
        float bounceDuration = (float)Property(sofa, "BounceDuration");
        float fallHeight = (float)Property(sofa, "FallHeight");

        Assert.That(Invoke(intro, "Tick", 0f), Is.True);
        AssertVector(player.transform.position, start);
        Assert.That(Invoke(intro, "Tick", fallDuration * 0.5f), Is.True);
        Vector3 falling = player.transform.position;
        Assert.That(falling.x, Is.EqualTo(start.x).Within(0.0001f));
        Assert.That(falling.z, Is.EqualTo(start.z).Within(0.0001f));
        Assert.That(start.y - falling.y, Is.EqualTo(fallHeight * 0.25f).Within(0.0001f));

        Assert.That(Invoke(intro, "Tick", fallDuration * 0.5f), Is.True);
        Assert.That(cushion.localScale.y, Is.EqualTo(5f));
        Vector3 impact = player.transform.position;
        Assert.That(impact.x, Is.EqualTo(start.x).Within(0.0001f));
        Assert.That(impact.z, Is.EqualTo(start.z).Within(0.0001f));
        Assert.That(impact.y, Is.EqualTo(start.y - fallHeight + (float)Property(sofa, "StretchLift")).Within(0.0001f));

        Assert.That(Invoke(intro, "Tick", bounceDuration * 0.5f), Is.True);
        Vector3 bouncing = player.transform.position;
        Assert.That(bouncing.x, Is.GreaterThan(start.x).And.LessThan(landing.x));
        Assert.That(bouncing.z, Is.EqualTo(start.z).Within(0.0001f));
        Assert.That(bouncing.y, Is.GreaterThan(landing.y));
        Assert.That(Invoke(intro, "Tick", bounceDuration * 0.5f + 0.001f), Is.False);
        AssertVector(player.transform.position, landing);
    }

    private void CreateSofa()
    {
        // 비활성 제단은 Main 루프를 초기화하지 않는다. 별도 충돌체만 활성화해 bounds를 계산한다.
        GameObject root = Track(new GameObject("StartSofa_Test"));
        root.SetActive(false);
        root.transform.position = new Vector3(40f, 3f, 50f);
        sofa = root.AddComponent(sofaType);
        cushion = new GameObject("P_PROP_sofa_cushion_interior_01").transform;
        cushion.SetParent(root.transform, false);
        cushion.localScale = new Vector3(4f, 0.5f, 4f);
        GameObject seatRoot = Track(new GameObject("StartSofa_Seat_Test"));
        seatRoot.transform.position = root.transform.position;
        seat = seatRoot.AddComponent<BoxCollider>();
        seat.center = new Vector3(0.005529523f, 0.061487954f, 0.017863095f);
        seat.size = new Vector3(2.529481f, 0.12737912f, 2.6026512f);
        SetField(sofa, "cushion", cushion);
        SetField(sofa, "seatCollider", seat);
        GameObject ground = Track(new GameObject("StartSofa_Ground_Test"));
        ground.layer = LayerMask.NameToLayer("Ground");
        ground.transform.position = new Vector3(40f, 2.5f, 50f);
        ground.AddComponent<BoxCollider>().size = new Vector3(20f, 1f, 20f);
        Physics.SyncTransforms();
    }

    private Component CreatePlayer()
    {
        GameObject root = Track(new GameObject("StartSofa_Player_Test"));
        root.SetActive(false);
        root.transform.localScale = new Vector3(1.5f, 1.2f, 0.8f);
        root.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
        CharacterController controller = root.AddComponent<CharacterController>();
        controller.radius = 0.35f;
        controller.height = 2.2f;
        controller.center = new Vector3(0.2f, 1.2f, -0.1f);
        Component motor = root.AddComponent(FindType("PlayerMotor"));
        Component player = root.AddComponent(FindType("Player"));
        SetField(player, "motor", motor);
        return player;
    }

    private GameObject Track(GameObject value) { created.Add(value); return value; }
    private static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object GetField(object target, string name) => target.GetType().GetField(name, Members).GetValue(target);
    private static void SetField(object target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
    private static object Property(object target, string name) => target.GetType().GetProperty(name, Members).GetValue(target);
    private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Members).Invoke(target, args);
    private static void AssertVector(Vector3 actual, Vector3 expected)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
        Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f));
    }
}
