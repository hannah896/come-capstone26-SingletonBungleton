using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>JSON 검증과 파일 교체. 직렬화는 메인 스레드, Write/ReadText는 작업 스레드에서도 사용 가능.</summary>
public static class SaveFileStore
{
    public const int MaxFileBytes = 32 * 1024 * 1024;
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

    [Serializable]
    private class Envelope
    {
        public string format;
        public string checksum;
        public string payload;
    }

    public static string Serialize(GameSaveData data)
    {
        Validate(data);
        string payload = JsonUtility.ToJson(data);
        string text = JsonUtility.ToJson(new Envelope
        {
            format = "SingletonBungleton.Save.v1", payload = payload, checksum = Hash(payload)
        }, true);
        if (Utf8.GetByteCount(text) > MaxFileBytes) throw new InvalidDataException("저장 데이터가 허용 크기를 초과했습니다.");
        return text;
    }

    public static GameSaveData Deserialize(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || Utf8.GetByteCount(text) > MaxFileBytes)
            throw new InvalidDataException("저장 파일이 비어 있거나 너무 큽니다.");
        Envelope envelope;
        try { envelope = JsonUtility.FromJson<Envelope>(text); }
        catch (Exception e) { throw new InvalidDataException("저장 JSON을 읽을 수 없습니다.", e); }
        if (envelope == null || envelope.format != "SingletonBungleton.Save.v1" ||
            string.IsNullOrEmpty(envelope.payload) || envelope.checksum != Hash(envelope.payload))
            throw new InvalidDataException("저장 파일의 무결성 검사에 실패했습니다.");
        GameSaveData data;
        try { data = JsonUtility.FromJson<GameSaveData>(envelope.payload); }
        catch (Exception e) { throw new InvalidDataException("저장 데이터를 읽을 수 없습니다.", e); }
        Validate(data);
        return data;
    }

    public static void Validate(GameSaveData data)
    {
        if (data == null || data.schemaVersion != GameSaveData.CurrentSchemaVersion ||
            data.generatorVersion != GameSaveData.CurrentGeneratorVersion)
            throw new InvalidDataException("이 버전에서 지원하지 않는 저장 파일입니다.");
        if (!Guid.TryParse(data.worldId, out _) || data.world == null || data.players == null || data.players.Count > 64)
            throw new InvalidDataException("월드 저장 정보가 올바르지 않습니다.");
        WorldSaveAdapter.Validate(data.world);
        var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach (var player in data.players)
        {
            if (player == null || !Guid.TryParse(player.playerId, out _) || !ids.Add(player.playerId) ||
                !Finite(player.position.x) || !Finite(player.position.y) || !Finite(player.position.z) || !Finite(player.yaw) ||
                player.inventory == null || player.status == null || player.crafting == null)
                throw new InvalidDataException("플레이어 저장 정보가 올바르지 않습니다.");
            ValidatePlayer(player);
        }
    }

    public static void ValidatePlayer(PlayerSaveData player)
    {
        if (player == null || player.characterIndex < 0 || player.characterIndex > 1 ||
            player.status == null || player.inventory == null || player.crafting == null)
            throw new InvalidDataException("플레이어 저장 정보가 올바르지 않습니다.");
        var status = player.status;
        if (!Finite(status.hp) || !Finite(status.hunger) || !Finite(status.ego) ||
            !Finite(status.temperature) || !Finite(status.wetness) || !Finite(status.dotDamagePerTick) ||
            !Finite(status.dotTickInterval) || !Finite(status.dotTickTimer) || status.dotTicksLeft < 0)
            throw new InvalidDataException("플레이어 상태값이 올바르지 않습니다.");
        var inventory = player.inventory;
        if (inventory.slotCount < 1 || inventory.slotCount > 10000 || inventory.slots == null ||
            inventory.equipment == null || inventory.quickSlotCount < 0 || inventory.quickSlotCount > inventory.slotCount ||
            inventory.selectedSlotIndex < 0 || inventory.selectedSlotIndex >= inventory.slotCount)
            throw new InvalidDataException("인벤토리 저장 정보가 올바르지 않습니다.");
        var slots = new System.Collections.Generic.HashSet<int>();
        foreach (var item in inventory.slots)
        {
            WorldSaveAdapter.ValidateItem(item);
            if (item.slotIndex < 0 || item.slotIndex >= inventory.slotCount || !slots.Add(item.slotIndex))
                throw new InvalidDataException("인벤토리 슬롯이 중복되거나 범위를 벗어났습니다.");
        }
        var equipped = new System.Collections.Generic.HashSet<EquipSlot>();
        foreach (var entry in inventory.equipment)
        {
            if (entry == null || entry.equipSlot == EquipSlot.None || !Enum.IsDefined(typeof(EquipSlot), entry.equipSlot) ||
                !equipped.Add(entry.equipSlot)) throw new InvalidDataException("장비 슬롯이 올바르지 않습니다.");
            WorldSaveAdapter.ValidateItem(entry.item);
            if (entry.item.count != 1) throw new InvalidDataException("장비 수량이 올바르지 않습니다.");
        }
        if (player.crafting.learnedRecipeIds == null || player.crafting.pending == null)
            throw new InvalidDataException("제작 저장 목록이 없습니다.");
        foreach (var id in player.crafting.learnedRecipeIds)
            if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException("해금한 레시피 ID가 없습니다.");
        foreach (var job in player.crafting.pending)
            if (job == null || string.IsNullOrWhiteSpace(job.recipeId) || !Finite(job.remainingSeconds) || job.remainingSeconds < 0f)
                throw new InvalidDataException("제작 대기 정보가 올바르지 않습니다.");
        if (player.hasLocalStructures) WorldSaveAdapter.ValidateStructures(player.localStructures);
    }

    public static string ReadText(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("저장 파일이 없습니다.", path);
        if (info.Length > MaxFileBytes) throw new InvalidDataException("저장 파일이 너무 큽니다.");
        return File.ReadAllText(path, Utf8);
    }

    public static void Write(string path, string text, bool preserveBackup = false)
    {
        if (Utf8.GetByteCount(text) > MaxFileBytes) throw new InvalidDataException("저장 파일이 너무 큽니다.");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp = path + ".tmp";
        try
        {
            byte[] bytes = Utf8.GetBytes(text);
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            // 기존 파일은 교체가 성공하기 전까지 유지하며, 이전 정상 파일을 백업한다.
            if (File.Exists(path)) File.Replace(temp, path, preserveBackup ? null : path + ".bak");
            else File.Move(temp, path);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static string Hash(string text)
    {
        using var sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(Utf8.GetBytes(text)));
    }
}
