using XaocSirck_Core.Feature;
using XaocSirck_Core.Interface.Feature;

namespace XaocSirck_Core.Engine.Feature_Cache;

internal sealed unsafe class FeatureCache : IDisposable
{
    private readonly DatabaseManagement _database = new(Path.Combine(App.RuntimeDirectory, "feature_cache.db"));
    private Boolean _disposed;

    public Int32 Count => _database.Count();

    public void Insert(String sha256, FeaturesStruct features)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(FeatureCache));
        ArgumentException.ThrowIfNullOrEmpty(sha256);

        (Single[] rbData, Single[] emData, Single[] itData, Single[] alData, Single[] zfData) = GetArray(features);
        _database.InsertOrUpdate(sha256, rbData, emData, itData, alData, zfData);
        App.Logger.Debug($"Feature cache inserted: {sha256}");
    }

    public FeatureRecord? Get(String sha256)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(FeatureCache));
        ArgumentException.ThrowIfNullOrEmpty(sha256);

        FeatureRecord? record = _database.Read(sha256);
        if (record != null)
            App.Logger.Debug($"Feature cache hit: {sha256}");
        return record;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _database.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
            App.Logger.Info("FeatureCache disposed");
        }
    }

    private static (Single[], Single[], Single[], Single[], Single[]) GetArray(FeaturesStruct features)
    {
        return (new Span<Single>((Single*)features.RB, DatabaseManagement.RbLength).ToArray(),
                new Span<Single>((Single*)features.EM, DatabaseManagement.EmLength).ToArray(),
                new Span<Single>((Single*)features.IT, DatabaseManagement.ItLength).ToArray(),
                new Span<Single>((Single*)features.AL, DatabaseManagement.AlLength).ToArray(),
                new Span<Single>((Single*)features.Zeroflow, DatabaseManagement.ZfLength).ToArray());
    }
}
