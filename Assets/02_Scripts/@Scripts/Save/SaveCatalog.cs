using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

/// <summary>월드별 파일을 관리한다. 파일 개수 제한이나 오래된 월드 자동 삭제는 하지 않는다.</summary>
public static class SaveCatalog
{
    public sealed class Entry
    {
        public string Path;
        public string Name;
        public int? Seed;
        public string WorldId;
        public DateTime SavedAtUtc;
        public bool IsMultiplayer;
        public bool RecoveredBackup;
        public string Error;
        public bool CanLoad => string.IsNullOrEmpty(Error);
    }

    public sealed class LoadedSave
    {
        public GameSaveData Data;
        public bool RecoveredBackup;
    }

    public static string GetWorldPath(string directory, string worldId) =>
        System.IO.Path.Combine(directory, "world_" + Guid.Parse(worldId).ToString("N") + ".json");

    public static List<string> EnumeratePaths(string directory)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(directory)) return new List<string>();
        foreach (string path in Directory.EnumerateFiles(directory, "*.json")) paths.Add(path);
        // 원본이 없어도 백업만으로 복구할 수 있으며 원본과 백업을 별도 슬롯으로 표시하지 않는다.
        foreach (string path in Directory.EnumerateFiles(directory, "*.json.bak")) paths.Add(path.Substring(0, path.Length - 4));
        return new List<string>(paths);
    }

    public static async UniTask<LoadedSave> ReadAsync(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            string json = await Task.Run(() => SaveFileStore.ReadText(path), token);
            token.ThrowIfCancellationRequested();
            return new LoadedSave { Data = SaveFileStore.Deserialize(json) };
        }
        catch (Exception error) when (!(error is OperationCanceledException))
        {
            if (!File.Exists(path + ".bak")) throw;
            string json = await Task.Run(() => SaveFileStore.ReadText(path + ".bak"), token);
            token.ThrowIfCancellationRequested();
            return new LoadedSave { Data = SaveFileStore.Deserialize(json), RecoveredBackup = true };
        }
    }

    public static async UniTask<List<Entry>> ListAsync(string directory, string playerId, CancellationToken token)
    {
        var entries = new List<Entry>();
        var paths = await Task.Run(() => EnumeratePaths(directory), token);
        foreach (string path in paths)
        {
            token.ThrowIfCancellationRequested();
            var entry = new Entry { Path = path, Name = System.IO.Path.GetFileNameWithoutExtension(path) };
            try
            {
                LoadedSave loaded = await ReadAsync(path, token);
                GameSaveData data = loaded.Data;
                entry.Name = string.IsNullOrWhiteSpace(data.worldName) ? "이름 없음" : data.worldName;
                entry.Seed = data.world.seed;
                entry.WorldId = data.worldId;
                DateTime.TryParse(data.savedAtUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out entry.SavedAtUtc);
                entry.IsMultiplayer = data.isMultiplayer;
                entry.RecoveredBackup = loaded.RecoveredBackup;
                if (!data.players.Exists(p => p.playerId == playerId))
                    entry.Error = "이 기기의 플레이어 정보가 없습니다.";
            }
            catch (Exception error) when (!(error is OperationCanceledException))
            {
                // 손상된 파일 하나 때문에 나머지 저장 목록을 잃지 않는다.
                entry.Error = error.Message;
            }
            entries.Add(entry);
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
        entries.Sort((a, b) =>
        {
            int dateOrder = b.SavedAtUtc.CompareTo(a.SavedAtUtc);
            return dateOrder != 0 ? dateOrder : StringComparer.OrdinalIgnoreCase.Compare(a.Path, b.Path);
        });
        return entries;
    }
}
