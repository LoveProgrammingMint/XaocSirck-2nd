using XaocSirck_Core.Interface.Cloud;

namespace XaocSirck_Core.Interface.Engine;

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
