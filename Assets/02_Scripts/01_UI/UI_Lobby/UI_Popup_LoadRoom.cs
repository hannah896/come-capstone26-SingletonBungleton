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
    [SerializeField] private Sprite SlotInactiveSprite;
    [SerializeField] private Sprite SlotActiveSprite;

    private const int PageSize = 50;
    private readonly List<(SaveCatalog.Entry entry, Button button, Button deleteButton)> rows = new();
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

    private async UniTask RefreshAsync(string preferredPath = null, int visibleCount = PageSize)
    {
        SetBusy(true);
        foreach (Transform child in SaveListContent)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        rows.Clear();
        moreButton = null;
        selected = null;
        Button status = CreateRow("저장 목록을 불러오는 중...");
        status.interactable = false;
        try
        {
            entries = await SaveCatalog.ListAsync(Main.Save.SaveDirectory, Main.Save.LocalPlayerId, lifetime.Token);
            if (closed || this == null) return;
            status.gameObject.SetActive(false);
            Destroy(status.gameObject);
            if (entries.Count == 0)
            {
                CreateRow("저장된 월드가 없습니다.").interactable = false;
                return;
            }
            do { AddPage(); }
            while (rows.Count < Math.Min(visibleCount, entries.Count));
            ApplySelection(rows.Find(row => row.entry.Path == preferredPath).entry ??
                rows.Find(row => row.entry.CanLoad).entry);
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (closed || this == null) return;
            SetLabel(status, "저장 목록을 읽지 못했습니다.");
            SaveManager.ShowError(error.Message);
        }
        finally { if (this != null && !closed) SetBusy(false); }
    }

    private void AddPage()
    {
        if (moreButton != null)
        {
            moreButton.gameObject.SetActive(false);
            Destroy(moreButton.gameObject);
            moreButton = null;
        }
        int end = Math.Min(rows.Count + PageSize, entries.Count);
        for (int i = rows.Count; i < end; i++)
        {
            SaveCatalog.Entry entry = entries[i];
            Button row = CreateRow(Describe(entry));
            row.onClick.AddListener(() => Select(entry));
            Button deleteButton = FindDeleteButton(row);
            if (deleteButton != null)
            {
                deleteButton.gameObject.SetActive(true);
                deleteButton.onClick.AddListener(() => DeleteEntryAsync(entry).Forget());
            }
            rows.Add((entry, row, deleteButton));
        }
        if (rows.Count < entries.Count)
        {
            moreButton = CreateRow($"더 보기 ({rows.Count} / {entries.Count})");
            moreButton.onClick.AddListener(() => { if (!busy) AddPage(); });
        }
    }

    private void Select(SaveCatalog.Entry entry)
    {
        if (busy || closed) return;
        ApplySelection(entry);
        if (entry != null && !entry.CanLoad) SaveManager.ShowError(entry.Error);
    }

    private void ApplySelection(SaveCatalog.Entry entry)
    {
        selected = entry;
        foreach (var row in rows)
        {
            SetLabel(row.button, Describe(row.entry));
            SetRowSelected(row.button, row.entry == selected);
        }
        loadButton.interactable = !busy && selected != null && selected.CanLoad;
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
        // 포커스가 Continue 버튼으로 옮겨져도 선택한 저장 슬롯의 테두리는 유지한다.
        button.transition = Selectable.Transition.None;
        button.onClick = new Button.ButtonClickedEvent();
        var trigger = row.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (trigger != null) trigger.triggers.Clear();
        // 안내/더 보기 행에는 삭제 버튼을 표시하지 않는다.
        Button deleteButton = FindDeleteButton(button);
        if (deleteButton != null)
        {
            deleteButton.onClick = new Button.ButtonClickedEvent();
            deleteButton.gameObject.SetActive(false);
        }
        SetLabel(button, label);
        SetRowSelected(button, false);
        row.SetActive(true);
        return button;
    }

    private static Button FindDeleteButton(Button row) =>
        row.transform.Find("UI_Button_Close")?.GetComponent<Button>();

    private async UniTaskVoid DeleteEntryAsync(SaveCatalog.Entry entry)
    {
        if (busy || closed || entry == null) return;
        SetBusy(true);
        UI_Popup_ConfirmDelete popup = null;
        bool attempted = false;
        string selectedPath = selected?.Path;
        int visibleCount = rows.Count;
        try
        {
            popup = await Extensions.ShowPopup<UI_Popup_ConfirmDelete>(clickGuard: true, token: lifetime.Token);
            lifetime.Token.ThrowIfCancellationRequested();
            if (popup == null) throw new InvalidOperationException("삭제 확인창을 열지 못했습니다.");
            if (!await popup.ConfirmAsync(entry.Name, lifetime.Token)) return;
            lifetime.Token.ThrowIfCancellationRequested();
            attempted = true;
            await SaveCatalog.DeleteAsync(Main.Save.SaveDirectory, entry.Path, lifetime.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (this != null && !closed) SaveManager.ShowError("삭제하지 못했습니다. " + error.Message);
        }
        finally
        {
            if (popup != null) popup.Close();
            if (this != null && !closed)
            {
                // 일부 파일만 지워진 오류 상황도 실제 디스크 상태로 다시 표시한다.
                if (attempted) await RefreshAsync(selectedPath, visibleCount);
                else SetBusy(false);
            }
        }
    }

    private void SetRowSelected(Button button, bool isSelected)
    {
        var image = button.targetGraphic as Image;
        if (image == null) return;
        image.overrideSprite = null;
        image.sprite = isSelected ? SlotActiveSprite : SlotInactiveSprite;
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
        foreach (var row in rows)
        {
            row.button.interactable = !value;
            if (row.deleteButton != null) row.deleteButton.interactable = !value;
        }
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
