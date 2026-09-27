namespace Hora.Core.Logging;

using System.IO;

public sealed class FileHoraLogger : IHoraLogger
{
    private readonly string _filePath;
    private readonly object _gate = new();

    public FileHoraLogger(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public void Info(string message) => Write("INFO", message, null);

    public void Warn(string message) => Write("WARN", message, null);

    public void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private void Write(string level, string message, Exception? ex)
    {
        var line = ex is null
            ? $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}"
            : $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message} | {Flatten(ex)}";
        lock (_gate)
        {
            try
            {
                File.AppendAllText(_filePath, line + Environment.NewLine);
            }
            catch
            {
                // Logger asla uygulama çökertmez.
            }
        }
    }

    private static string Flatten(Exception ex)
    {
        var stack = ex.StackTrace ?? string.Empty;
        if (stack.Length > 800)
        {
            stack = stack[..800];
        }

        return $"{ex.GetType().Name}: {ex.Message} || {stack}";
    }
}
