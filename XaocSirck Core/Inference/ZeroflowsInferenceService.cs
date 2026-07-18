using XaocSirck_Core.Interface.Inference;

namespace XaocSirck_Core.Inference;

internal sealed class ZeroflowsInferenceService : IDisposable
{
    private const Int32 FeatureSize = 256;
    private const Int32 MetaInputSize = 260;

    private readonly ModelManager _models;
    private readonly HashSet<String> _loaded = new();
    private Boolean _disposed;

    public ZeroflowsInferenceService(Boolean enableGpu = false)
    {
        _models = new ModelManager { EnableGpu = enableGpu };
    }

    public Boolean IsLoaded => _loaded.Count == 3;

    public ModelManager Models => _models;

    public void Load(String modelsDirectory)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(ZeroflowsInferenceService));
        ArgumentNullException.ThrowIfNull(modelsDirectory);

        LoadIfExists("zeroflows_meta", Path.Combine(modelsDirectory, "Zeroflows", "meta.onnx"));
        LoadIfExists("zeroflows_cb", Path.Combine(modelsDirectory, "Zeroflows", "cb.onnx"));
        LoadIfExists("zeroflows_lgb", Path.Combine(modelsDirectory, "Zeroflows", "lgb.onnx"));

        App.Logger.Info($"Zeroflows loaded {_loaded.Count} models from {modelsDirectory}");
    }

    public Single[] Infer(IntPtr zeroflowInput)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(ZeroflowsInferenceService));
        EnsureLoaded(["zeroflows_meta", "zeroflows_cb", "zeroflows_lgb"]);

        Single[] metaInput = GC.AllocateUninitializedArray<Single>(MetaInputSize);
        unsafe
        {
            fixed (Single* dst = metaInput)
            {
                Buffer.MemoryCopy((void*)zeroflowInput, dst, (UIntPtr)(FeatureSize * sizeof(Single)), (UIntPtr)(FeatureSize * sizeof(Single)));
                dst[FeatureSize + 0] = 0.0f;
                dst[FeatureSize + 1] = 0.0f;
                dst[FeatureSize + 2] = 0.0f;
                dst[FeatureSize + 3] = 0.0f;
            }
        }

        Single[] meta = _models.Get("zeroflows_meta").Run(metaInput, [1, MetaInputSize]);
        Single[] cb = RunModelToArray(_models.Get("zeroflows_cb"), zeroflowInput, [1, FeatureSize]);
        Single[] lgb = RunModelToArray(_models.Get("zeroflows_lgb"), zeroflowInput, [1, FeatureSize]);

        Single[] probs = GC.AllocateUninitializedArray<Single>(2);
        for (Int32 i = 0; i < 2; i++)
        {
            Single sum = 0.0f;
            if (meta.Length > i) sum += meta[i];
            if (cb.Length > i) sum += cb[i];
            if (lgb.Length > i) sum += lgb[i];
            probs[i] = sum / 3.0f;
        }
        return probs;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _models.Dispose();
            _loaded.Clear();
            _disposed = true;
            GC.SuppressFinalize(this);
            App.Logger.Info("ZeroflowsInferenceService disposed");
        }
    }

    private void LoadIfExists(String name, String path)
    {
        if (!File.Exists(path))
        {
            App.Logger.Warning($"Zeroflows model missing: {path}");
            return;
        }
        try
        {
            _models.Load(name, path);
            _loaded.Add(name);
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Zeroflows model load failed: {name} ({path})", ex);
        }
    }

    private void EnsureLoaded(String[] names)
    {
        foreach (String name in names)
            if (!_loaded.Contains(name))
                throw new InvalidOperationException($"Model '{name}' is not loaded. Call Load first.");
    }

    private static Single[] RunModelToArray(OnnxModel model, IntPtr input, Int64[] shape)
    {
        Single[] output = model.Run(input, shape);
        return output;
    }
}
