using System.Text.Json;
using XaocSirck_Core.Interface.Engine;
using XaocSirck_Core.Interface.Settings;

namespace XaocSirck_Core.Engine;

public sealed class Settings
{
    private readonly Lock _lock = new();
    private EngineSettings _config = new();
    private String _filePath = Path.Combine(AppContext.BaseDirectory, "configs.json");

    public Settings()
    {
        Load();
    }

    public EngineSettings Config
    {
        get
        {
            lock (_lock)
            {
                return _config;
            }
        }
    }

    public String Version => Config.Version;
    public Boolean ParticipateInCoConstruction => Config.ParticipateInCoConstruction;
    public String ModelsDirectory => Config.ModelsDirectory;
    public Boolean EnableGpu => Config.EnableGpu;
    public Int32 ThreadCount => Config.ThreadCount;
    public Boolean FilterByExtension => Config.FilterByExtension;
    public String[] TargetExtensions => Config.TargetExtensions;
    public _Mode_Bitremal BitremalMode => Config.BitremalMode;
    public _Mode_Zeroflows ZeroflowMode => Config.ZeroflowMode;
    public _Mode_Signature SignatureMode => Config.SignatureMode;
    public _Mode_Archive ArchiveMode => Config.ArchiveMode;
    public _Mode_Documentation DocumentationMode => Config.DocumentationMode;
    public _Mode_Shell ShellMode => Config.ShellMode;
    public _Mode_Charwolf CharwolfMode => Config.CharwolfMode;
    public Boolean EnableCloudCache => Config.EnableCloudCache;
    public String CloudServerAddress => Config.CloudServerAddress;
    public String UpdateServerAddress => Config.UpdateServerAddress;
    public Int32 MaxFiles => Config.MaxFiles;
    public Boolean Recursive => Config.Recursive;
    public Int32 QueueCapacity => Config.QueueCapacity;
    public Int32 MaxDirectoryDepth => Config.MaxDirectoryDepth;
    public String LogDirectory => Config.LogDirectory;
    public Boolean EnableFeatureCache => Config.EnableFeatureCache;
    public Boolean EnableLogging => Config.EnableLogging;

    public void Load(String? path = null)
    {
        lock (_lock)
        {
            if (!String.IsNullOrEmpty(path))
                _filePath = path;

            if (!File.Exists(_filePath))
            {
                App.Logger.Info($"Configuration file not found, creating default: {_filePath}");
                _config = new EngineSettings();
                SaveLocked();
                return;
            }

            try
            {
                String json = File.ReadAllText(_filePath);
                _config = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.EngineSettings) ?? new EngineSettings();
                App.Logger.Enabled = _config.EnableLogging;
                App.Logger.Info($"Configuration loaded: {_filePath}");
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Configuration load failed, using defaults: {_filePath}", ex);
                _config = new EngineSettings();
                SaveLocked();
            }
        }
    }

    public void ReLoad()
    {
        Load();
    }

    public void Save()
    {
        lock (_lock)
        {
            SaveLocked();
        }
    }

    public void Update(Action<EngineSettings> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (_lock)
        {
            action(_config);
            App.Logger.Enabled = _config.EnableLogging;
            SaveLocked();
        }
    }

    private void SaveLocked()
    {
        try
        {
            String? dir = Path.GetDirectoryName(_filePath);
            if (!String.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            String json = JsonSerializer.Serialize(_config, SettingsJsonContext.Default.EngineSettings);
            File.WriteAllText(_filePath, json);
            App.Logger.Info($"Configuration saved: {_filePath}");
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Configuration save failed: {_filePath}", ex);
        }
    }
}
