using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using PeNet;
using XaocSirck_Core.Cloud;
using XaocSirck_Core.Engine;
using XaocSirck_Core.Engine.Feature_Cache;
using XaocSirck_Core.Feature;
using XaocSirck_Core.Inference;
using XaocSirck_Core.Interface.Cloud;
using XaocSirck_Core.Interface.Engine;
using XaocSirck_Core.Interface.Feature;
using XaocSirck_Core.Interface.Settings;
using Timer = XaocSirck_Core.Engine.Timer;
using ITimer = XaocSirck_Core.Interface.Engine.ITimer;

namespace XaocSirck_Core.Core.Queues;

internal sealed class MainQueue : SPSC<MainQueue.TaskItem>, IDisposable
{
    private readonly CloudClient _cloud;
    private readonly BitremalInferenceService? _inference;
    private readonly ZeroflowsInferenceService? _zeroflows;
    private readonly ICharwolfEngine? _charwolf;
    private readonly EngineSettings _settings;
    private readonly Features _features;
    private readonly FeatureCache? _featureCache;
    private readonly ITimer _timer;
    private readonly CompressedArchive _archive = new();
    private readonly Signature _signature = new();
    private readonly List<ScanResult> _results = new();
    private Thread? _producer;
    private Thread? _consumer;
    private CancellationTokenSource? _cts;
    private String? _scanPath;
    private Int32 _maxFiles;
    private Boolean _disposed;

    public MainQueue(CloudClient cloud, BitremalInferenceService inference, Int32 capacity = 1024) : this(cloud, inference, null, null, new EngineSettings { QueueCapacity = capacity }, capacity)
    {
    }

    public MainQueue(CloudClient cloud, BitremalInferenceService? inference, ZeroflowsInferenceService? zeroflows, Int32 capacity = 1024) : this(cloud, inference, zeroflows, null, new EngineSettings { QueueCapacity = capacity }, capacity)
    {
    }

    public MainQueue(CloudClient cloud, BitremalInferenceService? inference, ZeroflowsInferenceService? zeroflows, EngineSettings settings, Int32 capacity = 1024) : this(cloud, inference, zeroflows, null, settings, capacity)
    {
    }

    public MainQueue(CloudClient cloud, BitremalInferenceService? inference, ZeroflowsInferenceService? zeroflows, ICharwolfEngine? charwolf, EngineSettings settings, Int32 capacity = 1024) : base(capacity)
    {
        _cloud = cloud;
        _inference = inference;
        _zeroflows = zeroflows;
        _charwolf = charwolf;
        _settings = settings ?? new EngineSettings();
        _features = new Features();
        _timer = new Timer(_settings.EnableTiming);
        _featureCache = _settings.EnableFeatureCache ? new FeatureCache() : null;
    }

    public IReadOnlyList<ScanResult> Results => _results;
    public ITimer Timer => _timer;

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
            _featureCache?.Dispose();
            _archive.Dispose();
            if (_timer.Enabled)
                LogTimingSummary();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    private void LogTimingSummary()
    {
        IReadOnlyDictionary<String, TimeSpan> results = _timer.Results;
        if (results.Count == 0)
            return;
        TimeSpan total = TimeSpan.Zero;
        foreach (TimeSpan value in results.Values)
            total += value;
        App.Logger.Info($"[Timing] Total processing time: {total.TotalMilliseconds:F3} ms");
        foreach (KeyValuePair<String, TimeSpan> kv in results.OrderByDescending(x => x.Value.Ticks))
            App.Logger.Info($"[Timing] {kv.Key}: {kv.Value.TotalMilliseconds:F3} ms");
    }

