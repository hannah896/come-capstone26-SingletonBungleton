using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

public class GameScene : SceneBase
{
    #region Properties

    // 게임 상태 — 상태 전환 시 해당 단계의 처리를 발생시키는 허브
    public static GameState GameState
    {
        get => _gameState;
        set
        {
            if (_gameState == value) return;
            _gameState = value;

            switch (_gameState)
            {
                case GameState.World:
                    // 맵 생성 단계 진입
                    (Main.Scene.Current as GameScene)?.OnGameWorldGenerate?.Invoke();
                    break;

                case GameState.Player:
                    // 플레이어 생성 단계 진입
                    (Main.Scene.Current as GameScene)?.OnGamePlayerGenerate?.Invoke();
                    break;

                case GameState.Failed:
                    // 게임 오버 (사망)
                    GameProcessing = GameProcessing.Stopping;
                    GameEvents.OnGameOver?.Invoke();
                    if (Main.IsEditorMode)
                        Main.Scene.Load("EditorScene");
                    else
                        (Main.Scene.Current as GameScene)?.FailGame();
                    break;
            }
        }
    }

    public static GameProcessing GameProcessing { get; set; }

    // 인게임 HUD (플레이어 소환 후 표시 예정)
    public UI_Hud_Game UIHud { get; private set; }

    #endregion

    #region Fields

    private static GameState _gameState = GameState.None;

    // 단계별 이벤트 (구독자가 단계 진입을 감지)
    public event Action OnGameWorldGenerate;   // 맵 생성 단계
    public event Action OnGamePlayerGenerate;  // 플레이어 생성 단계

    #endregion

    #region Scene Lifecycle

    public override async UniTask EnterScene(CancellationToken token)
    {
        await StartGame();
    }

    public override void ExitScene()
    {
        // 게임 루프 이벤트 정리
        Main.Loop.ResetGameEvent();
    }

    #endregion

    #region Game Flow

    /// <summary>
    /// 게임 시작 흐름. 맵 생성이 온전히 끝난 뒤 플레이어 소환까지 진행한다.
    /// (플레이어 소환과 StartRegion 배치는 WorldGen 내부에서 처리됨)
    /// </summary>
    public async UniTask StartGame()
    {
        CancellationToken token = Main.Scene.CurrentToken;

        // #1. 크래프팅 시스템 선행 생성 (플레이어 Bind보다 먼저 존재해야 함)
        if (CraftingManager.Instance == null)
            new GameObject(nameof(CraftingManager)).AddComponent<CraftingManager>();

        // CraftingUI를 UIManager HUD 캔버스에 로드 (시작 시 숨김)
        var craftingUI = await Main.UI.ShowHudOverlay<CraftingUI>("CraftingUI");
        if (craftingUI != null) craftingUI.gameObject.SetActive(false);

        // UIKeyHandler는 CraftingUI와 별도 오브젝트로 생성 (UI가 꺼져도 입력 감지 유지)
        new GameObject("UIKeyHandler").AddComponent<UIKeyHandler>();

        // #2. 맵 생성 단계 — WorldGen 매니저를 동적 생성 (씬에 미리 배치할 필요 없음)
        GameState = GameState.World;

        if (WorldGenManager.Instance == null)
        {
            // 디렉터는 Start()에서 자동 부착되고, WorldSettings는 Addressable로 self-init 됨
            new GameObject(nameof(WorldGenManager)).AddComponent<WorldGenManager>();
        }

        // WorldGenManager.Start()의 WorldSettings(Addressable) 로드 완료까지 대기
        await UniTask.WaitUntil(() => WorldGenManager.Instance != null, cancellationToken: token);
        await UniTask.WaitUntil(() => WorldGenManager.Instance.WorldSettings != null, cancellationToken: token);

        // #2. 맵 로드/생성
        //     - 저장된 월드 데이터가 있으면 로드
        //     - 없으면 새 맵을 자동 생성 (WorldGen 내부에서 맵 생성 + 플레이어 소환까지 완료)
        // TODO: 저장 시스템 구현 후 분기 조건 교체
        bool hasSavedWorld = false;

        if (hasSavedWorld)
        {
            // TODO: 저장된 월드 데이터 로드 후 플레이어 배치
        }
        else
        {
            // #3. 플레이어 생성 단계 — 맵 생성 (WorldGen 내부에서 플레이어 소환까지 완료)
            GameState = GameState.Player;

            if (WorldGenRequest.HasRequest)
            {
                // 로비(UI_Popup_WorldGen)에서 확정한 옵션·시드로 생성
                WorldGenRequest.Data req = WorldGenRequest.Consume();
                await WorldGenManager.Instance.GenerateWorld(
                    req.Branch, req.Loop, req.Seed, req.Size, token);
            }
            else
            {
                // 로비를 거치지 않고 직접 진입한 경우(에디터 테스트 등) 기본 옵션으로 새 맵 자동 생성
                await WorldGenManager.Instance.GenerateWorldFromUI(
                    WorldBranchSetting.Default,
                    WorldLoopSetting.Default);
            }
        }

        // 여기 도달 = 맵 생성 + 플레이어 소환 완료 → 게임 진행 시작
        GameProcessing = GameProcessing.Processing;
        GameState = GameState.Playing;

        // TODO: 소환된 플레이어가 자신의 HUD(UI_Hud_Game)를 띄우는 단계 (다음 작업)
    }

    /// <summary>게임 오버 처리. (UI_Editor 및 상태 전환에서 호출)</summary>
    public void FailGame()
    {
        // TODO: 게임 오버 연출 및 후처리
    }

    #endregion
}

public enum GameState
{
    None = -1,
    World = 0,   // 맵 생성중
    Player,      // 플레이어 생성중
    Check,       // 게임 시작 전 최종 초기화중
    Ready,       // 준비 완료
    Playing,     // 게임 진행중
    InTutorial,  // 튜토리얼 진행중
    Failed,      // 게임 오버 (사망)
}

public enum GameProcessing
{
    None = 0,
    Processing = 1 << 0,
    Stopping = 1 << 1,
}
