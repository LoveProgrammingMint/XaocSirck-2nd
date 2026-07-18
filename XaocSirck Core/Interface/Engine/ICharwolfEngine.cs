namespace XaocSirck_Core.Interface.Engine;

public sealed class CharwolfMatch
{
    public String RuleName { get; set; } = String.Empty;
    public Int32 StringId { get; set; }
    public String Part { get; set; } = String.Empty;
}

public sealed class CharwolfScanResult
{
    public String FilePath { get; set; } = String.Empty;
    public Boolean Matched { get; set; }
    public List<CharwolfMatch> Matches { get; set; } = [];
}

public interface ICharwolfEngine : IDisposable
{
    void LoadRules(String rulesDirectory);
    Boolean IsLoaded { get; }
    CharwolfScanResult ScanFile(String filePath);
}
