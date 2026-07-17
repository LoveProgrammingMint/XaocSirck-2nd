using System.Security.Cryptography;
using PeNet;
using XaocSirck_Core.Cloud;
using XaocSirck_Core.Engine;
using XaocSirck_Core.Feature;
using XaocSirck_Core.Inference;
using XaocSirck_Core.Interface.Engine;

namespace XaocSirck_Core.Core.Queues;

internal sealed class MainQueue : SPSC<MainQueue.TaskItem>, IDisposable
{
    private readonly CloudClient _cloud;
    private readonly BitremalInferenceService? _inference;
    private readonly ZeroflowsInferenceService? _zeroflows;
    private readonly ICharwolfEngine? _charwolf;
    private readonly EngineSettings _settings;
    private readonly Features _features;
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
            _archive.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
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
            Byte[] sha256 = ComputeSha256(item.FilePath);

            CloudCacheResult cacheResult = CloudCacheResult.Error;
            if (_settings.EnableCloudCache && _cloud.IsConnected)
            {
                try { cacheResult = _cloud.QueryCache(sha256); }
                catch { }
            }

            SignatureResult? signatureResult = null;
            if (mode.Signature != _Mode_Signature.Disabled)
            {
                try { signatureResult = CheckSignature(item.FilePath, sha256, mode.Signature); }
                catch { }
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
                try { charwolfResult = _charwolf.ScanFile(item.FilePath); }
                catch { }
            }

            ShellResult? shellResult = null;
            if (mode.Shell != _Mode_Shell.Disabled)
            {
                try { shellResult = CheckShell(item.FilePath); }
                catch { }
            }

            ArchiveResult? archiveResult = null;
            if (mode.Archive != _Mode_Archive.Disabled)
            {
                try { archiveResult = CheckArchive(item.FilePath); }
                catch { }
            }

            DocumentationResult? documentationResult = null;
            if (mode.Documentation != _Mode_Documentation.Disabled)
            {
                try { documentationResult = CheckDocumentation(item.FilePath); }
                catch { }
            }

            _features.Set(item.FilePath, mode);
            FeaturesStruct features = _features.Execute(null);

            Single[]? bitremalProbs = null;
            Single[]? zeroflowsProbs = null;

            if (mode.Bitremal != _Mode_Bitremal.Disabled && _inference != null)
            {
                try { bitremalProbs = _inference.InferOverThink(features.AL, features.RB, features.IT, features.EM); }
                catch { }
            }

            if (mode.Zeroflow != _Mode_Zeroflows.Disabled && _zeroflows != null && features.Zeroflow != IntPtr.Zero)
            {
                try { zeroflowsProbs = _zeroflows.Infer(features.Zeroflow); }
                catch { }
            }

            AddResult(item.FilePath, cacheResult, bitremalProbs, zeroflowsProbs, charwolfResult, signatureResult, shellResult, archiveResult, documentationResult);
        }
        catch
        {
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

public sealed class ScanResult
{
    public String FilePath { get; }
    public CloudCacheResult CacheResult { get; }
    public Single[]? BitremalProbabilities { get; }
    public Single[]? ZeroflowsProbabilities { get; }
    public CharwolfScanResult? CharwolfResult { get; }
    public SignatureResult? SignatureResult { get; }
    public ShellResult? ShellResult { get; }
    public ArchiveResult? ArchiveResult { get; }
    public DocumentationResult? DocumentationResult { get; }

    public ScanResult(String filePath, CloudCacheResult cacheResult, Single[]? bitremalProbabilities, Single[]? zeroflowsProbabilities, CharwolfScanResult? charwolfResult, SignatureResult? signatureResult = null, ShellResult? shellResult = null, ArchiveResult? archiveResult = null, DocumentationResult? documentationResult = null)
    {
        FilePath = filePath;
        CacheResult = cacheResult;
        BitremalProbabilities = bitremalProbabilities;
        ZeroflowsProbabilities = zeroflowsProbabilities;
        CharwolfResult = charwolfResult;
        SignatureResult = signatureResult;
        ShellResult = shellResult;
        ArchiveResult = archiveResult;
        DocumentationResult = documentationResult;
    }

    public Single[] Probabilities => BitremalProbabilities ?? ZeroflowsProbabilities ?? [1.0f, 0.0f];

    public Boolean IsMalicious
    {
        get
        {
            if (CacheResult == CloudCacheResult.Hit)
                return true;
            if (SignatureResult is { IsSigned: true, IsTrusted: false })
                return true;
            if (CharwolfResult?.Matched == true)
                return true;
            if (ShellResult?.Hit != ShellHits.Emtpy && ShellResult?.Hit != null)
                return true;
            if (ArchiveResult?.SuspiciousEntryCount > 0)
                return true;
            if (DocumentationResult?.HasMacro == true)
                return true;
            if (BitremalProbabilities is { Length: >= 2 } && BitremalProbabilities[1] > BitremalProbabilities[0])
                return true;
            if (ZeroflowsProbabilities is { Length: >= 2 } && ZeroflowsProbabilities[1] > ZeroflowsProbabilities[0])
                return true;
            return false;
        }
    }
}

public sealed class SignatureResult
{
    public String FilePath { get; set; } = String.Empty;
    public Boolean IsSigned { get; set; }
    public Boolean IsLocallyTrusted { get; set; }
    public Boolean IsTrusted { get; set; }
    public CloudSignatureResult CloudResult { get; set; } = CloudSignatureResult.Error;
}

public sealed class ShellResult
{
    public String FilePath { get; set; } = String.Empty;
    public ShellHits Hit { get; set; } = ShellHits.Emtpy;
}

public sealed class ArchiveResult
{
    public String FilePath { get; set; } = String.Empty;
    public Boolean IsArchive { get; set; }
    public Int32 EntryCount { get; set; }
    public Int32 SuspiciousEntryCount { get; set; }
}

public sealed class DocumentationResult
{
    public String FilePath { get; set; } = String.Empty;
    public Boolean HasMacro { get; set; }
}
