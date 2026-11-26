using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PixShell.Proxy;

/// <summary>
/// 代理列表持久化：`%APPDATA%\PixShell\proxies.json`。风格照抄 Storage.cs 的 HostStore。
/// 容错加载：文件不存在/整体解析失败 → 退回空列表，绝不因为一份坏文件崩溃或丢已有配置。
/// 代理密码统一经 DPAPI CredentialStore 加密保存，绝不以明文留存 proxies.json。
/// </summary>
public static class ProxyStore
{
    private static string CredentialId(string id) => "proxy-password-" + id;
    private static string FilePath => Path.Combine(HostStore.AppDir, "proxies.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new ProxyTypeJsonConverter() },
    };

    public static List<ProxyConfig> Load()
    {
        return Store.LocalFileTransaction.WithFileLock(FilePath, () =>
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<ProxyConfig>();
                var json = File.ReadAllText(FilePath);
                var list = JsonSerializer.Deserialize<List<ProxyConfig>>(json, JsonOpts);
                if (list == null)
                {
                    Store.LocalFileTransaction.BackupCorruptFile(FilePath, "反序列化结果为 null", "proxy");
                    return new List<ProxyConfig>();
                }
                var migrated = false;
                var migrationFailed = false;
                foreach (var proxy in list)
                {
                    if (!string.IsNullOrEmpty(proxy.Password))
                    {
                        var legacyPassword = proxy.Password;
                        var thisMigrationFailed = false;
                        try
                        {
                            CredentialStore.SetPassword(CredentialId(proxy.Id), legacyPassword);
                            if (CredentialStore.GetPassword(CredentialId(proxy.Id)) == legacyPassword)
                                migrated = true;
                            else
                                thisMigrationFailed = true;
                        }
                        catch
                        {
                            thisMigrationFailed = true;
                        }
                        if (thisMigrationFailed)
                        {
                            migrationFailed = true;
                            Logging.Log.Warn($"代理 {proxy.Id} 的明文凭据迁移失败，保留原配置", "proxy");
                            continue;
                        }
                    }
                    proxy.Password = CredentialStore.GetPassword(CredentialId(proxy.Id)) ?? "";
                }
                if (migrated && !migrationFailed) SaveConfigUnsafe(list);
                return list;
            }
            catch (Exception ex)
            {
                Store.LocalFileTransaction.BackupCorruptFile(FilePath, ex.Message, "proxy");
                Logging.Log.Warn($"代理配置解析失败，使用空列表: {ex.Message}", "proxy");
                return new List<ProxyConfig>();
            }
        });
    }

    private static void SaveConfigUnsafe(List<ProxyConfig> list)
    {
        var safe = list.Select(proxy => new ProxyConfig
        {
            Id = proxy.Id, Name = proxy.Name, Type = proxy.Type, Host = proxy.Host,
            Port = proxy.Port, Username = proxy.Username, Password = "",
        }).ToList();
        Store.LocalFileTransaction.WriteAllTextAtomic(FilePath, JsonSerializer.Serialize(safe, JsonOpts));
    }

    public static List<ProxyConfig> List() => Load();

    /// <summary>对齐 JS findProxyById：空 id 或 "0" 视为"不使用代理"。</summary>
    public static ProxyConfig? Find(string? id)
    {
        if (string.IsNullOrEmpty(id) || id == "0") return null;
        return Load().FirstOrDefault(p => p.Id == id);
    }

    public static void Upsert(ProxyConfig p) => TryUpsert(p, out _);

    public static bool TryUpsert(ProxyConfig p, out string? error)
    {
        try
        {
            Store.LocalFileTransaction.MutateJsonStrict<List<ProxyConfig>>(
                FilePath,
                () => new List<ProxyConfig>(),
                list =>
                {
                    var i = list.FindIndex(x => x.Id == p.Id);
                    if (i >= 0) list[i] = p; else list.Add(p);
                    foreach (var proxy in list)
                    {
                        if (string.IsNullOrEmpty(proxy.Password) && proxy.Id != p.Id) continue;
                        CredentialStore.SetPassword(CredentialId(proxy.Id), proxy.Password);
                        var stored = CredentialStore.GetPassword(CredentialId(proxy.Id));
                        if ((!string.IsNullOrEmpty(proxy.Password) && stored != proxy.Password)
                            || (string.IsNullOrEmpty(proxy.Password) && stored != null))
                            throw new InvalidOperationException("凭据写入后校验失败");
                    }
                    if (list.Any(proxy => !string.IsNullOrEmpty(proxy.Password)
                        && CredentialStore.GetPassword(CredentialId(proxy.Id)) != proxy.Password))
                        throw new InvalidOperationException("凭据写入后校验失败");
                    for (int k = 0; k < list.Count; k++)
                    {
                        list[k] = new ProxyConfig
                        {
                            Id = list[k].Id, Name = list[k].Name, Type = list[k].Type, Host = list[k].Host,
                            Port = list[k].Port, Username = list[k].Username, Password = "",
                        };
                    }
                },
                JsonOpts,
                "proxy");
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Logging.Log.Warn($"代理配置保存失败，已保持原文件不变: {ex.Message}", "proxy");
            return false;
        }
    }

    public static void Delete(string id)
    {
        try
        {
            Store.LocalFileTransaction.MutateJsonStrict<List<ProxyConfig>>(
                FilePath,
                () => new List<ProxyConfig>(),
                list =>
                {
                    list.RemoveAll(x => x.Id == id);
                    CredentialStore.Remove(CredentialId(id));
                },
                JsonOpts,
                "proxy");
        }
        catch (Exception ex)
        {
            Logging.Log.Warn($"删除代理配置失败，已保持原文件不变: {ex.Message}", "proxy");
        }
    }
}
