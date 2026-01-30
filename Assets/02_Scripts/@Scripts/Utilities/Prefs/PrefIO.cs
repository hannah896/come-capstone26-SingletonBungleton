using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace Blossom.Preference {
    
    internal static class PrefIO {

        private static string _persistentDataPath;

        public static void Initialize() {
            _persistentDataPath = Application.persistentDataPath;
        }

        public static bool Serialize(PrefData data) {
            string path = GetPath(data.Key);

            try {
                if (PrefSystem.UseEncryption) {
                    byte[] bytes = PrefEncrypt.Encrypt(SerializeToBytes(data));
                    if (bytes == null) {
                        Debug.LogError($"[PrefIO] Failed to encrypt file: {path}");
                        return false;
                    }

                    File.WriteAllBytes(path, bytes);
                }
                else {
                    using FileStream stream = File.Open(path, FileMode.Create);
                    BinaryFormatter formatter = new();
                    formatter.Serialize(stream, data);
                }

                return true;
            }
            catch (Exception e) {
                Debug.LogError($"[PrefIO] Serialize Error: {e.Message}");
                return false;
            }

        }

        public static T Deserialize<T>(string key) where T : IPrefData, new() {
            string path = GetPath(key);

            if (!File.Exists(path)) return NewData<T>();

            try {
                if (PrefSystem.UseEncryption) {
                    byte[] bytes = PrefEncrypt.Decrypt(File.ReadAllBytes(path));
                    if (bytes == null || bytes.Length == 0) {
                        Debug.LogError($"[PrefIO] Failed to decrypt file: {path}");
                        return NewData<T>();
                    }

                    T data = DeserializeFromBytes<T>(bytes);
                    return data;
                }
                using FileStream stream = File.Open(path, FileMode.Open);
                BinaryFormatter formatter = new();
                return (T)formatter.Deserialize(stream);
            }
            catch (Exception e) {
                Debug.LogError($"[PrefIO] Deserialize Error: {e.Message}");
                return NewData<T>();
            }
        }

        public static bool DeleteFile(string key) {
            try {
                string path = GetPath(key);
                if (!File.Exists(path)) return false;
                File.Delete(path);
                return true;
            }
            catch (Exception e) {
                Debug.LogError($"[PrefIO] Delete Error: {e.Message}");
                return false;
            }
        }

        private static T NewData<T>() where T : IPrefData, new(){
            return new T();
        }

        private static string GetPath(string key) {
            return Path.Combine(GetEnsurePath(ref _persistentDataPath), $"{key}.dat");
        }
        
        private static string GetEnsurePath(ref string path) {
            if (!string.IsNullOrEmpty(path)) return path;
            path = Application.persistentDataPath;
            return path;
        }

        private static byte[] SerializeToBytes<T>(T obj) {
            using MemoryStream stream = new();
            BinaryFormatter formatter = new();
            formatter.Serialize(stream, obj);
            return stream.ToArray();
        }
        
        private static T DeserializeFromBytes<T>(byte[] data) where T : new() {
            if (data == null || data.Length == 0) return new T();
            try {
                using MemoryStream stream = new(data);
                BinaryFormatter formatter = new();
                return (T)formatter.Deserialize(stream);
            }
            catch (Exception e) {
                Debug.LogError($"[PrefIO] DeserializeFromBytes<{typeof(T).Name}> Exception: {e.Message}");
                return new T();
            }
        }

    }
    
    internal static class PrefEncrypt {
        internal static readonly string KEY = "m71a12x28p94r6e5";
        internal static readonly byte[] KeyBytes = Encoding.UTF8.GetBytes(KEY);

        public static byte[] Encrypt(byte[] rawData) {
            if (rawData == null || rawData.Length == 0) return Array.Empty<byte>();

            try {
                using var rijndael = new RijndaelManaged();
                rijndael.Mode = CipherMode.CBC;
                rijndael.Padding = PaddingMode.PKCS7;
                rijndael.KeySize = 128;
                rijndael.BlockSize = 128;
                rijndael.Key = KeyBytes;
                rijndael.IV = KeyBytes;

                using MemoryStream memoryStream = new();
                using (CryptoStream cryptoStream = new(
                           memoryStream, rijndael.CreateEncryptor(), CryptoStreamMode.Write)) {
                    cryptoStream.Write(rawData, 0, rawData.Length);
                    cryptoStream.FlushFinalBlock();
                }

                return memoryStream.ToArray();
            }
            catch (Exception e) {
                Debug.LogError($"[PrefEncrypt] EncryptBinary Error: {e.Message}");
                return null;
            }
        }

        public static byte[] Decrypt(byte[] encryptedData) {
            if (encryptedData == null || encryptedData.Length == 0) return Array.Empty<byte>();

            try {
                using var rijndael = new RijndaelManaged();
                rijndael.Mode = CipherMode.CBC;
                rijndael.Padding = PaddingMode.PKCS7;
                rijndael.KeySize = 128;
                rijndael.BlockSize = 128;
                rijndael.Key = KeyBytes;
                rijndael.IV = KeyBytes;

                using MemoryStream memoryStream = new();
                using (CryptoStream cryptoStream = new(
                           memoryStream, rijndael.CreateDecryptor(), CryptoStreamMode.Write)) {
                    cryptoStream.Write(encryptedData, 0, encryptedData.Length);
                    cryptoStream.FlushFinalBlock();
                }

                return memoryStream.ToArray();
            }
            catch (Exception e) {
                Debug.LogError($"[PrefEncrypt] DecryptBinary Error: {e.Message}");
                return null;
            }
        }
    }
    
}
