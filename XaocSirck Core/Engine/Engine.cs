using XaocSirck_Core.Cloud;
using XaocSirck_Core.Core.Queues;
using XaocSirck_Core.Inference;
using XaocSirck_Core.Interface.Engine;
using XaocSirck_Core.Interface.Settings;
using ITimer = XaocSirck_Core.Interface.Engine.ITimer;

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
    public ITimer? Timer => _queue?.Timer;
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
            try { _bitremal.Load(modelsDirectory); }
            catch (Exception ex) { App.Logger.Error($"Bitremal model load failed from {modelsDirectory}", ex); }
            try { _zeroflows.Load(modelsDirectory); }
            catch (Exception ex) { App.Logger.Error($"Zeroflows model load failed from {modelsDirectory}", ex); }
        }
        else
        {
            App.Logger.Warning($"Models directory not found: {modelsDirectory}");
        }

        if (_settings.EnableCloudCache && !String.IsNullOrEmpty(_settings.CloudServerAddress))
        {
            try
            {
                _cloud.Connect(_settings.CloudServerAddress);
                App.Logger.Info($"Cloud cache connected: {_settings.CloudServerAddress}");
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Cloud cache connection failed: {_settings.CloudServerAddress}", ex);
            }
        }

        if (!String.IsNullOrEmpty(_settings.UpdateServerAddress))
        {
            try
            {
                _update.Connect(_settings.UpdateServerAddress);
                App.Logger.Info($"Update client connected: {_settings.UpdateServerAddress}");
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Update client connection failed: {_settings.UpdateServerAddress}", ex);
            }
        }

        if (_settings.CharwolfMode != _Mode_Charwolf.Disabled)
        {
            String rulesDirectory = Path.Combine(App.RuntimeDirectory, "Rules");
            if (Directory.Exists(rulesDirectory))
            {
                try
                {
                    _charwolf.LoadRules(rulesDirectory);
                    App.Logger.Info($"Charwolf rules loaded from {rulesDirectory}");
                }
                catch (Exception ex) { App.Logger.Error($"Charwolf rules load failed from {rulesDirectory}", ex); }
            }
            else
            {
                App.Logger.Warning($"Rules directory not found: {rulesDirectory}");
            }
        }

        _initialized = true;
        App.Logger.Info("Engine initialized");
    }

    public ScanResult[] Scan(String path, EngineMode? mode = null, Int32 maxFiles = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(Engine));
        if (!_initialized)
            Initialize();

        EngineMode scanMode = mode ?? BuildMode();
        Int32 capacity = _settings.QueueCapacity > 0 ? _settings.QueueCapacity : 1024;
        Int32 files = maxFiles > 0 ? maxFiles : _settings.MaxFiles;

        App.Logger.Info($"Scan started: {path}, mode={scanMode}, maxFiles={files}");

        try
        {
            _queue?.Dispose();
            _queue = new MainQueue(_cloud, _bitremal, _zeroflows, _charwolf, _settings.Config, capacity);
            _queue.StartAndWait(path, scanMode, _settings.Recursive, files);
            ScanResult[] results = [.. _queue.Results];
            App.Logger.Info($"Scan completed: {path}, results={results.Length}");
            return results;
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Scan failed: {path}", ex);
            throw;
        }
    }

    public async Task<ScanResult[]> ScanAsync(String path, EngineMode? mode = null, Int32 maxFiles = 0, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(Engine));
        if (!_initialized)
            Initialize();

        EngineMode scanMode = mode ?? BuildMode();
        Int32 capacity = _settings.QueueCapacity > 0 ? _settings.QueueCapacity : 1024;
        Int32 files = maxFiles > 0 ? maxFiles : _settings.MaxFiles;

        App.Logger.Info($"Async scan started: {path}, mode={scanMode}, maxFiles={files}");

        try
        {
            _queue?.Dispose();
            _queue = new MainQueue(_cloud, _bitremal, _zeroflows, _charwolf, _settings.Config, capacity);
            _queue.Start(path, scanMode, _settings.Recursive, files);
            using (cancellationToken.Register(() => _queue.Stop()))
            {
                await Task.Run(() => _queue.Wait(), cancellationToken);
            }
            ScanResult[] results = [.. _queue.Results];
            App.Logger.Info($"Async scan completed: {path}, results={results.Length}");
            return results;
        }
        catch (OperationCanceledException)
        {
            App.Logger.Warning($"Async scan cancelled: {path}");
            throw;
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Async scan failed: {path}", ex);
            throw;
        }
    }

    public void Stop()
    {
        _queue?.Stop();
    }

    public String? CheckForUpdate()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(Engine));
        if (!_update.IsConnected)
        {
            App.Logger.Warning("Update client is not connected.");
            return null;
        }
        try
        {
            String? version = _update.CheckVersion();
            App.Logger.Info($"Update check result: {version ?? "no update"}");
            return version;
        }
        catch (Exception ex)
        {
            App.Logger.Error("Check for update failed", ex);
            throw;
        }
    }

    public void DownloadUpdate(String? outputPath = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(Engine));
        if (!_update.IsConnected)
        {
            App.Logger.Warning("Update client is not connected.");
            return;
        }
        try
        {
            if (!String.IsNullOrEmpty(outputPath))
                _update.Download(outputPath);
            else
                _update.Download();
            App.Logger.Info($"Update downloaded to {_update.PackagePath}");
        }
        catch (Exception ex)
        {
            App.Logger.Error("Download update failed", ex);
            throw;
        }
    }

    public void ApplyUpdate(String? serviceName = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(Engine));
        try
        {
            _update.Apply(serviceName);
            App.Logger.Info($"Update applied, service={serviceName ?? "none"}");
        }
        catch (Exception ex)
        {
            App.Logger.Error("Apply update failed", ex);
            throw;
        }
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
            App.Logger.Info("Engine disposed");
        }
    }

    public EngineMode BuildMode()
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
