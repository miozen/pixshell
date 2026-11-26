using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using PixShell.Logging;

namespace PixShell.Store;

/// <summary>
/// 本地配置与凭据文件的跨进程事务锁 + 原子落盘 + 损坏备份保护，避免多进程竞态丢更新或读失败清空配置。
/// </summary>
public static class LocalFileTransaction
{
    private static readonly TimeSpan MutexTimeout = TimeSpan.FromSeconds(10);

    public static T WithFileLock<T>(string filePath, Func<T> action)
    {
        var mutexName = BuildMutexName(filePath);
        using var mutex = new Mutex(initiallyOwned: false, name: mutexName);
        var acquired = false;
        try
        {
            try
            {
                acquired = mutex.WaitOne(MutexTimeout);
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }

            if (!acquired)
                throw new TimeoutException($"获取本地配置锁超时：{Path.GetFileName(filePath)}");

            return action();
        }
        finally
        {
            if (acquired)
            {
                try { mutex.ReleaseMutex(); } catch { }
            }
        }
    }

    public static void WithFileLock(string filePath, Action action) =>
        WithFileLock(filePath, () =>
        {
            action();
            return true;
        });

    /// <summary>同目录临时文件落盘刷新后原子替换目标文件。</summary>
    public static void WriteAllTextAtomic(string filePath, string content, Encoding? encoding = null)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tempPath = $"{filePath}.tmp.{Guid.NewGuid():N}";
        try
        {
            var enc = encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            using (var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(fs, enc))
            {
                writer.Write(content);
                writer.Flush();
                fs.Flush(flushToDisk: true);
            }

            File.Move(tempPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

    /// <summary>在跨进程互斥锁内安全读取；解析失败时仅生成损坏备份，绝不覆写原文件。</summary>
    public static T ReadJsonOrBackup<T>(
        string filePath,
        Func<T> missingFactory,
        JsonSerializerOptions? options = null,
        string logCategory = "store") where T : class
    {
        return WithFileLock(filePath, () =>
        {
            if (!File.Exists(filePath)) return missingFactory();

            try
            {
                var json = File.ReadAllText(filePath, Encoding.UTF8);
                var parsed = JsonSerializer.Deserialize<T>(json, options);
                if (parsed != null) return parsed;
                BackupCorruptFile(filePath, "反序列化结果为 null", logCategory);
                return missingFactory();
            }
            catch (Exception ex)
            {
                BackupCorruptFile(filePath, ex.Message, logCategory);
                return missingFactory();
            }
        });
    }

    /// <summary>
    /// 在跨进程互斥锁内执行严格「读—校验—改—原子写」事务。
    /// 若既有文件损坏或无法解析，先备份损坏文件并抛出异常中止写回，禁止用单条更新清空全库。
    /// </summary>
    public static void MutateJsonStrict<T>(
        string filePath,
        Func<T> missingFactory,
        Action<T> mutate,
        JsonSerializerOptions? options = null,
        string logCategory = "store") where T : class
    {
        WithFileLock(filePath, () =>
        {
            T state;
            if (!File.Exists(filePath))
            {
                state = missingFactory();
            }
            else
            {
                string json;
                try
                {
                    json = File.ReadAllText(filePath, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    throw new IOException($"读取本地配置文件失败，已拒绝覆写 {Path.GetFileName(filePath)}: {ex.Message}", ex);
                }

                try
                {
                    state = JsonSerializer.Deserialize<T>(json, options)
                            ?? throw new InvalidDataException("JSON 反序列化结果为 null");
                }
                catch (Exception ex)
                {
                    var backup = BackupCorruptFile(filePath, ex.Message, logCategory);
                    throw new InvalidDataException(
                        backup != null
                            ? $"本地配置已损坏，已备份至 {Path.GetFileName(backup)} 并阻止覆写：{ex.Message}"
                            : $"本地配置已损坏，已阻止覆写：{ex.Message}",
                        ex);
                }
            }

            mutate(state);
            var updatedJson = JsonSerializer.Serialize(state, options);
            WriteAllTextAtomic(filePath, updatedJson, Encoding.UTF8);
        });
    }

    public static string? BackupCorruptFile(string filePath, string reason, string logCategory = "store")
    {
        try
        {
            if (!File.Exists(filePath)) return null;
            var backupPath = $"{filePath}.corrupt.{DateTime.UtcNow:yyyyMMddHHmmssfff}.{Guid.NewGuid().ToString("N")[..6]}";
            File.Copy(filePath, backupPath, overwrite: false);
            Log.Warn($"本地文件 {Path.GetFileName(filePath)} 解析失败（{reason}），已备份至 {Path.GetFileName(backupPath)}", logCategory);
            return backupPath;
        }
        catch (Exception ex)
        {
            Log.Warn($"备份损坏文件 {Path.GetFileName(filePath)} 失败: {ex.Message}", logCategory);
            return null;
        }
    }

    private static string BuildMutexName(string filePath)
    {
        var full = Path.GetFullPath(filePath).ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(full));
        return @"Local\PixShell.Store." + Convert.ToHexString(hash)[..32];
    }
}
