using System.Globalization;
using System.Text;

namespace Hora.Core.Logging;

public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3,
}

public interface IHoraLogger
{
    void Debug(string message);
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? exception = null);
    IReadOnlyList<string> Tail(int count);
}

/// <summary>
/// Append-only daily log file. Writes never throw, and message bodies are
/// sanitized so user paths are not leaked accidentally.
/// </summary>
public sealed class FileHoraLogger : IHoraLogger, IDisposable
{
    private const long MaxBytes = 4 * 1024 * 1024;
    private readonly object _sync = new();
    private readonly string _path;
    private readonly string _directory;
    private readonly bool _daily;
    private readonly int _tailCapacity;
    private readonly Queue<string> _tail;
    private bool _disposed;

    public FileHoraLogger(string path, int tailCapacity = 400)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        _directory = Path.GetDirectoryName(path) ?? ".";
        // A path produced by HoraPaths ends in hora-YYYYMMDD.log. Recompute the
        // date on every write so a process that runs past midnight starts a new
        // file instead of appending to yesterday's.
        var name = Path.GetFileNameWithoutExtension(path);
        _daily = name.StartsWith("hora-", StringComparison.Ordinal)
                 && name.Length == 13
                 && name[5..].All(char.IsAsciiDigit);
        _tailCapacity = Math.Max(20, tailCapacity);
        _tail = new Queue<string>(_tailCapacity);
        if (!string.IsNullOrEmpty(_directory))
        {
            Directory.CreateDirectory(_directory);
            PruneOldLogs(_directory, Path.GetFileName(CurrentPath()));
        }
    }

    /// <summary>
    /// One file per day is cheap to keep, but a year of logs is not. Files older
    /// than a week are removed at startup; a locked file is skipped.
    /// </summary>
    private static void PruneOldLogs(string directory, string currentName)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-7);
            foreach (var file in Directory.EnumerateFiles(directory, "hora-*.log*"))
            {
                if (string.Equals(Path.GetFileName(file), currentName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Retention is best effort and must never block startup.
        }
    }

    public void Debug(string message) => Write(LogLevel.Debug, message, null);

    public void Info(string message) => Write(LogLevel.Info, message, null);

    public void Warn(string message) => Write(LogLevel.Warn, message, null);

    public void Error(string message, Exception? exception = null) => Write(LogLevel.Error, message, exception);

    public IReadOnlyList<string> Tail(int count)
    {
        lock (_sync)
        {
            return _tail.Skip(Math.Max(0, _tail.Count - Math.Max(1, count))).ToArray();
        }
    }

    private void Write(LogLevel level, string message, Exception? exception)
    {
        if (_disposed)
        {
            return;
        }

        var builder = new StringBuilder(160);
        builder.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(level.ToString().ToUpperInvariant())
            .Append(' ')
            .Append(Redact.Message(message));
        if (exception is not null)
        {
            builder.Append(" | ").Append(exception.GetType().Name).Append(": ").Append(Redact.Message(exception.Message));
        }

        var line = builder.ToString();
        lock (_sync)
        {
            _tail.Enqueue(line);
            while (_tail.Count > _tailCapacity)
            {
                _tail.Dequeue();
            }

            try
            {
                var path = CurrentPath();
                RotateIfNeeded(path);
                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch (IOException)
            {
                // Logging must never break the application.
            }
            catch (UnauthorizedAccessException)
            {
                // Same rationale as above.
            }
        }
    }

    private string CurrentPath() => _daily
        ? Path.Combine(_directory, $"hora-{DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log")
        : _path;

    private static void RotateIfNeeded(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < MaxBytes)
        {
            return;
        }

        var archive = path + ".1";
        try
        {
            File.Move(path, archive, overwrite: true);
        }
        catch (IOException)
        {
            // Another process may hold the file; keep appending for now.
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }
}

/// <summary>Removes home directories and user names from text before it is stored or displayed.</summary>
public static class Redact
{
    public static string Message(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var value = text.ReplaceLineEndings(" ");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
        {
            value = value.Replace(home, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        }

        var user = Environment.UserName;
        if (!string.IsNullOrEmpty(user) && user.Length > 2)
        {
            value = value.Replace("\\" + user + "\\", "\\%USER%\\", StringComparison.OrdinalIgnoreCase);
        }

        return value.Length > 800 ? value[..800] + "..." : value;
    }
}
