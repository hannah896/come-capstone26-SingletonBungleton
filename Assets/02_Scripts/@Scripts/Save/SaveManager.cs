using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>월드 저장 수명과 씬 전환 전에 완료해야 하는 작업을 관리한다.</summary>
public class SaveManager : CoreManager
{
    private const string PlayerIdKey = "Save_PlayerId";
    private readonly Dictionary<string, PlayerSaveData> rememberedPlayers = new(StringComparer.Ordinal);
    private string worldId;
    private string worldName;
    private bool captureBlocked;
    private bool exitInProgress;
    private bool sessionReady;
    private bool networkSession;
    private bool allowQuit;
    private float nextAutoSave;
    private bool recoveredBackup;

    public string LocalPlayerId { get; private set; }
    public string CurrentWorldId => worldId;
    public string SavePath { get; private set; }
    public string SaveDirectory { get; private set; }
    public GameSaveData PendingLoad { get; private set; }
    private bool restoring;
    public bool IsRestoring
    {
        get => restoring;
        private set { restoring = value; SavePlayClock.SetPaused(restoring || captureBlocked); }
    }
    public bool IsSaving { get; private set; }
    public bool IsCapturing => captureBlocked;
    public bool HasSave => !string.IsNullOrEmpty(SaveDirectory) && SaveCatalog.EnumeratePaths(SaveDirectory).Count > 0;
    public bool CanSave => sessionReady && !IsRestoring && WorldGenManager.Instance != null &&
        (Main.Network == null || !Main.Network.IsInRoom || Main.Network.IsHost);
    public string LastError { get; private set; }

    protected override UniTask OnInitializeAsync()
    {
        LocalPlayerId = PlayerPrefs.GetString(PlayerIdKey, "");
        if (!Guid.TryParse(LocalPlayerId, out _))
        {
            LocalPlayerId = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(PlayerIdKey, LocalPlayerId);
            PlayerPrefs.Save();
        }
        SaveDirectory = Path.Combine(Application.persistentDataPath, "Saves");
        SavePath = Path.Combine(SaveDirectory, "slot_0.json");
        Application.wantsToQuit += OnWantsToQuit;
        return UniTask.CompletedTask;
    }

    // Main.Clear는 씬 진입 전에도 호출되므로 PendingLoad와 슬롯 선택을 지우지 않는다.
    public override void Clear() { }

    public void BeginNewGame()
    {
        PendingLoad = null;
        IsRestoring = false;
        sessionReady = false;
        networkSession = false;
        recoveredBackup = false;
        worldId = Guid.NewGuid().ToString("N");
        worldName = null;
        SavePath = SaveCatalog.GetWorldPath(SaveDirectory, worldId);
        rememberedPlayers.Clear();
    }

    public void SetWorldName(string name) => worldName = name?.Trim();

    public void BeginSceneLoad()
    {
        sessionReady = false;
        networkSession = Main.Network != null && Main.Network.IsInRoom;
        IsRestoring = true;
        if (string.IsNullOrEmpty(worldId))
        {
            worldId = Guid.NewGuid().ToString("N");
            SavePath = SaveCatalog.GetWorldPath(SaveDirectory, worldId);
        }
    }

    public void SetRemoteLoad(GameSaveData data)
    {
        SaveFileStore.Validate(data);
        PendingLoad = data;
        worldId = data.worldId;
        worldName = data.worldName;
        IsRestoring = true;
        rememberedPlayers.Clear();
        foreach (var player in data.players) RememberPlayer(player);
    }

    public PlayerSaveData FindPlayer(string playerId)
    {
        if (string.IsNullOrEmpty(playerId)) return null;
        if (rememberedPlayers.TryGetValue(playerId, out var data)) return data;
        return PendingLoad?.players?.Find(p => p.playerId == playerId);
    }

    public PlayerSaveData GetLocalPlayerSave() => FindPlayer(LocalPlayerId);
    public void RememberPlayer(PlayerSaveData data)
    {
        if (data != null && !string.IsNullOrEmpty(data.playerId)) rememberedPlayers[data.playerId] = data;
    }
    public void SetCaptureBlocked(bool value)
    {
        captureBlocked = value;
        SavePlayClock.SetPaused(IsRestoring || captureBlocked);
    }

    public void CompleteLoad()
    {
        PendingLoad = null;
        IsRestoring = false;
        sessionReady = true;
        nextAutoSave = Time.realtimeSinceStartup + 120f;
    }

    public void EndSession()
    {
        sessionReady = false;
        networkSession = false;
        PendingLoad = null;
        IsRestoring = false;
        SetCaptureBlocked(false);
        worldId = null;
        worldName = null;
        rememberedPlayers.Clear();
    }

