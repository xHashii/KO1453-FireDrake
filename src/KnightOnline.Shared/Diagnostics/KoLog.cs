namespace KnightOnline.Shared.Diagnostics;

/// <summary>
/// Minimal thread-safe console+file logger standing in for the original
/// TRACE()/printf() output and the servers' hand-rolled log files.
/// (Can be swapped for Serilog/Microsoft.Extensions.Logging later without
/// touching call sites much - keep messages going through here.)
/// </summary>
public static class KoLog
{
    private static readonly object Gate = new();
    private static StreamWriter? _file;

    /// <summary>Starts appending to the given log file (directory created if needed).</summary>
    public static void ConfigureFile(string filePath)
    {
        lock (Gate)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            _file?.Dispose();
            _file = new StreamWriter(new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = true,
            };
        }
    }

    public static void Info(string message) => Write("INFO ", message);
    public static void Warn(string message) => Write("WARN ", message);
    public static void Error(string message) => Write("ERROR", message);

    public static void Error(string message, Exception ex) =>
        Write("ERROR", $"{message}\n{ex}");

    private static void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
        lock (Gate)
        {
            Console.WriteLine(line);
            _file?.WriteLine(line);
        }
    }
}
