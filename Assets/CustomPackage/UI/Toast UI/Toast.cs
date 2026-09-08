using Cysharp.Threading.Tasks;
using UnityEngine;

/* -------------------------------
   Created by : Hamza Herbou
   hamza95herbou@gmail.com
---------------------------------- */

public enum ToastColor
{
    Black,
    Red,
    Purple,
    Magenta,
    Blue,
    Green,
    Yellow,
    Orange
}

public enum ToastPosition
{
    TopLeft,
    TopCenter,
    TopRight,
    MiddleLeft,
    MiddleCenter,
    MiddleRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}

public static class Toast
{
    public static bool isLoaded = false;

    private static ToastUI toastUI;

    // 프리팹 로드가 진행 중인지 (동시에 여러 번 Show를 불러도 인스턴스가 하나만 생기게 한다)
    private static bool isPreparing;

    #region Prepare

    /// <summary>
    /// 토스트 UI 인스턴스를 확보한다.
    ///
    /// Addressable 로드는 비동기라서, 첫 호출은 반드시 await로 기다려야 한다.
    /// (이전 구현은 async void로 로드를 던져놓고 곧바로 인스턴스를 참조해서 첫 호출이 항상 NRE였다)
    ///
    /// 씬 전환으로 인스턴스가 파괴되면 toastUI가 Unity의 가짜 null이 되므로 다음 호출에서 다시 만든다.
    /// </summary>
    private static async UniTask<ToastUI> PrepareAsync()
    {
        if (toastUI != null) return toastUI;

        // 이미 다른 호출이 로드 중이면 그 결과를 기다린다
        if (isPreparing)
        {
            await UniTask.WaitUntil(() => !isPreparing);
            return toastUI;
        }

        if (Main.Resource == null) return null;

        isPreparing = true;
        try
        {
            var prefab = await Main.Resource.LoadAssetAsync<ToastUI>("ToastUI");
            if (prefab == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[Toast] ToastUI 프리팹을 로드하지 못했습니다. Addressable 키를 확인하세요.");
#endif
                return null;
            }

            toastUI = Object.Instantiate(prefab);
            toastUI.gameObject.name = "[TOAST UI]";
            isLoaded = true;
            return toastUI;
        }
        finally
        {
            isPreparing = false;
        }
    }

    // ToastColor(팔레트 인덱스) 버전
    private static async UniTaskVoid ShowAsync(string text, float duration, ToastColor color, ToastPosition position)
    {
        ToastUI ui = await PrepareAsync();
        if (ui == null) return;

        ui.Init(text, duration, color, position);
    }

    // Color(직접 지정) 버전
    private static async UniTaskVoid ShowAsync(string text, float duration, Color color, ToastPosition position)
    {
        ToastUI ui = await PrepareAsync();
        if (ui == null) return;

        ui.Init(text, duration, color, position);
    }

    #endregion

    #region Show

    public static void Show(string text)
    {
        ShowAsync(text, 2F, ToastColor.Black, ToastPosition.MiddleCenter).Forget();
    }

    public static void Show(string text, float duration)
    {
        ShowAsync(text, duration, ToastColor.Black, ToastPosition.BottomCenter).Forget();
    }

    public static void Show(string text, float duration, ToastPosition position)
    {
        ShowAsync(text, duration, ToastColor.Black, position).Forget();
    }

    public static void Show(string text, ToastColor color)
    {
        ShowAsync(text, 2F, color, ToastPosition.BottomCenter).Forget();
    }

    public static void Show(string text, ToastColor color, ToastPosition position)
    {
        ShowAsync(text, 2F, color, position).Forget();
    }

    public static void Show(string text, Color color)
    {
        ShowAsync(text, 2F, color, ToastPosition.BottomCenter).Forget();
    }

    public static void Show(string text, Color color, ToastPosition position)
    {
        ShowAsync(text, 2F, color, position).Forget();
    }

    public static void Show(string text, float duration, ToastColor color)
    {
        ShowAsync(text, duration, color, ToastPosition.BottomCenter).Forget();
    }

    public static void Show(string text, float duration, ToastColor color, ToastPosition position)
    {
        ShowAsync(text, duration, color, position).Forget();
    }

    public static void Show(string text, float duration, Color color)
    {
        ShowAsync(text, duration, color, ToastPosition.BottomCenter).Forget();
    }

    public static void Show(string text, float duration, Color color, ToastPosition position)
    {
        ShowAsync(text, duration, color, position).Forget();
    }

    #endregion

    public static void Dismiss()
    {
        if (toastUI != null)
            toastUI.Dismiss();
    }
}
