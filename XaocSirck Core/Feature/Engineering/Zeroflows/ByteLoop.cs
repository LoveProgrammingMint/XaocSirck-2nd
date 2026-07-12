namespace XaocSirck_Core.Feature.Engineering.Zeroflows;
internal sealed unsafe class ByteLoop : IDisposable
{
    private readonly ShareFeatures _features;
    private ByteHistogram? _histogram;
    private Entropys? _entropys;
    private ByteStatistics? _statistics;
    private ByteRuns? _runs;
    private BytePatterns? _patterns;
    private Int32 _headFilled;
    private Int32 _tailFilled;
    private Boolean _disposed;

    public ByteLoop(ShareFeatures features)
    {
        ArgumentNullException.ThrowIfNull(features);
        _features = features;
    }

    public void Register(ByteHistogram histogram, Entropys entropys, ByteStatistics statistics, ByteRuns runs, BytePatterns patterns)
    {
        ArgumentNullException.ThrowIfNull(histogram);
        ArgumentNullException.ThrowIfNull(entropys);
        ArgumentNullException.ThrowIfNull(statistics);
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(patterns);
        _histogram = histogram;
        _entropys = entropys;
        _statistics = statistics;
        _runs = runs;
        _patterns = patterns;
    }

    public void LoadFromFile(String filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found.", filePath);

        using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        Int64 length = stream.Length;

        _headFilled = (Int32)Math.Min(length, _features.HeadLength);
        if (_headFilled > 0)
            stream.ReadExactly(_features.HeadSpan.Slice(0, _headFilled));
        if (_headFilled < _features.HeadLength)
            _features.HeadSpan.Slice(_headFilled).Clear();

        _tailFilled = (Int32)Math.Min(length, _features.TailLength);
        if (_tailFilled > 0)
        {
            if (length > _tailFilled)
                stream.Seek(-_tailFilled, SeekOrigin.End);
            else
                stream.Seek(0, SeekOrigin.Begin);
            stream.ReadExactly(_features.TailSpan.Slice(0, _tailFilled));
        }
        if (_tailFilled < _features.TailLength)
            _features.TailSpan.Slice(_tailFilled).Clear();
    }

    public void Execute()
    {
        if (_histogram == null || _entropys == null || _statistics == null || _runs == null || _patterns == null)
            return;

        _histogram.Clear();
        _entropys.Clear();
        _statistics.Clear();
        _runs.Clear();
        _patterns.Clear();

        if (_headFilled > 0)
        {
            Byte* head = _features.HeadPtr;
            _histogram.ProcessHead(head, _headFilled);
            _entropys.ProcessHead(head, _headFilled);
            _statistics.ProcessHead(head, _headFilled);
            _runs.ProcessHead(head, _headFilled);
            _patterns.ProcessHead(head, _headFilled);
        }

        if (_tailFilled > 0)
        {
            Byte* tail = _features.TailPtr;
            _histogram.ProcessTail(tail, _tailFilled);
            _entropys.ProcessTail(tail, _tailFilled);
            _statistics.ProcessTail(tail, _tailFilled);
            _runs.ProcessTail(tail, _tailFilled);
            _patterns.ProcessTail(tail, _tailFilled);
        }

        _histogram.Complete();
        _entropys.Complete();
        _statistics.Complete();
        _runs.Complete();
        _patterns.Complete();
    }

    public void Clear()
    {
        _headFilled = 0;
        _tailFilled = 0;
        _features.Clear();
        _histogram?.Clear();
        _entropys?.Clear();
        _statistics?.Clear();
        _runs?.Clear();
        _patterns?.Clear();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _histogram?.Dispose();
            _entropys?.Dispose();
            _statistics?.Dispose();
            _runs?.Dispose();
            _patterns?.Dispose();
            _histogram = null;
            _entropys = null;
            _statistics = null;
            _runs = null;
            _patterns = null;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
