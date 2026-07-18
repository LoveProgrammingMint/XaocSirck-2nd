using System.Diagnostics;

namespace XaocSirck_Core.Interface.Engine;

public readonly struct TimerScope : IDisposable
{
    private readonly ITimer _timer;
    private readonly String _phase;
    private readonly Stopwatch? _stopwatch;

    public TimerScope(ITimer timer, String phase)
    {
        _timer = timer;
        _phase = phase;
        _stopwatch = timer.Enabled ? Stopwatch.StartNew() : null;
    }

    public void Dispose()
    {
        if (_stopwatch == null)
            return;
        _stopwatch.Stop();
        _timer.Record(_phase, _stopwatch.Elapsed);
    }
}
