using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ServerBrowser.Accounts;

internal static class CredentialBackup {
    private const int Iterations = 600_000;
    private static readonly byte[] AssociatedData = Encoding.UTF8.GetBytes("ChoriziteServerBrowserCredentialBackup-v1");

    public static void Write(string path, string masterPassword, IReadOnlyList<BackupAccount> accounts) {
        ValidateMasterPassword(masterPassword);
        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var key = DeriveKey(masterPassword, salt);
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(accounts);
        var ciphertext = new byte[plaintext.Length];
        try {
            using var aes = new AesGcm(key, tag.Length);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, AssociatedData);
            var envelope = new BackupEnvelope {
                Version = 1,
                Iterations = Iterations,
                Salt = Convert.ToBase64String(salt),
                Nonce = Convert.ToBase64String(nonce),
                Tag = Convert.ToBase64String(tag),
                Ciphertext = Convert.ToBase64String(ciphertext)
            };
            System.IO.File.WriteAllText(path, JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public static List<BackupAccount> Read(string path, string masterPassword) {
        ValidateMasterPassword(masterPassword);
        var envelope = JsonSerializer.Deserialize<BackupEnvelope>(System.IO.File.ReadAllText(path))
            ?? throw new InvalidOperationException("Backup file is empty or invalid");
        if (envelope.Version != 1 || envelope.Iterations != Iterations) {
            throw new InvalidOperationException("Unsupported credential backup version");
        }

        var salt = Convert.FromBase64String(envelope.Salt);
        var nonce = Convert.FromBase64String(envelope.Nonce);
        var tag = Convert.FromBase64String(envelope.Tag);
        var ciphertext = Convert.FromBase64String(envelope.Ciphertext);
        var plaintext = new byte[ciphertext.Length];
        var key = DeriveKey(masterPassword, salt);
        try {
            using var aes = new AesGcm(key, tag.Length);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, AssociatedData);
            return JsonSerializer.Deserialize<List<BackupAccount>>(plaintext) ?? [];
        }
        finally {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static byte[] DeriveKey(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);

    private static void ValidateMasterPassword(string password) {
        if (string.IsNullOrWhiteSpace(password)) {
            throw new ArgumentException("Backup passphrase must not be empty", nameof(password));
        }
    }

    internal sealed class BackupAccount {
        public string Id { get; init; } = "";
        public string Username { get; init; } = "";
        public string Alias { get; init; } = "";
        public string DefaultServerId { get; init; } = "";
        public string Password { get; init; } = "";
    }

    private sealed class BackupEnvelope {
        public int Version { get; init; }
        public int Iterations { get; init; }
        public string Salt { get; init; } = "";
        public string Nonce { get; init; } = "";
        public string Tag { get; init; } = "";
        public string Ciphertext { get; init; } = "";
    }
}
