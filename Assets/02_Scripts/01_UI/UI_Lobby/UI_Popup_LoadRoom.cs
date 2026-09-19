using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>저장한 월드를 선택한 뒤 기존 싱글/호스트 복원 흐름으로 연결한다.</summary>
public class UI_Popup_LoadRoom : UI_Popup
{
    [SerializeField] private GameObject SaveSlotTemplate;
    [SerializeField] private RectTransform SaveListContent;
    [SerializeField] private RectTransform LoadButtonRoot;
    [SerializeField] private RectTransform CloseButtonRoot;

    private const int PageSize = 50;
    private readonly List<(SaveCatalog.Entry entry, Button button)> rows = new();
    private readonly CancellationTokenSource lifetime = new();
    private List<SaveCatalog.Entry> entries;
    private SaveCatalog.Entry selected;
    private Button loadButton;
    private Button closeButton;
    private Button moreButton;
    private bool busy;
    private bool closed;

    protected override void Start()
    {
        base.Start();
        OnDestroyEvent.AddListener(() => { lifetime.Cancel(); lifetime.Dispose(); });
        loadButton = LoadButtonRoot.GetComponent<Button>();
        closeButton = CloseButtonRoot.GetComponent<Button>();
        loadButton.onClick.AddListener(() => LoadSelectedAsync().Forget());
        closeButton.onClick.AddListener(Close);
        loadButton.interactable = false;
        RefreshAsync().Forget();
    }

    private async UniTaskVoid RefreshAsync()
    {
        Button status = CreateRow("저장 목록을 불러오는 중...");
        status.interactable = false;
        try
        {
            entries = await SaveCatalog.ListAsync(Main.Save.SaveDirectory, Main.Save.LocalPlayerId, lifetime.Token);
            if (closed || this == null) return;
            Destroy(status.gameObject);
            if (entries.Count == 0)
            {
                CreateRow("저장된 월드가 없습니다.").interactable = false;
                return;
            }
            AddPage();
            Select(rows.Find(row => row.entry.CanLoad).entry);
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (closed || this == null) return;
            SetLabel(status, "저장 목록을 읽지 못했습니다.");
            SaveManager.ShowError(error.Message);
        }
    }

    private void AddPage()
    {
        if (moreButton != null) Destroy(moreButton.gameObject);
        int end = Math.Min(rows.Count + PageSize, entries.Count);
        for (int i = rows.Count; i < end; i++)
        {
            SaveCatalog.Entry entry = entries[i];
            Button row = CreateRow(Describe(entry));
            row.onClick.AddListener(() => Select(entry));
            rows.Add((entry, row));
        }
        if (rows.Count < entries.Count)
        {
            moreButton = CreateRow($"더 보기 ({rows.Count} / {entries.Count})");
            moreButton.onClick.AddListener(AddPage);
        }
    }

    private void Select(SaveCatalog.Entry entry)
    {
        if (busy) return;
        selected = entry;
        foreach (var row in rows) SetLabel(row.button, Describe(row.entry));
        loadButton.interactable = selected != null && selected.CanLoad;
        if (entry != null && !entry.CanLoad) SaveManager.ShowError(entry.Error);
    }

    private string Describe(SaveCatalog.Entry entry)
    {
        string name = entry.Name.Replace('\n', ' ').Replace('\r', ' ');
        if (name.Length > 24) name = name.Substring(0, 24) + "…";
        string seed = entry.Seed?.ToString() ?? "-";
        string id = string.IsNullOrEmpty(entry.WorldId) ? "-" : entry.WorldId.Substring(0, 8);
        string date = entry.SavedAtUtc == default ? "저장 시각 없음" : entry.SavedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        string state = !entry.CanLoad ? "불러올 수 없음" : entry.IsMultiplayer ? "멀티플레이" : "싱글플레이";
        if (entry.RecoveredBackup) state += " · 백업 복구";
        return (selected == entry ? "▶ " : "") + "Name: " + name +
            "\nSeed: " + seed + "   Id: " + id + "\n" + date + " · " + state;
    }

    private Button CreateRow(string label)
    {
        var row = Instantiate(SaveSlotTemplate, SaveListContent);
        ((RectTransform)row.transform).sizeDelta = new Vector2(670f, 96f);
        var button = row.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        var trigger = row.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (trigger != null) trigger.triggers.Clear();
        SetLabel(button, label);
        return button;
    }

    private static void SetLabel(Button button, string label)
    {
        foreach (var text in button.GetComponentsInChildren<UI_Text>(true)) text.LocaleName = ELocalizedName.NONE;
        foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
        {
            text.richText = false;
            text.enableAutoSizing = false;
            text.fontSize = 22f;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.text = label;
        }
    }

    private async UniTaskVoid LoadSelectedAsync()
    {
        if (busy || selected == null || !selected.CanLoad) return;
        SetBusy(true);
        bool prepared = false;
        try
        {
            if (!await Main.Save.PrepareLoadAsync(selected.Path, lifetime.Token))
            {
                SaveManager.ShowError(Main.Save.LastError ?? "저장 파일을 불러올 수 없습니다.");
                return;
            }
            prepared = true;
            if (Main.Save.PendingLoad.isMultiplayer)
            {
                var popup = await Extensions.ShowPopup<UI_Popup_MakeRoom>(clickGuard: true);
                if (popup == null) throw new InvalidOperationException("방 만들기 화면을 열지 못했습니다.");
                popup.SetContinueMode();
                busy = false;
                Close();
            }
            else await Main.Scene.ChangeSceneAsync("GameScene");
        }
        catch (Exception error)
        {
            if (prepared) Main.Save.EndSession();
            SaveManager.ShowError("불러오기 실패: " + error.Message);
        }
        finally { if (this != null && !closed) SetBusy(false); }
    }

    private void SetBusy(bool value)
    {
        busy = value;
        closeButton.interactable = !value;
        loadButton.interactable = !value && selected != null && selected.CanLoad;
        foreach (var row in rows) row.button.interactable = !value;
        if (moreButton != null) moreButton.interactable = !value;
    }

    public override void Close()
    {
        if (busy || closed) return;
        closed = true;
        lifetime.Cancel();
        base.Close();
    }
}
