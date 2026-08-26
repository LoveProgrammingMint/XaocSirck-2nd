namespace FeatureExtractor;

internal sealed class ProgressRenderer : IDisposable
{
    private readonly Object _lock = new();
    private readonly Row[] _rows;
    private readonly Int32 _startRow;
    private readonly Int32 _barWidth;
    private readonly Boolean _enabled;
    private Boolean _disposed;

    public ProgressRenderer(Int32 workerCount, Int32 barWidth = 32)
    {
        _barWidth = barWidth;
        _rows = new Row[workerCount];
        for (Int32 i = 0; i < workerCount; i++)
            _rows[i] = new Row(0, 0, "");

        _enabled = !Console.IsOutputRedirected && workerCount > 0;
        if (_enabled)
        {
            Console.WriteLine();
            _startRow = Console.CursorTop;
            for (Int32 i = 0; i < workerCount; i++)
                Console.WriteLine(new String(' ', Console.WindowWidth - 1));
            Render();
        }
    }

    public void Update(Int32 workerId, Int32 done, Int32 total, String current)
    {
        if (!_enabled || _disposed) return;
        lock (_lock)
        {
            _rows[workerId] = new Row(done, total, current);
            RenderRow(workerId);
        }
    }

    public void Finish(Int32 totalOk, Int32 totalFiles)
    {
        if (!_enabled || _disposed) return;
        lock (_lock)
        {
            Console.SetCursorPosition(0, _startRow + _rows.Length);
            Console.WriteLine();
        }
        _disposed = true;
    }

    private void Render()
    {
        for (Int32 i = 0; i < _rows.Length; i++)
            RenderRow(i);
    }

    private void RenderRow(Int32 i)
    {
        if (i < 0 || i >= _rows.Length) return;
        Row r = _rows[i];
        Double pct = r.Total > 0 ? (Double)r.Done / r.Total : 0;
        Int32 filled = (Int32)Math.Round(pct * _barWidth);
        String bar = new String('=', Math.Max(0, filled - 1)) +
                     (filled > 0 ? ">" : "") +
                     new String(' ', Math.Max(0, _barWidth - filled));
        String status = r.Done >= r.Total && r.Total > 0 ? "done" : r.Current;
        Int32 w = Console.WindowWidth - 1;
        String line = $"[W{i}] [{bar}] {r.Done}/{r.Total} {status}";
        if (line.Length > w) line = line[..w];
        else line = line.PadRight(w);

        Console.SetCursorPosition(0, _startRow + i);
        Console.Write(line);
    }

    private readonly record struct Row(Int32 Done, Int32 Total, String Current);

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_enabled)
            {
                lock (_lock)
                {
                    Console.SetCursorPosition(0, _startRow + _rows.Length);
                    Console.WriteLine();
                }
            }
            _disposed = true;
        }
    }
}