    public void OnNetworkDisconnected()
    {
        // 연결이 사라진 월드를 싱글플레이로 오인하여 자동 저장하거나 종료를 막지 않는다.
        if (networkSession) EndSession();
    }

    public UniTask<bool> PrepareContinueAsync(CancellationToken token = default) => PrepareLoadAsync(SavePath, token);

    public async UniTask<bool> PrepareLoadAsync(string path, CancellationToken token = default)
    {
        if (IsSaving || IsRestoring) return false;
        try
        {
            token.ThrowIfCancellationRequested();
            SaveCatalog.LoadedSave loaded = await SaveCatalog.ReadAsync(path, token);
            GameSaveData data = loaded.Data;
            token.ThrowIfCancellationRequested();
            if (!data.players.Exists(p => p.playerId == LocalPlayerId))
                throw new InvalidDataException("이 기기의 플레이어 정보가 저장 파일에 없습니다.");
            SetRemoteLoad(data);
            // 기존 slot_0.json을 선택했다면 그 파일을 계속 갱신한다.
            SavePath = path;
            recoveredBackup = loaded.RecoveredBackup;
            Main.Network?.SetLocalCharacter(GetLocalPlayerSave().characterIndex);
            LastError = null;
            if (recoveredBackup) Toast.Show("이전 백업에서 저장 데이터를 복구했습니다.", 3f, ToastColor.Yellow, ToastPosition.TopCenter);
            return true;
        }
        catch (Exception e)
        {
            LastError = e.Message;
            IsRestoring = false;
            PendingLoad = null;
            Debug.LogWarning($"[Save] 불러오기 실패: {e.Message}");
            return false;
        }
    }

    public async UniTask<bool> SaveAsync(bool notify = true, CancellationToken token = default)
    {
        if (IsSaving) return false;
        if (!CanSave)
        {
            LastError = "월드가 준비된 뒤 방장만 저장할 수 있습니다.";
            if (notify) ShowError(LastError);
            return false;
        }
        IsSaving = true;
        try
        {
            SetCaptureBlocked(true);
            List<PlayerSaveData> players = await NetworkSaveCoordinator.CapturePlayersAsync(token);
            foreach (var player in players) RememberPlayer(player);
            var data = new GameSaveData
            {
                worldId = worldId, worldName = worldName, savedAtUtc = DateTime.UtcNow.ToString("O"),
                isMultiplayer = Main.Network != null && Main.Network.IsInRoom,
                world = WorldSaveAdapter.Capture(), players = new List<PlayerSaveData>(rememberedPlayers.Values)
            };
            string json = SaveFileStore.Serialize(data);
            // 백업으로 복구한 뒤 첫 저장에서는 손상된 원본이 정상 백업을 덮지 않게 한다.
            bool preserveBackup = recoveredBackup;
            await Task.Run(() => SaveFileStore.Write(SavePath, json, preserveBackup), token);
            recoveredBackup = false;
            LastError = null;
            if (notify) Toast.Show("저장했습니다.", 2f, ToastColor.Yellow, ToastPosition.TopCenter);
            return true;
        }
        catch (Exception e)
        {
            LastError = e.Message;
            Debug.LogError($"[Save] 저장 실패: {e}");
            if (notify) ShowError("저장하지 못했습니다. " + e.Message);
            return false;
        }
        finally
        {
            NetworkSaveCoordinator.EndCapture();
            SetCaptureBlocked(false);
            IsSaving = false;
            nextAutoSave = Time.realtimeSinceStartup + 120f;
        }
    }

    public void Tick()
    {
        if (CanSave && !IsSaving && !exitInProgress && Time.realtimeSinceStartup >= nextAutoSave)
            SaveAsync(false).Forget();
    }

    public async UniTask<bool> SaveAndExitAsync(bool quit)
    {
        if (exitInProgress) return false;
        exitInProgress = true;
        try
        {
            await UniTask.WaitUntil(() => !IsSaving);
            if (CanSave && !await SaveAsync()) return false;
            if (Main.Network != null && Main.Network.IsInRoom)
                await Main.Network.LeaveRoomAsync();
            EndSession();
            GameScene.GameState = GameState.None;
            GameScene.GameProcessing = GameProcessing.None;
            if (quit)
            {
                allowQuit = true;
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
            else await Main.Scene.ChangeSceneAsync("LobbyScene");
            return true;
        }
        catch (Exception e)
        {
            ShowError("나가기를 완료하지 못했습니다. " + e.Message);
            return false;
        }
        finally { exitInProgress = false; }
    }

    private bool OnWantsToQuit()
    {
        if (allowQuit || !sessionReady) return true;
        SaveAndExitAsync(true).Forget();
        return false;
    }

    public static void ShowError(string message) => Toast.Show(message, 4f, ToastColor.Yellow, ToastPosition.TopCenter);
}
