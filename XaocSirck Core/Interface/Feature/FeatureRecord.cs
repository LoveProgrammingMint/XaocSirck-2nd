namespace XaocSirck_Core.Interface.Feature;

public sealed class FeatureRecord
{
    public String Hash { get; set; } = String.Empty;
    public Single[] RB { get; set; } = [];
    public Single[] EM { get; set; } = [];
    public Single[] IT { get; set; } = [];
    public Single[] AL { get; set; } = [];
    public Single[] ZF { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}
