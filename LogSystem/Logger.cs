using LogSystem.Interface;

namespace LogSystem;

public sealed class Logger : ILogger, IDisposable
{
    public const Int32 Capacity = 500;

    private readonly Queue<LogEntry> _queue = new();
    private readonly Lock _lock = new();
    private readonly String _baseDirectory;
    private Boolean _disposed;
    private Boolean _enabled = true;

    public Logger(String? baseDirectory = null)
    {
        _baseDirectory = baseDirectory ?? Path.Combine(AppContext.BaseDirectory, "XaocSirck", "Logs");
    }

    public Logger(String? baseDirectory, Boolean enabled) : this(baseDirectory)
    {
        _enabled = enabled;
    }

    public Boolean Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public void Debug(String message) => Enqueue(new LogEntry(DateTime.Now, LogLevel.Debug, message));

    public void Info(String message) => Enqueue(new LogEntry(DateTime.Now, LogLevel.Info, message));

    public void Warning(String message) => Enqueue(new LogEntry(DateTime.Now, LogLevel.Warning, message));

    public void Error(String message) => Enqueue(new LogEntry(DateTime.Now, LogLevel.Error, message, null));

    public void Error(String message, Exception exception) => Enqueue(new LogEntry(DateTime.Now, LogLevel.Error, message, exception.ToString()));

    public void Fatal(String message) => Enqueue(new LogEntry(DateTime.Now, LogLevel.Fatal, message, null));

    public void Fatal(String message, Exception exception) => Enqueue(new LogEntry(DateTime.Now, LogLevel.Fatal, message, exception.ToString()));

    public void Flush()
    {
        lock (_lock)
        {
            FlushLocked();
        }
    }

    private void Enqueue(LogEntry entry)
    {
        lock (_lock)
        {
            if (_disposed || !_enabled)
            {
                return;
            }
            _queue.Enqueue(entry);
            if (_queue.Count >= Capacity)
            {
                FlushLocked();
            }
        }
    }

    private void FlushLocked()
    {
        if (_queue.Count == 0)
        {
            return;
        }

        Directory.CreateDirectory(_baseDirectory);
        String path = Path.Combine(_baseDirectory, $"{DateTime.Now:yyyy-MM-dd}_{Environment.ProcessId}.log");
        using StreamWriter writer = new(path, true, System.Text.Encoding.UTF8);
        while (_queue.Count > 0)
        {
            LogEntry entry = _queue.Dequeue();
            writer.WriteLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Level}] {entry.Message}");
            if (!String.IsNullOrEmpty(entry.Exception))
            {
                writer.WriteLine(entry.Exception);
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }
            FlushLocked();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
