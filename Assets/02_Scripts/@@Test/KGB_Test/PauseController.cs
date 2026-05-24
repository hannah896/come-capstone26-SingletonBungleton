using UnityEngine;

public class PauseController : MonoBehaviour
{
    public bool IsPaused { get; private set; }

    private float _cachedGameSpeed = 1f;
    private GameProcessing _cachedGameProcessing = GameProcessing.None;

    private void OnEnable()
    {
        GameEvents.OnGamePause += HandleGamePause;
        GameEvents.OnGameResume += HandleGameResume;
    }

    private void OnDisable()
    {
        GameEvents.OnGamePause -= HandleGamePause;
        GameEvents.OnGameResume -= HandleGameResume;
    }

    public void HandlePauseInput()
    {
        Toggle();
    }

    public void Toggle()
    {
        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (IsPaused) return;

        _cachedGameSpeed = Main.Loop.GameSpeed;
        _cachedGameProcessing = GameScene.GameProcessing;

        Main.Loop.SetGameSpeed(0f);
        GameScene.GameProcessing = GameProcessing.Stopping;

        GameEvents.OnGamePause?.Invoke();
    }

    public void Resume()
    {
        if (!IsPaused) return;

        Main.Loop.SetGameSpeed(_cachedGameSpeed <= 0f ? 1f : _cachedGameSpeed);
        GameScene.GameProcessing = _cachedGameProcessing;

        GameEvents.OnGameResume?.Invoke();
    }

    private void HandleGamePause() => IsPaused = true;

    private void HandleGameResume() => IsPaused = false;
}