    private void ProducerLoop(String path, Boolean recursive)
    {
        SearchOption option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        Boolean filterByExtension = _settings.FilterByExtension;
        String[] targetExtensions = _settings.TargetExtensions;
        Int32 count = 0;
        try
        {
            foreach (String file in Directory.EnumerateFiles(path, "*", option))
            {
                if (_cts!.IsCancellationRequested)
                    break;
                if (_maxFiles > 0 && count >= _maxFiles)
                    break;
                if (filterByExtension && !IsTargetExtension(file, targetExtensions))
                    continue;

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
        catch (UnauthorizedAccessException ex)
        {
            App.Logger.Warning($"Producer access denied: {path} - {ex.Message}");
        }
        catch (DirectoryNotFoundException ex)
        {
            App.Logger.Warning($"Producer directory not found: {path} - {ex.Message}");
        }
        catch (IOException ex)
        {
            App.Logger.Warning($"Producer IO error: {path} - {ex.Message}");
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
            Byte[] sha256;
            using (new TimerScope(_timer, "SHA256"))
                sha256 = ComputeSha256(item.FilePath);

            CloudCacheResult cacheResult = CloudCacheResult.Error;
            if (_settings.EnableCloudCache && _cloud.IsConnected)
            {
                try { using (new TimerScope(_timer, "CloudCache")) cacheResult = _cloud.QueryCache(sha256); }
                catch (Exception ex) { App.Logger.Error($"Cloud cache query failed: {item.FilePath}", ex); }
            }

            if (_settings.ParticipateInCoConstruction && _cloud.IsConnected && cacheResult == CloudCacheResult.Miss)
            {
                Boolean reported = false;
                try { reported = _cloud.Report(sha256, item.FilePath); }
                catch (Exception ex) { App.Logger.Error($"Cloud report failed: {item.FilePath}", ex); }
                if (!reported)
                    App.Logger.Warning($"Cloud report rejected: {item.FilePath}");
            }

            SignatureResult? signatureResult = null;
            if (mode.Signature != _Mode_Signature.Disabled)
            {
                try { using (new TimerScope(_timer, "Signature")) signatureResult = CheckSignature(item.FilePath, sha256, mode.Signature); }
                catch (Exception ex) { App.Logger.Error($"Signature check failed: {item.FilePath}", ex); }
            }

            if (cacheResult == CloudCacheResult.Hit)
            {
                AddResult(item.FilePath, cacheResult, null, null, null, signatureResult, null, null, null);
                return;
            }

            if (signatureResult?.IsTrusted == true)
            {
                AddResult(item.FilePath, cacheResult, null, null, null, signatureResult, null, null, null);
                return;
            }

            CharwolfScanResult? charwolfResult = null;
            if (mode.Charwolf != _Mode_Charwolf.Disabled && _charwolf != null)
            {
                try { using (new TimerScope(_timer, "Charwolf")) charwolfResult = _charwolf.ScanFile(item.FilePath); }
                catch (Exception ex) { App.Logger.Error($"Charwolf scan failed: {item.FilePath}", ex); }
            }

            ShellResult? shellResult = null;
            if (mode.Shell != _Mode_Shell.Disabled)
            {
                try { using (new TimerScope(_timer, "Shell")) shellResult = CheckShell(item.FilePath); }
                catch (Exception ex) { App.Logger.Error($"Shell check failed: {item.FilePath}", ex); }
            }

            ArchiveResult? archiveResult = null;
            if (mode.Archive != _Mode_Archive.Disabled)
            {
                try { using (new TimerScope(_timer, "Archive")) archiveResult = CheckArchive(item.FilePath); }
                catch (Exception ex) { App.Logger.Error($"Archive check failed: {item.FilePath}", ex); }
            }

            DocumentationResult? documentationResult = null;
            if (mode.Documentation != _Mode_Documentation.Disabled)
            {
                try { using (new TimerScope(_timer, "Documentation")) documentationResult = CheckDocumentation(item.FilePath); }
                catch (Exception ex) { App.Logger.Error($"Documentation check failed: {item.FilePath}", ex); }
            }

            String sha256String = BitConverter.ToString(sha256).Replace("-", "");
            FeaturesStruct features;
            Boolean fromCache = false;
            Boolean needFullFeatures = mode.Bitremal != _Mode_Bitremal.Disabled;
            Boolean useFeatureCache = _settings.EnableFeatureCache && _featureCache != null;
            try
            {
                FeatureRecord? cacheRecord = null;
                if (useFeatureCache && needFullFeatures)
                {
                    using (new TimerScope(_timer, "FeatureCacheQuery"))
                        cacheRecord = _featureCache!.Get(sha256String);
                }
                if (cacheRecord != null)
                {
                    features = RestoreFeatures(cacheRecord);
                    fromCache = true;
                }
                else
                {
                    using (new TimerScope(_timer, "FeatureExtraction"))
                    {
                        _features.Set(item.FilePath, mode);
                        features = needFullFeatures ? _features.Execute_Cache() : _features.Execute(null);
                    }
                    try
                    {
                        if (useFeatureCache && needFullFeatures && features.RB != IntPtr.Zero && features.AL != IntPtr.Zero && features.IT != IntPtr.Zero && features.EM != IntPtr.Zero && features.Zeroflow != IntPtr.Zero)
                        {
                            using (new TimerScope(_timer, "FeatureCacheInsert"))
                                _featureCache!.Insert(sha256String, features);
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Error($"Feature cache insert failed: {item.FilePath}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Feature cache access failed: {item.FilePath}", ex);
                using (new TimerScope(_timer, "FeatureExtraction"))
                {
                    _features.Set(item.FilePath, mode);
                    features = needFullFeatures ? _features.Execute_Cache() : _features.Execute(null);
                }
            }

            Single[]? bitremalProbs = null;
            Single[]? zeroflowsProbs = null;

            try
            {
                if (mode.Bitremal != _Mode_Bitremal.Disabled && _inference != null)
                {
                    try { using (new TimerScope(_timer, "Bitremal")) bitremalProbs = _inference.InferOverThink(features.AL, features.RB, features.IT, features.EM); }
                    catch (Exception ex) { App.Logger.Error($"Bitremal inference failed: {item.FilePath}", ex); }
                }

                if (mode.Zeroflow != _Mode_Zeroflows.Disabled && _zeroflows != null && features.Zeroflow != IntPtr.Zero)
                {
                    try { using (new TimerScope(_timer, "Zeroflows")) zeroflowsProbs = _zeroflows.Infer(features.Zeroflow); }
                    catch (Exception ex) { App.Logger.Error($"Zeroflows inference failed: {item.FilePath}", ex); }
                }

                AddResult(item.FilePath, cacheResult, bitremalProbs, zeroflowsProbs, charwolfResult, signatureResult, shellResult, archiveResult, documentationResult);
            }
            finally
            {
                if (fromCache)
                    FreeFeatures(features);
            }
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Unhandled processing error: {item.FilePath}", ex);
            AddResult(item.FilePath, CloudCacheResult.Error, null, null, null, null, null, null, null);
        }
    }

    private SignatureResult CheckSignature(String filePath, Byte[] sha256, _Mode_Signature signatureMode)
    {
        SignatureResult result = new() { FilePath = filePath };
        if (!File.Exists(filePath))
            return result;

        PeFile? pe = null;
        try { pe = new PeFile(filePath); }
        catch { }

        if (pe == null)
            return result;

        result.IsSigned = pe.SigningAuthenticodeCertificate != null;
        if (!result.IsSigned)
            return result;

        Boolean localValid = _signature.FileDigitallySignedAndValid(pe);
        result.IsLocallyTrusted = localValid;

        if (signatureMode == _Mode_Signature.Strict)
        {
            CloudSignatureResult cloudResult = CloudSignatureResult.Error;
            if (_settings.EnableCloudCache && _cloud.IsConnected)
            {
                try { cloudResult = _cloud.QuerySignatureTrust(sha256); }
                catch { }
            }
            result.CloudResult = cloudResult;
            result.IsTrusted = localValid && cloudResult == CloudSignatureResult.Trusted;
        }
        else
        {
            result.IsTrusted = localValid;
        }

        return result;
    }

    private ShellResult CheckShell(String filePath)
    {
        ShellResult result = new() { FilePath = filePath };
        PeFile? pe = null;
        try { pe = new PeFile(filePath); }
        catch { }

        if (pe == null)
            return result;

        Shell checker = new();
        checker.Set(pe);
        result.Hit = checker.Check();
        return result;
    }

    private unsafe ArchiveResult CheckArchive(String filePath)
    {
        ArchiveResult result = new() { FilePath = filePath };
        if (!IsArchiveExtension(filePath))
            return result;

        try
        {
            _archive.Load(filePath);
            result.IsArchive = true;
            Int32 entries = 0;
            Int32 suspicious = 0;
            while (_archive.Next())
            {
                entries++;
                Int64 length = _archive.Length;
                if (length <= 0)
                    continue;

                ReadOnlySpan<Byte> header = new(_archive.Buffer.ToPointer(), (Int32)Math.Min(length, 8));
                if (IsExecutableHeader(header))
                    suspicious++;

                if (entries >= 1024)
                    break;
            }
            result.EntryCount = entries;
            result.SuspiciousEntryCount = suspicious;
        }
        catch
        {
            result.IsArchive = false;
        }
        finally
        {
            _archive.Dispose();
        }

        return result;
    }

    private DocumentationResult CheckDocumentation(String filePath)
    {
        DocumentationResult result = new() { FilePath = filePath };
        if (!File.Exists(filePath))
            return result;

        try { result.HasMacro = DocVBA.HasMacro(filePath); }
        catch { }
        return result;
    }

    private void AddResult(String filePath, CloudCacheResult cacheResult, Single[]? bitremalProbabilities, Single[]? zeroflowsProbabilities, CharwolfScanResult? charwolfResult, SignatureResult? signatureResult, ShellResult? shellResult, ArchiveResult? archiveResult, DocumentationResult? documentationResult)
    {
        lock (_results)
            _results.Add(new ScanResult(filePath, cacheResult, bitremalProbabilities, zeroflowsProbabilities, charwolfResult, signatureResult, shellResult, archiveResult, documentationResult));
    }

    private static Byte[] ComputeSha256(String filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        return SHA256.HashData(stream);
    }

    private static Boolean IsTargetExtension(String filePath, String[] targetExtensions)
    {
        String ext = Path.GetExtension(filePath);
        for (Int32 i = 0; i < targetExtensions.Length; i++)
            if (String.Equals(ext, targetExtensions[i], StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static Boolean IsArchiveExtension(String filePath)
    {
        String ext = Path.GetExtension(filePath);
        ReadOnlySpan<String> archiveExtensions = [".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".cab", ".iso"];
        for (Int32 i = 0; i < archiveExtensions.Length; i++)
            if (String.Equals(ext, archiveExtensions[i], StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static Boolean IsExecutableHeader(ReadOnlySpan<Byte> header)
    {
        if (header.Length < 2)
            return false;
        if (header[0] == 0x4D && header[1] == 0x5A)
            return true;
        if (header.Length >= 4 && header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04)
            return false;
        return false;
    }

    private static unsafe FeaturesStruct RestoreFeatures(FeatureRecord record)
    {
        FeaturesStruct features = new();
        features.RB = (IntPtr)NativeMemory.AlignedAlloc((UIntPtr)(record.RB.Length * sizeof(Single)), 64);
        features.EM = (IntPtr)NativeMemory.AlignedAlloc((UIntPtr)(record.EM.Length * sizeof(Single)), 64);
        features.AL = (IntPtr)NativeMemory.AlignedAlloc((UIntPtr)(record.AL.Length * sizeof(Single)), 64);
        features.Zeroflow = (IntPtr)NativeMemory.AlignedAlloc((UIntPtr)(record.ZF.Length * sizeof(Single)), 64);

        Int32 itTotalBytes = sizeof(Int32) + record.IT.Length * sizeof(Single);
        features.IT = (IntPtr)NativeMemory.AlignedAlloc((UIntPtr)itTotalBytes, 64);
        *(Int32*)features.IT = itTotalBytes;

        fixed (Single* rbPtr = record.RB)
            Buffer.MemoryCopy(rbPtr, features.RB.ToPointer(), record.RB.Length * sizeof(Single), record.RB.Length * sizeof(Single));
        fixed (Single* emPtr = record.EM)
            Buffer.MemoryCopy(emPtr, features.EM.ToPointer(), record.EM.Length * sizeof(Single), record.EM.Length * sizeof(Single));
        fixed (Single* alPtr = record.AL)
            Buffer.MemoryCopy(alPtr, features.AL.ToPointer(), record.AL.Length * sizeof(Single), record.AL.Length * sizeof(Single));
        fixed (Single* zfPtr = record.ZF)
            Buffer.MemoryCopy(zfPtr, features.Zeroflow.ToPointer(), record.ZF.Length * sizeof(Single), record.ZF.Length * sizeof(Single));
        fixed (Single* itPtr = record.IT)
            Buffer.MemoryCopy(itPtr, (Byte*)features.IT + sizeof(Int32), record.IT.Length * sizeof(Single), record.IT.Length * sizeof(Single));

        return features;
    }

    private static unsafe void FreeFeatures(FeaturesStruct features)
    {
        if (features.RB != IntPtr.Zero) { NativeMemory.AlignedFree(features.RB.ToPointer()); features.RB = IntPtr.Zero; }
        if (features.EM != IntPtr.Zero) { NativeMemory.AlignedFree(features.EM.ToPointer()); features.EM = IntPtr.Zero; }
        if (features.IT != IntPtr.Zero) { NativeMemory.AlignedFree(features.IT.ToPointer()); features.IT = IntPtr.Zero; }
        if (features.AL != IntPtr.Zero) { NativeMemory.AlignedFree(features.AL.ToPointer()); features.AL = IntPtr.Zero; }
        if (features.Zeroflow != IntPtr.Zero) { NativeMemory.AlignedFree(features.Zeroflow.ToPointer()); features.Zeroflow = IntPtr.Zero; }
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

