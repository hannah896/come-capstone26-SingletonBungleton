using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>저장 슬롯 삭제 여부만 확인한다. 파일 삭제는 호출한 목록에서 처리한다.</summary>
public class UI_Popup_ConfirmDelete : UI_Popup
{
    [SerializeField] private UI_Button ConfirmButton;
    [SerializeField] private UI_Button CancelButton;
    [SerializeField] private UI_Button CloseButton;
    [SerializeField] private TMP_Text MessageText;

    private UniTaskCompletionSource<bool> completion;
    private bool closed;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;
        ConfirmButton.GetComponent<Button>().onClick.AddListener(() => Complete(true));
        CancelButton.GetComponent<Button>().onClick.AddListener(Close);
        CloseButton.GetComponent<Button>().onClick.AddListener(Close);
        OnDestroyEvent.AddListener(() => completion?.TrySetResult(false));
        return true;
    }

    public UniTask<bool> ConfirmAsync(string worldName, CancellationToken token)
    {
        if (closed) return UniTask.FromResult(false);
        if (completion != null) throw new InvalidOperationException("이미 삭제 확인 중입니다.");
        completion = new UniTaskCompletionSource<bool>();
        string name = string.IsNullOrWhiteSpace(worldName) ? "이름 없음" : worldName.Replace('\n', ' ').Replace('\r', ' ');
        if (name.Length > 18) name = name.Substring(0, 18) + "…";
        var localizedText = MessageText.GetComponent<UI_Text>();
        if (localizedText != null) localizedText.LocaleName = ELocalizedName.NONE;
        MessageText.richText = false;
        MessageText.text = $"\"{name}\"\n정말 삭제하시겠습니까?\n삭제한 저장은 복구할 수 없습니다.";
        return completion.Task.AttachExternalCancellation(token);
    }

    private void Complete(bool confirmed)
    {
        if (closed) return;
        closed = true;
        ConfirmButton.GetComponent<Button>().interactable = false;
        CancelButton.GetComponent<Button>().interactable = false;
        CloseButton.GetComponent<Button>().interactable = false;
        base.Close();
        completion?.TrySetResult(confirmed);
    }

    public override void Close() => Complete(false);
}
