namespace KnightOnline.Shared.Diagnostics;

/// <summary>Thread-safe console + UTF-8 file logger. Server console messages all
/// go through this class; each server process opens its own per-run transcript.</summary>
public static class KoLog
{
    private static readonly object Gate = new();
    private static StreamWriter? _file;
    public static string? CurrentFilePath { get; private set; }

    static KoLog() => AppDomain.CurrentDomain.ProcessExit += (_, _) => CloseFile();

    /// <summary>Appends to an explicit file, mainly for callers needing a fixed path.</summary>
    public static void ConfigureFile(string filePath)
    {
        lock (Gate) OpenFile(filePath, FileMode.Append);
    }

    /// <summary>Creates a new file relative to the launch working directory.
    /// PID + random suffix prevent collisions between simultaneous starts.
    /// A filesystem error leaves console logging available instead of killing the server.</summary>
    public static string? StartRun(string service, string directory = "Logs")
    {
        if (string.IsNullOrWhiteSpace(service) || service.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_')))
            throw new ArgumentException("Use a simple service name, not a path.", nameof(service));
        lock (Gate)
        {
            try
            {
                var filename = $"{service}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}_{Environment.ProcessId}_{Guid.NewGuid():N}.txt";
                OpenFile(Path.Combine(directory, filename), FileMode.CreateNew);
                Info($"Run log: {CurrentFilePath} (local time; UTF-8; flushed after every entry)");
                return CurrentFilePath;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                CloseFile();
                Warn($"Could not create run log in '{directory}': {ex.Message}. Continuing with console logging only.");
                return null;
            }
        }
    }

    private static void OpenFile(string path, FileMode mode)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var writer = new StreamWriter(new FileStream(fullPath, mode, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
        CloseFile();
        _file = writer;
        CurrentFilePath = fullPath;
    }

    public static void CloseFile()
    {
        lock (Gate)
        {
            var file = _file;
            _file = null;
            CurrentFilePath = null;
            try { file?.Dispose(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [WARN ] Closing run log failed: {ex.Message}");
            }
        }
    }

    public static void Info(string message) => Write("INFO ", message);
    public static void Warn(string message) => Write("WARN ", message);
    public static void Error(string message) => Write("ERROR", message);
    public static void Error(string message, Exception ex) => Write("ERROR", $"{message}\n{ex}");

    private static void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
        lock (Gate)
        {
            Console.WriteLine(line);
            try { _file?.WriteLine(line); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                CloseFile();
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [WARN ] Run log write failed: {ex.Message}. Continuing with console logging only.");
            }
        }
    }
}
