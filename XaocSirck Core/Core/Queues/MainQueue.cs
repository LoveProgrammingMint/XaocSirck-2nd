using System.Security.Cryptography;
using XaocSirck_Core.Cloud;
using XaocSirck_Core.Feature;
using XaocSirck_Core.Inference;
using XaocSirck_Core.Interface.Engine;

namespace XaocSirck_Core.Core.Queues;

internal sealed class MainQueue : SPSC<MainQueue.TaskItem>, IDisposable
{
    private readonly CloudClient _cloud;
    private readonly BitremalInferenceService? _inference;
    private readonly ZeroflowsInferenceService? _zeroflows;
    private readonly Features _features;
    private readonly List<ScanResult> _results = new();
    private Thread? _producer;
    private Thread? _consumer;
    private CancellationTokenSource? _cts;
    private String? _scanPath;
    private Int32 _maxFiles;
    private Boolean _disposed;

    public MainQueue(CloudClient cloud, BitremalInferenceService inference, Int32 capacity = 1024) : this(cloud, inference, null, capacity)
    {
    }

    public MainQueue(CloudClient cloud, BitremalInferenceService? inference, ZeroflowsInferenceService? zeroflows, Int32 capacity = 1024) : base(capacity)
    {
        _cloud = cloud;
        _inference = inference;
        _zeroflows = zeroflows;
        _features = new Features();
    }

    public IReadOnlyList<ScanResult> Results => _results;

    public void Start(String path, EngineMode mode, Boolean recursive = true, Int32 maxFiles = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(MainQueue));
        ArgumentNullException.ThrowIfNull(path);
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Directory not found: {path}");

        _scanPath = path;
        _maxFiles = maxFiles;
        _cts = new CancellationTokenSource();
        _producer = new Thread(() => ProducerLoop(path, recursive));
        _consumer = new Thread(() => ConsumerLoop(mode));
        _producer.Start();
        _consumer.Start();
    }

    public void Stop() => _cts?.Cancel();

    public void Wait()
    {
        _producer?.Join();
        _consumer?.Join();
    }

    public void StartAndWait(String path, EngineMode mode, Boolean recursive = true, Int32 maxFiles = 0)
    {
        Start(path, mode, recursive, maxFiles);
        Wait();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            Wait();
            _cts?.Dispose();
            _features.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    private void ProducerLoop(String path, Boolean recursive)
    {
        SearchOption option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        Int32 count = 0;
        try
        {
            foreach (String file in Directory.EnumerateFiles(path, "*", option))
            {
                if (_cts!.IsCancellationRequested)
                    break;
                if (_maxFiles > 0 && count >= _maxFiles)
                    break;

                TaskItem item = new(file);
                while (!TryEnqueue(item))
                {
                    if (_cts.IsCancellationRequested)
                        return;
                    Thread.Yield();
                }
                count++;
            }
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException)
        {
        }
    }

    private void ConsumerLoop(EngineMode mode)
    {
        while (!_cts!.IsCancellationRequested)
        {
            if (TryDequeue(out TaskItem? item))
            {
                Process(item, mode);
            }
            else if (_producer?.IsAlive == false)
            {
                break;
            }
            else
            {
                Thread.Yield();
            }
        }
    }

    private void Process(TaskItem item, EngineMode mode)
    {
        try
        {
            CloudCacheResult cacheResult = CloudCacheResult.Error;
            if (_cloud.IsConnected)
            {
                Byte[] sha256 = ComputeSha256(item.FilePath);
                cacheResult = _cloud.QueryCache(sha256);
            }

            if (cacheResult == CloudCacheResult.Hit)
            {
                AddResult(item.FilePath, cacheResult, null, null);
                return;
            }

            _features.Set(item.FilePath, mode);
            FeaturesStruct features = _features.Execute(null);

            Single[]? bitremalProbs = null;
            Single[]? zeroflowsProbs = null;

            if (mode.Bitremal != _Mode_Bitremal.Disabled && _inference != null)
            {
                try
                {
                    bitremalProbs = _inference.InferOverThink(features.AL, features.RB, features.IT, features.EM);
                }
                catch
                {
                }
            }

            if (mode.Zeroflow != _Mode_Zeroflows.Disabled && _zeroflows != null && features.Zeroflow != IntPtr.Zero)
            {
                try
                {
                    zeroflowsProbs = _zeroflows.Infer(features.Zeroflow);
                }
                catch
                {
                }
            }

            AddResult(item.FilePath, cacheResult, bitremalProbs, zeroflowsProbs);
        }
        catch
        {
            AddResult(item.FilePath, CloudCacheResult.Error, null, null);
        }
    }

    private void AddResult(String filePath, CloudCacheResult cacheResult, Single[]? bitremalProbabilities, Single[]? zeroflowsProbabilities)
    {
        lock (_results)
            _results.Add(new ScanResult(filePath, cacheResult, bitremalProbabilities, zeroflowsProbabilities));
    }

    private static Byte[] ComputeSha256(String filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        return SHA256.HashData(stream);
    }

    internal sealed class TaskItem
    {
        public String FilePath { get; }

        public TaskItem(String filePath)
        {
            ArgumentNullException.ThrowIfNull(filePath);
            FilePath = filePath;
        }
    }
}

internal sealed class ScanResult
{
    public String FilePath { get; }
    public CloudCacheResult CacheResult { get; }
    public Single[]? BitremalProbabilities { get; }
    public Single[]? ZeroflowsProbabilities { get; }

    public ScanResult(String filePath, CloudCacheResult cacheResult, Single[]? bitremalProbabilities, Single[]? zeroflowsProbabilities)
    {
        FilePath = filePath;
        CacheResult = cacheResult;
        BitremalProbabilities = bitremalProbabilities;
        ZeroflowsProbabilities = zeroflowsProbabilities;
    }

    public Single[] Probabilities => BitremalProbabilities ?? ZeroflowsProbabilities ?? [1.0f, 0.0f];

    public Boolean IsMalicious
    {
        get
        {
            if (CacheResult == CloudCacheResult.Hit)
                return true;
            if (BitremalProbabilities is { Length: >= 2 } && BitremalProbabilities[1] > BitremalProbabilities[0])
                return true;
            if (ZeroflowsProbabilities is { Length: >= 2 } && ZeroflowsProbabilities[1] > ZeroflowsProbabilities[0])
                return true;
            return false;
        }
    }
}
