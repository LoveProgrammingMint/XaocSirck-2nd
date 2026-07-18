using XaocSirck_Core.Interface.Engine;
using ITimer = XaocSirck_Core.Interface.Engine.ITimer;

namespace XaocSirck_Core.Engine;

internal sealed class Timer : ITimer
{
    private readonly Boolean _enabled;
    private readonly Lock _lock = new();
    private readonly Dictionary<String, Int64> _ticks = new();

    public Timer(Boolean enabled)
    {
        _enabled = enabled;
    }

    public Boolean Enabled => _enabled;

    public IReadOnlyDictionary<String, TimeSpan> Results
    {
        get
        {
            lock (_lock)
            {
                Dictionary<String, TimeSpan> copy = new(_ticks.Count);
                foreach (KeyValuePair<String, Int64> kv in _ticks)
                    copy[kv.Key] = TimeSpan.FromTicks(kv.Value);
                return copy;
            }
        }
    }

    public void Record(String phase, TimeSpan elapsed)
    {
        if (!_enabled)
            return;
        ArgumentException.ThrowIfNullOrEmpty(phase);
        lock (_lock)
        {
            _ticks[phase] = _ticks.GetValueOrDefault(phase) + elapsed.Ticks;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _ticks.Clear();
        }
    }
}
