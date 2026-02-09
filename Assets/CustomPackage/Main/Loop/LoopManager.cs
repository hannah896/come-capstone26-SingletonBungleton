using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 게임의 Update들을 한 곳에서 관리해주며 게임의 속도를 조절해주는 매니저.
/// 일반 Update는 OnUpdate에 등록해서 사용.
/// 게임 속도에 영향을 받는 Update들은 OnGameUpdate에 등록.
/// 게임 속도에 영향을 받는 DoTween은 GetGameSequence를 통해 시퀀스를 만들어 등록.
/// 등록한 Update나 Sequence들은, OnDestroy나 OnDisable에서 연결 해제 및 DoKill()을 호출해줘야 함.
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

    #region Properties

    // 현재 게임 속도
    public float GameSpeed { get; private set; } = 1f;

    #endregion

    #region Events

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
    public void SetGameSpeed(float gameSpeed)
    {
        GameSpeed = gameSpeed;
        foreach (Sequence seq in _listSequence)
        {
            seq.timeScale = gameSpeed;
        }
    }

    /// <summary>
    /// 타겟 스피드를 향해 점진적으로 스피드가 적용합니다.
    /// </summary>
    public async UniTaskVoid DoFadeGameSpeed(float targetSpeed, float duration)
    {
        CancelSlowMotion();
        _slowMotionCTS = new CancellationTokenSource();
        var token = _slowMotionCTS.Token;

        float currentSpeed = GameSpeed;
        float elapsed = 0f;

        try
        {
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float nextSpeed = Mathf.Lerp(currentSpeed, targetSpeed, elapsed / duration);

                SetGameSpeed(nextSpeed);

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            SetGameSpeed(targetSpeed);
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