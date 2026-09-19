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

    /// <summary>
    /// OnGameUpdate(게임 로직)를 돌려야 하는 상태인지.
    /// 정식 진행(Processing) 또는 테스트 씬(Testing)일 때 true.
    /// </summary>
    public static bool IsGameUpdating =>
        (GameProcessing == GameProcessing.Processing || GameProcessing == GameProcessing.Testing) &&
        !(Main.Save?.IsRestoring ?? false) && !(Main.Save?.IsCapturing ?? false);

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
        try { await StartGame(); }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            Debug.LogError($"[GameScene] 게임 준비 실패: {e}");
            GameProcessing = GameProcessing.Stopping;
            RecoverFailedLoadAsync(e.Message).Forget();
        }
    }

    private static async UniTaskVoid RecoverFailedLoadAsync(string message)
    {
        if (Main.Network != null && Main.Network.IsInRoom) await Main.Network.LeaveRoomAsync();
        Main.Save.EndSession();
        await UniTask.WaitUntil(() => !Main.Scene.IsTransitioning);
        await Main.Scene.ChangeSceneAsync("LobbyScene");
        SaveManager.ShowError("불러오기를 완료하지 못했습니다. " + message);
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
        Main.Save.BeginSceneLoad();
        GameProcessing = GameProcessing.Stopping;
        await NetworkSaveCoordinator.PrepareClientRestoreAsync(token);

        // #1. 크래프팅 시스템 선행 생성 (플레이어 Bind보다 먼저 존재해야 함)
        // allRecipes는 Resources/CraftingManager 프리팹에 미리 구워둔 값을 쓴다 (AssetDatabase는 빌드에서 동작하지 않음).
        if (CraftingManager.Instance == null)
        {
            GameObject prefab = Resources.Load<GameObject>(nameof(CraftingManager));
            if (prefab != null)
                UnityEngine.Object.Instantiate(prefab);
            else
                new GameObject(nameof(CraftingManager)).AddComponent<CraftingManager>();
        }

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

        // 복원 시에도 동일한 생성 파이프라인을 사용하되, 청크 배치 전에 변경분을 적용한다.
        GameState = GameState.Player;
        if (Main.Save.PendingLoad != null)
        {
            WorldSaveData saved = Main.Save.PendingLoad.world;
            WorldGenRequest.Set(saved.branch, saved.loop, saved.seed, saved.size,
                fromHost: Main.Network != null && Main.Network.IsInRoom);
            WorldGenRequest.Consume();
            await WorldGenManager.Instance.GenerateWorld(saved.branch, saved.loop, saved.seed, saved.size, token);
        }
        else
        {
            // #3. 플레이어 생성 단계 — 맵 생성 (WorldGen 내부에서 플레이어 소환까지 완료)
            GameState = GameState.Player;

            bool isNetworkSession = Main.Network != null && Main.Network.IsInRoom;

            if (isNetworkSession)
            {
                // 멀티 세션에서는 "호스트가 정한 옵션"만 쓴다.
                // 아직 안 왔으면(방 참가 직후 세션 속성을 놓친 경우) 도착할 때까지 기다린다.
                // 로컬에 남아 있던 이전 요청(싱글 월드 생성 등)을 쓰면 각자 다른 월드가 만들어진다.
                if (!WorldGenRequest.HasHostRequest)
                    await NetworkWorldConfig.WaitAndApplyAsync(token: token);

                if (WorldGenRequest.HasHostRequest)
                {
                    WorldGenRequest.Data req = WorldGenRequest.Consume();
                    await WorldGenManager.Instance.GenerateWorld(
                        req.Branch, req.Loop, req.Seed, req.Size, token);
                }
                else if (WorldGenRequest.HasData && WorldGenRequest.IsFromHost)
                {
                    // 요청은 이미 소비됐지만 이번 세션에서 호스트가 준 옵션이 있다 (씬 리로드 등) → 같은 시드로 재생성
                    WorldGenRequest.Data req = WorldGenRequest.Peek();
                    await WorldGenManager.Instance.GenerateWorld(
                        req.Branch, req.Loop, req.Seed, req.Size, token);
                }
                else
                {
                    throw new InvalidOperationException("호스트의 월드 생성 정보를 받지 못했습니다.");
                }
            }
            else if (WorldGenRequest.HasRequest)
            {
                // 싱글: 로비(UI_Popup_WorldGen)에서 확정한 옵션·시드로 생성
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

        Player localPlayer = FindLocalPlayer();
        if (localPlayer == null) throw new InvalidOperationException("플레이어를 생성하지 못했습니다.");
        await PlayerSaveAdapter.WaitUntilReadyAsync(localPlayer, token);
        await Main.UI.ShowHudOverlay<UI_Hud_WorldState>("UI_Hud_WorldState");
        await NetworkSaveCoordinator.RestoreLocalPlayerAsync(localPlayer, token);
        Main.Save.CompleteLoad();

        // 모든 복원과 초기화가 끝난 뒤 입력·시간·시뮬레이션을 시작한다.
        GameProcessing = localPlayer.IsDead ? GameProcessing.Stopping : GameProcessing.Processing;
        GameState = localPlayer.IsDead ? GameState.Failed : GameState.Playing;

        // 멀티 세션이면 다른 플레이어의 접속을 토스트로 알린다.
        // (로딩 중에 토스트가 뜨지 않도록 게임 진행이 시작된 뒤에 만든다.
        //  이미 들어와 있던 사람은 알리지 않고, 이후 새로 들어오는 사람만 알린다)
        if (Main.Network != null && Main.Network.IsInRoom)
        {
            new GameObject(nameof(NetworkPlayerJoinNotice)).AddComponent<NetworkPlayerJoinNotice>();
        }

        // TODO: 소환된 플레이어가 자신의 HUD(UI_Hud_Game)를 띄우는 단계 (다음 작업)
    }

    /// <summary>게임 오버 처리. (UI_Editor 및 상태 전환에서 호출)</summary>
    public void FailGame()
    {
        // TODO: 게임 오버 연출 및 후처리
    }

    private static Player FindLocalPlayer()
    {
        foreach (var player in UnityEngine.Object.FindObjectsByType<Player>(FindObjectsSortMode.None))
            if (player.IsLocalPlayer) return player;
        return null;
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
    Testing = 1 << 2,   // 테스트 씬 전용 — 로비/월드 생성 없이도 게임 로직(OnGameUpdate)을 돌린다
}
