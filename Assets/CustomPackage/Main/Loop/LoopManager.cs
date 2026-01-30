using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 게임의 Update들을 한 곳에서 관리해주며 게임의 속도를 조절해주는 매니저.
/// 게임 속도에 영향을 받는 Update들은 OnGameUpdate에 등록.
/// 게임 속도에 영향을 받는 DoTween은 GetGameSequence를 통해 시퀀스를 만들어 등록.
/// </summary>
public class LoopManager : CoreManager
{
    // 등록할 액션들은 매개변수가 실제로 필요없더라도 float deltaTime을 받도록 통일.
    public event Action<float> OnUpdate;
    public event Action<float> OnGameUpdate;
    private float GameSpeed { get; set; } = 1f;
    private List<Sequence> _listSequence = new ();

    public void Update(float deltaTime)
    {
        OnUpdate?.Invoke(deltaTime);
    }

    public void GameUpdate(float deltaTime)
    {
        if (GameScene.GameProcessing != GameProcessing.Processing) return;
        OnGameUpdate?.Invoke(deltaTime * GameSpeed);
    }

    public Sequence GetGameSequence()
    {
        Sequence seq = DOTween.Sequence();
        _listSequence.Add(seq);
        seq.OnKill(() => _listSequence.Remove(seq));
        return seq;
    }

    public void SetTimeScale(float timeScale)
    {
        GameSpeed = timeScale;
        foreach (Sequence seq in _listSequence)
        {
            seq.timeScale = timeScale;
        }
    }

    public void ResetGameEvent()
    {
        OnGameUpdate = null;
    }

    private float _lastTimeScale = 1f;
    private const float DEFAULT_FIXED_DELTA = 0.02f;

    private CancellationTokenSource _slowMotionCTS;

    public async UniTaskVoid DoSlowMotion(float targetScale, float duration)
    {
        CancelSlowMotion();
        _slowMotionCTS = new CancellationTokenSource();
        var token = _slowMotionCTS.Token;

        float startScale = GameSpeed;
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
        GameSpeed = timeScale;
        Time.timeScale = GameSpeed;

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
