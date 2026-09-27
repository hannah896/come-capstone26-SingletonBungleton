using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

public class SaveSlotDeleteUiTests
{
    private const string PrefabDirectory = "Assets/03_Prefabs/UI/Lobby/";
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private GameObject root;
    private FieldInfo mainInstance;
    private object previousMain;
    private object isolatedSave;
    private string temporarySaveDirectory;
    private string localPlayerId;

    [SetUp]
    public void SetUp()
    {
        // 실제 게임 초기화/저장 경로 접근 없이 팝업 닫기 경로만 검증한다.
        root = new GameObject("SaveSlotDeleteUiTests", typeof(RectTransform));
        root.hideFlags = HideFlags.HideAndDontSave;
        root.SetActive(false);
        Type mainType = FindType("Main");
        mainInstance = mainType.GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        previousMain = mainInstance.GetValue(null);
        Component isolatedMain = root.AddComponent(mainType);
        mainInstance.SetValue(null, isolatedMain);
        isolatedSave = Get(isolatedMain, "_save");
        object ui = Get(isolatedMain, "_ui");
        Type popupLayer = ui.GetType().GetNestedType("PopupLayer", BindingFlags.NonPublic);
        Set(ui, "_popups", Activator.CreateInstance(popupLayer, new object[] { null }));
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) UnityEngine.Object.DestroyImmediate(root);
        mainInstance?.SetValue(null, previousMain);
        if (temporarySaveDirectory != null && Directory.Exists(temporarySaveDirectory))
            Directory.Delete(temporarySaveDirectory, true);
    }

    [TestCase("ConfirmButton", true)]
    [TestCase("CancelButton", false)]
    [TestCase("CloseButton", false)]
    public void ConfirmationPrefab_ResolvesFromItsWiredButtons(string field, bool expected)
    {
        Component popup = CreatePopup("UI_Popup_ConfirmDelete");
        object pending = Call(popup, "ConfirmAsync", "테스트 월드", CancellationToken.None);
        Assert.That(IsCompleted(pending), Is.False);

        ButtonFor(popup, field).onClick.Invoke();

        Assert.That(IsCompleted(pending), Is.True);
        Assert.That(Result(pending), Is.EqualTo(expected));
        Assert.That(ButtonFor(popup, "ConfirmButton").interactable, Is.False);
        Assert.That(ButtonFor(popup, "CancelButton").interactable, Is.False);
        Assert.That(ButtonFor(popup, "CloseButton").interactable, Is.False);
        Assert.That(Result(Call(popup, "ConfirmAsync", "다른 월드", CancellationToken.None)), Is.False,
            "닫힌 확인창을 재사용해서 삭제를 승인하면 안 된다.");
    }

    [Test]
    public void Confirmation_CloseWithoutAnswerDoesNotApproveDeletion()
    {
        Component popup = CreatePopup("UI_Popup_ConfirmDelete");
        object pending = Call(popup, "ConfirmAsync", "테스트 월드", CancellationToken.None);
        Call(popup, "Close");
        Assert.That(Result(pending), Is.False);
    }

    [Test]
    public void Confirmation_CanceledLifetimeDoesNotReturnApproval()
    {
        Component popup = CreatePopup("UI_Popup_ConfirmDelete");
        using var cancellation = new CancellationTokenSource();
        object pending = Call(popup, "ConfirmAsync", "테스트 월드", cancellation.Token);
        cancellation.Cancel();
        Assert.That(IsCompleted(pending), Is.True);
        var error = Assert.Throws<TargetInvocationException>(() => Result(pending));
        Assert.That(error.InnerException, Is.InstanceOf<OperationCanceledException>());
        Call(popup, "Close");
    }

    [Test]
    public void Confirmation_ShowsWorldNameAsPlainText()
    {
        Component popup = CreatePopup("UI_Popup_ConfirmDelete");
        Call(popup, "ConfirmAsync", "<b>월드</b>\n이름", CancellationToken.None);
        object message = Get(popup, "MessageText");
        Assert.That(Property(message, "richText"), Is.False);
        Assert.That((string)Property(message, "text"), Does.Contain("<b>월드</b> 이름"));
        Assert.That((string)Property(message, "text"), Does.Contain("복구할 수 없습니다"));
        Call(popup, "Close");
    }

    [Test]
    public void StatusRows_HideDeletionControl()
    {
        Component popup = CreateLoadPopup();
        var row = (Button)Call(popup, "CreateRow", "저장된 월드가 없습니다.");
        Button deletion = row.transform.Find("UI_Button_Close").GetComponent<Button>();
        Assert.That(deletion, Is.Not.Null);
        Assert.That(deletion.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void SaveRows_KeepCorruptedSlotsDeletableAndLockControlsWhileBusy()
    {
        Component popup = CreateLoadPopup();
        Type entryType = FindType("SaveCatalog").GetNestedType("Entry");
        IList entries = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(entryType));
        object valid = NewEntry(entryType, "정상 월드", null);
        object corrupt = NewEntry(entryType, "손상 월드", "손상된 저장 파일");
        entries.Add(valid);
        entries.Add(corrupt);
        Set(popup, "entries", entries);
        Call(popup, "AddPage");
        var rows = (IList)Get(popup, "rows");
        Assert.That(rows.Count, Is.EqualTo(2));
        foreach (object row in rows)
            Assert.That(((Button)Get(row, "Item3")).gameObject.activeSelf, Is.True);

        Call(popup, "ApplySelection", valid);
        Assert.That(((Button)Get(popup, "loadButton")).interactable, Is.True);
        Call(popup, "SetBusy", true);
        foreach (object row in rows)
        {
            Assert.That(((Button)Get(row, "Item2")).interactable, Is.False);
            Assert.That(((Button)Get(row, "Item3")).interactable, Is.False);
        }
        Assert.That(((Button)Get(popup, "loadButton")).interactable, Is.False);
        Assert.That(((Button)Get(popup, "closeButton")).interactable, Is.False);
        Call(popup, "Select", corrupt);
        Assert.That(Get(popup, "selected"), Is.SameAs(valid));

        Call(popup, "SetBusy", false);
        foreach (object row in rows)
        {
            Assert.That(((Button)Get(row, "Item2")).interactable, Is.True);
            Assert.That(((Button)Get(row, "Item3")).interactable, Is.True);
        }
        Assert.That(((Button)Get(popup, "loadButton")).interactable, Is.True);
        Call(popup, "ApplySelection", corrupt);
        Assert.That(((Button)Get(popup, "loadButton")).interactable, Is.False);
    }

    [UnityTest]
    public IEnumerator Refresh_AfterDeletingAnotherSlotPreservesPreferredSelection()
    {
        ConfigureTemporarySaves();
        string preferred = WriteTemporarySave("선택한 월드", DateTime.UtcNow.AddDays(-1));
        WriteTemporarySave("더 최근 월드", DateTime.UtcNow);
        Component popup = CreateLoadPopup();
        yield return Refresh(popup, preferred);
        Assert.That(Get(Get(popup, "selected"), "Path"), Is.EqualTo(preferred));
        Assert.That(((Button)Get(popup, "loadButton")).interactable, Is.True);
    }

    [UnityTest]
    public IEnumerator Refresh_AfterDeletingSelectedSlotSelectsRemainingLoadableWorld()
    {
        ConfigureTemporarySaves();
        string remaining = WriteTemporarySave("남은 월드", DateTime.UtcNow);
        Component popup = CreateLoadPopup();
        yield return Refresh(popup, Path.Combine(temporarySaveDirectory, "deleted.json"));
        Assert.That(Get(Get(popup, "selected"), "Path"), Is.EqualTo(remaining));
        Assert.That(((Button)Get(popup, "loadButton")).interactable, Is.True);
    }

    [UnityTest]
    public IEnumerator Refresh_AfterDeletingLastSlotShowsEmptyStateAndDisablesLoad()
    {
        ConfigureTemporarySaves();
        Component popup = CreateLoadPopup();
        yield return Refresh(popup, Path.Combine(temporarySaveDirectory, "deleted.json"));
        Assert.That(Get(popup, "selected"), Is.Null);
        Assert.That(((IList)Get(popup, "rows")).Count, Is.Zero);
        Assert.That(((Button)Get(popup, "loadButton")).interactable, Is.False);
        int visibleRows = 0;
        foreach (Transform child in (Transform)Get(popup, "SaveListContent"))
        {
            if (!child.gameObject.activeSelf) continue;
            visibleRows++;
            Assert.That(child.GetComponent<Button>().interactable, Is.False);
            Assert.That(child.Find("UI_Button_Close").gameObject.activeSelf, Is.False);
            Component text = child.GetComponentInChildren(FindType("TMPro.TMP_Text"), true);
            Assert.That((string)Property(text, "text"), Is.EqualTo("저장된 월드가 없습니다."));
        }
        Assert.That(visibleRows, Is.EqualTo(1));
    }

    private void ConfigureTemporarySaves()
    {
        temporarySaveDirectory = Path.Combine(Path.GetTempPath(), "SingletonSaveUiTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporarySaveDirectory);
        localPlayerId = Guid.NewGuid().ToString("N");
        isolatedSave.GetType().GetProperty("SaveDirectory").SetValue(isolatedSave, temporarySaveDirectory);
        isolatedSave.GetType().GetProperty("LocalPlayerId").SetValue(isolatedSave, localPlayerId);
    }

    private string WriteTemporarySave(string name, DateTime savedAt)
    {
        object game = Activator.CreateInstance(FindType("GameSaveData"));
        string id = Guid.NewGuid().ToString("N");
        Set(game, "worldId", id);
        Set(game, "worldName", name);
        Set(game, "savedAtUtc", savedAt.ToString("O"));
        Set(game, "world", Activator.CreateInstance(FindType("WorldSaveData")));
        object player = Activator.CreateInstance(FindType("PlayerSaveData"));
        Set(player, "playerId", localPlayerId);
        ((IList)Get(game, "players")).Add(player);
        string json = (string)FindType("SaveFileStore").GetMethod("Serialize").Invoke(null, new[] { game });
        string path = Path.Combine(temporarySaveDirectory, "world_" + id + ".json");
        File.WriteAllText(path, json, new System.Text.UTF8Encoding(false));
        return path;
    }

    private static IEnumerator Refresh(Component popup, string preferred)
    {
        // 런타임 Destroy는 EditMode에서만 오류를 기록하므로 그 메시지만 예상한다.
        var content = (Transform)Get(popup, "SaveListContent");
        for (int i = content.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(content.GetChild(i).gameObject);
        LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));
        object pending = Call(popup, "RefreshAsync", preferred, 50);
        double deadline = EditorApplication.timeSinceStartup + 10;
        while (!IsCompleted(pending) && EditorApplication.timeSinceStartup < deadline) yield return null;
        Assert.That(IsCompleted(pending), Is.True, "임시 저장 목록 읽기가 완료되어야 한다.");
        Result(pending);
    }

    private Component CreatePopup(string typeName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDirectory + typeName + ".prefab");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent(FindType(typeName)), Is.Not.Null, "프리팹에 올바른 팝업 스크립트가 필요하다.");
        GameObject instance = UnityEngine.Object.Instantiate(prefab, root.transform);
        Component popup = instance.GetComponent(FindType(typeName));
        if (typeName == "UI_Popup_ConfirmDelete")
        {
            foreach (string field in new[] { "ConfirmButton", "CancelButton", "CloseButton", "MessageText" })
                Assert.That(Get(popup, field), Is.Not.Null, "프리팹 참조 누락: " + field);
        }
        Set(popup, "AnimationType", Enum.ToObject(FindType("PopupAnimationType"), 0));
        Call(popup, "Initialize");
        return popup;
    }

    private Component CreateLoadPopup()
    {
        Component popup = CreatePopup("UI_Popup_LoadRoom");
        Set(popup, "loadButton", ((Component)Get(popup, "LoadButtonRoot")).GetComponent<Button>());
        Set(popup, "closeButton", ((Component)Get(popup, "CloseButtonRoot")).GetComponent<Button>());
        return popup;
    }

    private static object NewEntry(Type entryType, string name, string error)
    {
        object entry = Activator.CreateInstance(entryType);
        Set(entry, "Name", name);
        Set(entry, "Path", name + ".json");
        Set(entry, "Error", error);
        return entry;
    }

    private static Button ButtonFor(Component popup, string field) => ((Component)Get(popup, field)).GetComponent<Button>();
    private static bool IsCompleted(object task) => (bool)Property(Call(task, "GetAwaiter"), "IsCompleted");
    private static object Result(object task) => Call(Call(task, "GetAwaiter"), "GetResult");
    private static object Property(object target, string name) => target.GetType().GetProperty(name, InstanceFlags).GetValue(target);
    private static object Get(object target, string name) => target.GetType().GetField(name, InstanceFlags).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, InstanceFlags).SetValue(target, value);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, InstanceFlags).Invoke(target, args);
    private static Type FindType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(name);
            if (type != null) return type;
        }
        throw new InvalidOperationException("Type not found: " + name);
    }
}
