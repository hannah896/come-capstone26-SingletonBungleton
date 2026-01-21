using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

public class TimeManager
{
    public float TimeScale { get; private set; } = 1f;
    public bool IsPaused { get; private set; } = false;

    private float _lastTimeScale = 1f;
    private const float DEFAULT_FIXED_DELTA = 0.02f;

    private CancellationTokenSource _slowMotionCTS;

    public void Clear()
    {
        CancelSlowMotion();
        SetTimeScale(1f);
        IsPaused = false;
    }

    public void SetTimeScale(float timeScale, bool fixedDeltaTime = false)
    {
        CancelSlowMotion();
        UpdateTimeScale(timeScale, fixedDeltaTime);
    }

    public void SetPause(bool isPause)
    {
        if (IsPaused == isPause) return;

        IsPaused = isPause;
        CancelSlowMotion();

        if (IsPaused)
        {
            _lastTimeScale = TimeScale;
            UpdateTimeScale(0f);
        }
        else
        {
            UpdateTimeScale(_lastTimeScale);
        }
    }

    public async UniTaskVoid DoSlowMotion(float targetScale, float duration)
    {
        CancelSlowMotion();
        _slowMotionCTS = new CancellationTokenSource();
        var token = _slowMotionCTS.Token;

        float startScale = TimeScale;
        float elapsed = 0f;

        try
        {
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float nextScale = Mathf.Lerp(startScale, targetScale, elapsed / duration);

                UpdateTimeScale(nextScale);

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            UpdateTimeScale(targetScale);
        }
        catch (System.OperationCanceledException)
        {
        }
        finally
        {
            _slowMotionCTS?.Dispose();
            _slowMotionCTS = null;
        }
    }

    private void UpdateTimeScale(float timeScale, bool fixedDeltaTime = false)
    {
        if (timeScale < 0f) timeScale = 0f;
        TimeScale = timeScale;
        Time.timeScale = TimeScale;

        if (fixedDeltaTime)
            Time.fixedDeltaTime = DEFAULT_FIXED_DELTA * Time.timeScale;
    }

    private void CancelSlowMotion()
    {
        if (_slowMotionCTS != null)
        {
            _slowMotionCTS.Cancel();
            _slowMotionCTS.Dispose();
            _slowMotionCTS = null;
        }
    }
}