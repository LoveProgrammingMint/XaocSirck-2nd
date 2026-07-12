using PeNet;
namespace XaocSirck_Core.Feature.Engineering.Zeroflows;
internal sealed unsafe class PeStatistics : IDisposable
{
    private readonly ShareFeatures _features;
    private PeMetadata? _metadata;
    private PeExtended? _extended;
    private PeFile? _peFile;
    private String _filePath = String.Empty;
    private Int64 _fileSize;
    private Boolean _disposed;

    public PeStatistics(ShareFeatures features)
    {
        ArgumentNullException.ThrowIfNull(features);
        _features = features;
    }

    public void Register(PeMetadata metadata, PeExtended extended)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(extended);
        _metadata = metadata;
        _extended = extended;
    }

    public void LoadFromFile(String filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found.", filePath);

        _fileSize = new FileInfo(filePath).Length;
        if (!String.Equals(_filePath, filePath, StringComparison.OrdinalIgnoreCase))
        {
            _filePath = filePath;
            _peFile = new PeFile(filePath);
        }
    }

    public void Execute()
    {
        if (_metadata == null || _extended == null)
            return;

        _metadata.Clear();
        _extended.Clear();

        if (_peFile == null)
            return;

        _metadata.Process(_peFile, _fileSize);
        _extended.Process(_peFile, _fileSize);
    }

    public void Clear()
    {
        _peFile = null;
        _filePath = String.Empty;
        _fileSize = 0;
        _features.Clear();
        _metadata?.Clear();
        _extended?.Clear();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _peFile = null;
            _metadata?.Dispose();
            _extended?.Dispose();
            _metadata = null;
            _extended = null;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
