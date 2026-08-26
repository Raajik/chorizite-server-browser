using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ServerBrowser.Accounts;

public sealed class AccountManager {
    private readonly string _accountsPath;
    private readonly ISecretStore _secrets;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public AccountManager(string dataDirectory, ISecretStore secrets) {
        Directory.CreateDirectory(dataDirectory);
        _accountsPath = Path.Combine(dataDirectory, "accounts.json");
        _secrets = secrets;
    }

    public List<SavedAccount> GetAccounts() {
        if (!File.Exists(_accountsPath)) return [];
        return JsonSerializer.Deserialize<List<SavedAccount>>(
            File.ReadAllText(_accountsPath),
            _jsonOptions) ?? [];
    }

    public SavedAccount Save(
        string id,
        string username,
        string alias,
        string defaultServerId,
        string password) {
        if (string.IsNullOrWhiteSpace(username)) {
            throw new ArgumentException("Username is required", nameof(username));
        }

        var accounts = GetAccounts();
        var accountId = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
        var existingIndex = accounts.FindIndex(account => account.Id == accountId);
        if (existingIndex < 0 && string.IsNullOrEmpty(password)) {
            throw new ArgumentException("Password is required for a new account", nameof(password));
        }

        var account = new SavedAccount {
            Id = accountId,
            Username = username.Trim(),
            Alias = string.IsNullOrWhiteSpace(alias) ? username.Trim() : alias.Trim(),
            DefaultServerId = defaultServerId?.Trim() ?? ""
        };

        if (existingIndex >= 0) accounts[existingIndex] = account;
        else accounts.Add(account);

        File.WriteAllText(_accountsPath, JsonSerializer.Serialize(accounts, _jsonOptions));
        if (!string.IsNullOrEmpty(password)) _secrets.Write(accountId, password);
        return account;
    }

    public string GetPassword(string accountId) =>
        _secrets.Read(accountId)
        ?? throw new InvalidOperationException("No saved password exists for this account");

    public void ExportBackup(string path, string masterPassword) {
        var accounts = GetAccounts().Select(account => new CredentialBackup.BackupAccount {
            Id = account.Id,
            Username = account.Username,
            Alias = account.Alias,
            DefaultServerId = account.DefaultServerId,
            Password = GetPassword(account.Id)
        }).ToList();
        CredentialBackup.Write(path, masterPassword, accounts);
    }

    public void ImportBackup(string path, string masterPassword) {
        foreach (var account in CredentialBackup.Read(path, masterPassword)) {
            Save(
                account.Id,
                account.Username,
                account.Alias,
                account.DefaultServerId,
                account.Password);
        }
    }

    public void Delete(string accountId) {
        var accounts = GetAccounts().Where(account => account.Id != accountId).ToList();
        File.WriteAllText(_accountsPath, JsonSerializer.Serialize(accounts, _jsonOptions));
        _secrets.Delete(accountId);
    }
}
