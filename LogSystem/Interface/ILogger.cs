namespace LogSystem.Interface;

public interface ILogger
{
    void Debug(String message);
    void Info(String message);
    void Warning(String message);
    void Error(String message);
    void Error(String message, Exception exception);
    void Fatal(String message);
    void Fatal(String message, Exception exception);
    void Flush();
}
