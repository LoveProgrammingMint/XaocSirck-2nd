using System.Text.Json.Serialization;
using XaocSirck_Core.Interface.Engine;

namespace XaocSirck_Core.Interface.Settings;

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
    public String LogDirectory { get; set; } = Path.Combine(App.RuntimeDirectory, "Logs");
    public Boolean EnableTiming { get; set; }
    public Boolean EnableFeatureCache { get; set; } = true;
    public Boolean EnableLogging { get; set; } = true;
}

[JsonSerializable(typeof(EngineSettings))]
public partial class SettingsJsonContext : JsonSerializerContext { }
