using XaocSirck_Core.Cloud;
using XaocSirck_Core.Core.Queues;
using XaocSirck_Core.Inference;
using XaocSirck_Core.Interface.Engine;

namespace XaocSirck_Core.Engine;

public sealed class Engine : IDisposable
{
    private readonly Settings _settings;
    private readonly CloudClient _cloud;
    private readonly UpdateClient _update;
    private readonly BitremalInferenceService _bitremal;
    private readonly ZeroflowsInferenceService _zeroflows;
    private readonly CharwolfEngineService _charwolf;
    private MainQueue? _queue;
    private Boolean _disposed;
    private Boolean _initialized;

    public Engine(Settings? settings = null)
    {
        _settings = settings ?? App.Settings;
        _cloud = new CloudClient();
        _update = new UpdateClient();
        _bitremal = new BitremalInferenceService(_settings.EnableGpu);
        _zeroflows = new ZeroflowsInferenceService(_settings.EnableGpu);
        _charwolf = new CharwolfEngineService();
    }

    public Settings Settings => _settings;
    public CloudClient Cloud => _cloud;
    public UpdateClient Update => _update;
    public Boolean IsInitialized => _initialized;
    public Boolean IsBitremalLoaded => _bitremal.IsLoaded;
    public Boolean IsZeroflowsLoaded => _zeroflows.IsLoaded;
    public Boolean IsCharwolfLoaded => _charwolf.IsLoaded;

    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(Engine));
        if (_initialized)
            return;

        String modelsDirectory = _settings.ModelsDirectory;
        if (Directory.Exists(modelsDirectory))
        {
            try { _bitremal.Load(modelsDirectory); } catch { }
            try { _zeroflows.Load(modelsDirectory); } catch { }
        }

        if (_settings.EnableCloudCache && !String.IsNullOrEmpty(_settings.CloudServerAddress))
        {
            try { _cloud.Connect(_settings.CloudServerAddress); } catch { }
        }

        if (_settings.CharwolfMode != _Mode_Charwolf.Disabled)
        {
            String rulesDirectory = Path.Combine(App.RuntimeDirectory, "Rules");
            if (Directory.Exists(rulesDirectory))
            {
                try { _charwolf.LoadRules(rulesDirectory); } catch { }
            }
        }

        _initialized = true;
    }

    public String? CheckForUpdate()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(Engine));
        String address = _settings.UpdateServerAddress;
        if (String.IsNullOrEmpty(address))
            return null;

        Boolean ownConnection = !_update.IsConnected;
        try
        {
            if (ownConnection)
                _update.Connect(address);
            return _update.CheckVersion();
        }
        catch
        {
            return null;
        }
        finally
        {
            if (ownConnection)
                _update.Disconnect();
        }
    }

    public void DownloadUpdate()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(Engine));
        String address = _settings.UpdateServerAddress;
        if (String.IsNullOrEmpty(address))
            throw new InvalidOperationException("Update server address is not configured.");

        Boolean ownConnection = !_update.IsConnected;
        try
        {
            if (ownConnection)
                _update.Connect(address);
            _update.Download();
        }
        finally
        {
            if (ownConnection)
                _update.Disconnect();
        }
    }

    public void ApplyUpdate(String? serviceName = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(Engine));
        _update.Apply(serviceName);
    }

    public ScanResult[] Scan(String path, EngineMode? mode = null, Int32 maxFiles = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(Engine));
        if (!_initialized)
            Initialize();

        EngineMode scanMode = mode ?? BuildMode();
        Int32 capacity = _settings.QueueCapacity > 0 ? _settings.QueueCapacity : 1024;
        Int32 files = maxFiles > 0 ? maxFiles : _settings.MaxFiles;

        _queue?.Dispose();
        _queue = new MainQueue(_cloud, _bitremal, _zeroflows, _charwolf, _settings.Config, capacity);

        _queue.StartAndWait(path, scanMode, _settings.Recursive, files);
        ScanResult[] results = [.. _queue.Results];
        return results;
    }

    public async Task<ScanResult[]> ScanAsync(String path, EngineMode? mode = null, Int32 maxFiles = 0, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(Engine));
        if (!_initialized)
            Initialize();

        EngineMode scanMode = mode ?? BuildMode();
        Int32 capacity = _settings.QueueCapacity > 0 ? _settings.QueueCapacity : 1024;
        Int32 files = maxFiles > 0 ? maxFiles : _settings.MaxFiles;

        _queue?.Dispose();
        _queue = new MainQueue(_cloud, _bitremal, _zeroflows, _charwolf, _settings.Config, capacity);

        _queue.Start(path, scanMode, _settings.Recursive, files);
        using (cancellationToken.Register(() => _queue.Stop()))
        {
            await Task.Run(() => _queue.Wait(), cancellationToken);
        }
        ScanResult[] results = [.. _queue.Results];
        return results;
    }

    public void Stop()
    {
        _queue?.Stop();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _queue?.Dispose();
            _cloud.Dispose();
            _update.Dispose();
            _bitremal.Dispose();
            _zeroflows.Dispose();
            _charwolf.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    private EngineMode BuildMode()
    {
        return new EngineMode
        {
            Bitremal = _settings.BitremalMode,
            Zeroflow = _settings.ZeroflowMode,
            Signature = _settings.SignatureMode,
            Charwolf = _settings.CharwolfMode,
            Archive = _settings.ArchiveMode,
            Documentation = _settings.DocumentationMode,
            Shell = _settings.ShellMode,
            Engines = new _Mode_Engines
            {
                Signature = _settings.SignatureMode,
                Archive = _settings.ArchiveMode,
                Documentation = _settings.DocumentationMode,
                Shell = _settings.ShellMode
            }
        };
    }
}
