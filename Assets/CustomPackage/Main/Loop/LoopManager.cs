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
    #region Fields

    // 게임 시퀀스 리스트
    private List<Sequence> _listSequence = new();

    // 슬로우 모션 취소 토큰
    private CancellationTokenSource _slowMotionCTS;

    // 슬로우 모션 이전 타임스케일
    private float _lastTimeScale = 1f;

    #endregion

    #region Constants

    // 기본 고정 델타 타임
    private const float DEFAULT_FIXED_DELTA = 0.02f;

    #endregion

    #region Properties

    // 현재 게임 속도
    private float GameSpeed { get; set; } = 1f;

    #endregion

    #region Events
    // 등록할 액션들은 매개변수가 실제로 필요없더라도 float deltaTime을 받도록 통일.

    // 매 프레임 호출되는 이벤트
    public event Action<float> OnUpdate;

    // 게임 속도가 적용된 업데이트 이벤트
    public event Action<float> OnGameUpdate;

    #endregion

    #region Update

    /// <summary>
    /// 매 프레임 업데이트를 실행합니다.
    /// </summary>
    public void Update(float deltaTime)
    {
        OnUpdate?.Invoke(deltaTime);
    }

    /// <summary>
    /// 게임 속도가 적용된 업데이트를 실행합니다.
    /// </summary>
    public void GameUpdate(float deltaTime)
    {
        if (GameScene.GameProcessing != GameProcessing.Processing) return;
        OnGameUpdate?.Invoke(deltaTime * GameSpeed);
    }

    #endregion

    #region Time Control

    /// <summary>
    /// 게임 속도에 맞춰 동작하는 시퀀스를 생성합니다.
    /// </summary>
    public Sequence GetGameSequence()
    {
        Sequence seq = DOTween.Sequence();
        _listSequence.Add(seq);
        seq.OnKill(() => _listSequence.Remove(seq));
        return seq;
    }

    /// <summary>
    /// 게임 속도를 설정합니다.
    /// </summary>
    public void SetTimeScale(float timeScale)
    {
        GameSpeed = timeScale;
        foreach (Sequence seq in _listSequence)
        {
            seq.timeScale = timeScale;
        }
    }

    /// <summary>
    /// 슬로우 모션 효과를 비동기로 적용합니다.
    /// </summary>
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
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _slowMotionCTS?.Dispose();
            _slowMotionCTS = null;
        }
    }

    #endregion

    #region Internal Methods

    // 타임스케일 업데이트
    private void UpdateTimeScale(float timeScale, bool fixedDeltaTime = false)
    {
        if (timeScale < 0f) timeScale = 0f;
        GameSpeed = timeScale;
        Time.timeScale = GameSpeed;

        if (fixedDeltaTime)
            Time.fixedDeltaTime = DEFAULT_FIXED_DELTA * Time.timeScale;
    }

    // 슬로우 모션 취소
    private void CancelSlowMotion()
    {
        if (_slowMotionCTS != null)
        {
            _slowMotionCTS.Cancel();
            _slowMotionCTS.Dispose();
            _slowMotionCTS = null;
        }
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// 게임 업데이트 이벤트를 초기화합니다.
    /// </summary>
    public void ResetGameEvent()
    {
        OnGameUpdate = null;
    }

    #endregion
}

