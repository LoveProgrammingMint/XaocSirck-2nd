using System.Text.Json;
using System.Text.Json.Serialization;
using XaocSirck_Core.Interface.Engine;

namespace XaocSirck_Core.Engine;

public sealed class EngineSettings
{
    public String Version { get; set; } = "1.0.0";
    public Boolean ParticipateInCoConstruction { get; set; }

    public String ModelsDirectory { get; set; } = Path.Combine(App.RuntimeDirectory, "Models");
    public Boolean EnableGpu { get; set; }
    public Int32 ThreadCount { get; set; }

    public Boolean FilterByExtension { get; set; } = true;
    public String[] TargetExtensions { get; set; } = [".exe", ".dll", ".sys", ".drv", ".ocx", ".cpl", ".scr", ".efi", ".com"];

    public _Mode_Bitremal BitremalMode { get; set; } = _Mode_Bitremal.Ot;
    public _Mode_Zeroflows ZeroflowMode { get; set; } = _Mode_Zeroflows.Disabled;
    public _Mode_Signature SignatureMode { get; set; } = _Mode_Signature.Disabled;
    public _Mode_Archive ArchiveMode { get; set; } = _Mode_Archive.Disabled;
    public _Mode_Documentation DocumentationMode { get; set; } = _Mode_Documentation.Disabled;
    public _Mode_Shell ShellMode { get; set; } = _Mode_Shell.Disabled;
    public _Mode_Charwolf CharwolfMode { get; set; } = _Mode_Charwolf.Disabled;

    public Boolean EnableCloudCache { get; set; }
    public String CloudServerAddress { get; set; } = String.Empty;
    public String UpdateServerAddress { get; set; } = String.Empty;

    public Int32 MaxFiles { get; set; }
    public Boolean Recursive { get; set; } = true;
    public Int32 QueueCapacity { get; set; } = 1024;
    public Int32 MaxDirectoryDepth { get; set; } = 256;
    public String LogDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "Logs");
}

[JsonSerializable(typeof(EngineSettings))]
public partial class SettingsJsonContext : JsonSerializerContext { }

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

    public void Load(String? path = null)
    {
        lock (_lock)
        {
            if (!String.IsNullOrEmpty(path))
                _filePath = path;

            if (!File.Exists(_filePath))
            {
                _config = new EngineSettings();
                SaveLocked();
                return;
            }

            try
            {
                String json = File.ReadAllText(_filePath);
                _config = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.EngineSettings) ?? new EngineSettings();
            }
            catch
            {
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
        }
        catch { }
    }
}
