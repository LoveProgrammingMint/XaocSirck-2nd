namespace LogSystem.Interface;

public readonly record struct LogEntry(DateTime Timestamp, LogLevel Level, String Message, String? Exception = null);
