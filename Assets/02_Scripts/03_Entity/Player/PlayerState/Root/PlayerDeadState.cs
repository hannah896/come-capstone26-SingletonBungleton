using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 플레이어 사망 상태 (Root)
/// wasHit=true  → Hit 트리거 + Dead bool → CombatDeath01 (피격 사망)
/// wasHit=false → Dead bool만          → CombatDeath02 (자연사)
///
/// 진입 시 입력을 차단하고 게임 오버 허브(GameState.Failed)에 통지한 뒤,
/// 사망 연출이 끝나면 <see cref="UI_Popup_PlayerDie"/>를 띄운다.
/// </summary>
public class PlayerDeadState : PlayerRootStateBase
{
    // 사망 애니메이션을 보여준 뒤 팝업을 띄우기까지의 대기 시간(초)
    private const float PopupDelay = 2f;

    private readonly bool wasHit;

    private CancellationTokenSource _cts;
    private UI_Popup_PlayerDie _popup;

    // 부활 시 되돌릴 사망 직전의 게임 진행 상태
    private GameState _prevGameState;
    private GameProcessing _prevProcessing;
    private bool _hasNotifiedGameOver;

    public PlayerDeadState(PlayerRootStateMachine machine, bool wasHit = false) : base(machine)
    {
        this.wasHit = wasHit;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayDeadAnimation(wasHit);

        // 남은 입력이 사망 후로 새어 나가지 않도록 즉시 비운다.
        Input?.SuppressAllInputs();

        Debug.Log($"[State] Dead 진입 (wasHit={wasHit})");

        // 원격 플레이어의 사망 연출은 각 클라이언트가 자기 화면에서 처리한다.
        if (!Entity.IsLocalPlayer) return;

        // 소지품 전량 드롭 — 부활하면 빈손으로 시작한다.
        int droppedCount = Entity.Inventory != null ? Entity.Inventory.DropAll() : 0;
        if (droppedCount > 0)
            Debug.Log($"[State] Dead — 소지품 {droppedCount}스택 드롭");

        _prevGameState = GameScene.GameState;
        _prevProcessing = GameScene.GameProcessing;
        _hasNotifiedGameOver = true;

        // 게임 오버 허브에 통지 — GameProcessing이 Stopping이 되고 GameEvents.OnGameOver가 발행된다.
        GameScene.GameState = GameState.Failed;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(Entity.GetCancellationTokenOnDestroy());
        ShowDiePopupAsync(_cts.Token).Forget();
    }

    public override void OnExit()
    {
        base.OnExit();

        // 팝업 로드가 진행 중이면 취소한다 (부활·씬 전환 경로).
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_popup != null)
        {
            _popup.Close();
            _popup = null;
        }

        // 부활로 빠져나가는 경로 — 사망 직전의 진행 상태로 되돌린다.
        if (_hasNotifiedGameOver && GameScene.GameState == GameState.Failed)
        {
            GameScene.GameState = _prevGameState;
            GameScene.GameProcessing = _prevProcessing;
        }
        _hasNotifiedGameOver = false;

        Debug.Log("[State] Dead 퇴장");
    }

    public override void Update(float time = 1)
    {
        // 사망 상태에서는 아무것도 하지 않는다.
        // 부활은 UI_Popup_PlayerDie → Player.Revive()가 Locomotion으로 전환시킨다.
    }

    /// <summary>
    /// 사망 연출을 보여준 뒤 게임 오버 팝업을 띄운다.
    /// </summary>
    private async UniTaskVoid ShowDiePopupAsync(CancellationToken token)
    {
        try
        {
            // 게임이 정지(Stopping)된 뒤이므로 timeScale의 영향을 받지 않는 실시간으로 대기한다.
            await UniTask.Delay(TimeSpan.FromSeconds(PopupDelay),
                                DelayType.UnscaledDeltaTime,
                                cancellationToken: token);

            var popup = await Extensions.ShowPopup<UI_Popup_PlayerDie>(
                clickGuard: true,
                clickGuardAlpha: 0.8f,
                token: token);

            if (popup == null)
            {
                Debug.LogWarning("[State] Dead — UI_Popup_PlayerDie 로드 실패 (Addressable 키 확인 필요)");
                return;
            }

            // 대기 중 부활·씬 전환으로 상태를 빠져나갔다면 방금 뜬 팝업을 정리한다.
            if (token.IsCancellationRequested)
            {
                popup.Close();
                return;
            }

            _popup = popup;
            _popup.Set(Entity, wasHit);
        }
        catch (OperationCanceledException)
        {
            // 부활 또는 씬 전환으로 취소되는 정상 경로다.
        }
    }
}
