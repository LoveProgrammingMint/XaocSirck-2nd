namespace XaocSirck_Core.Interface.Engine;

public interface ITimer
{
    Boolean Enabled { get; }

    void Record(String phase, TimeSpan elapsed);

    void Reset();

    IReadOnlyDictionary<String, TimeSpan> Results { get; }
}
