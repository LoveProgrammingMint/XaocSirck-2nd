using XaocSirck_Core.Interface.Inference;

namespace XaocSirck_Core.Inference;

internal sealed class BitremalInferenceService : IDisposable
{
    private const Int32 BackboneOutputSize = 128;
    private const Int32 OverThinkInputSize = 512;

    private readonly ModelManager _models;
    private readonly HashSet<String> _loaded = new();
    private Boolean _disposed;

    public BitremalInferenceService(Boolean enableGpu = false)
    {
        _models = new ModelManager { EnableGpu = enableGpu };
    }

    public Boolean IsLoaded => _loaded.Count == 7;

    public ModelManager Models => _models;

    public void Load(String modelsDirectory)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(BitremalInferenceService));
        ArgumentNullException.ThrowIfNull(modelsDirectory);

        LoadIfExists("albone", Path.Combine(modelsDirectory, "AssemblyList", "AL_backbone.onnx"));
        LoadIfExists("alclassifier", Path.Combine(modelsDirectory, "AssemblyList", "AL_head.onnx"));
        LoadIfExists("rbbone", Path.Combine(modelsDirectory, "RawByte", "RB_backbone.onnx"));
        LoadIfExists("rbclassifier", Path.Combine(modelsDirectory, "RawByte", "RB_head.onnx"));
        LoadIfExists("itbone", Path.Combine(modelsDirectory, "ImportTable", "IT_encoder.onnx"));
        LoadIfExists("embone", Path.Combine(modelsDirectory, "EntropyMap", "EM_encoder.onnx"));
        LoadIfExists("bitremal", Path.Combine(modelsDirectory, "Bitremal", "Bitremal.onnx"));

        App.Logger.Info($"Bitremal loaded {_loaded.Count} models from {modelsDirectory}");
    }

    public Single[] InferOnlyRB(IntPtr rawBytesInput)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(BitremalInferenceService));
        EnsureLoaded(["rbbone", "rbclassifier"]);

        Single[] bone = RunModelToArray(_models.Get("rbbone"), rawBytesInput, [1, 8, 128, 128]);
        Single[] logits = _models.Get("rbclassifier").Run(bone, [1, bone.Length]);
        SoftmaxInPlace(logits);
        return logits;
    }

    public Single[] InferOnlyAL(IntPtr assemblyListInput)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(BitremalInferenceService));
        EnsureLoaded(["albone", "alclassifier"]);

        Single[] bone = RunModelToArray(_models.Get("albone"), assemblyListInput, [1, 192, 512]);
        Single[] logits = _models.Get("alclassifier").Run(bone, [1, bone.Length]);
        SoftmaxInPlace(logits);
        return logits;
    }

    public Single[] InferOverThink(IntPtr alInput, IntPtr rbInput, IntPtr itInput, IntPtr emInput)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(BitremalInferenceService));
        EnsureLoaded(["albone", "rbbone", "itbone", "embone", "bitremal"]);

        Single[] al = RunModelToArray(_models.Get("albone"), alInput, [1, 192, 512]);
        Single[] rb = RunModelToArray(_models.Get("rbbone"), rbInput, [1, 8, 128, 128]);
        // IT buffer stores a 4-byte length prefix; skip it to reach the 417 feature values.
        Single[] it = RunModelToArray(_models.Get("itbone"), itInput + sizeof(Int32), [1, 417, 1]);
        Single[] em = RunModelToArray(_models.Get("embone"), emInput, [1, 64, 1]);

        Single[] combined = GC.AllocateUninitializedArray<Single>(OverThinkInputSize);
        rb.CopyTo(combined, 0);
        al.CopyTo(combined, BackboneOutputSize);
        em.CopyTo(combined, BackboneOutputSize * 2);
        it.CopyTo(combined, BackboneOutputSize * 3);

        Single[] logits = _models.Get("bitremal").Run(combined, [1, OverThinkInputSize]);
        SoftmaxInPlace(logits);
        return logits;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _models.Dispose();
            _loaded.Clear();
            _disposed = true;
            GC.SuppressFinalize(this);
            App.Logger.Info("BitremalInferenceService disposed");
        }
    }

    private void LoadIfExists(String name, String path)
    {
        if (!File.Exists(path))
        {
            App.Logger.Warning($"Bitremal model missing: {path}");
            return;
        }
        try
        {
            _models.Load(name, path);
            _loaded.Add(name);
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Bitremal model load failed: {name} ({path})", ex);
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

    private static void SoftmaxInPlace(Single[] logits)
    {
        for (Int32 i = 0; i < logits.Length; i++)
            if (Single.IsNaN(logits[i]) || Single.IsInfinity(logits[i]))
                logits[i] = 0.0f;

        Single max = logits[0];
        for (Int32 i = 1; i < logits.Length; i++)
            if (logits[i] > max)
                max = logits[i];

        Single sum = 0.0f;
        for (Int32 i = 0; i < logits.Length; i++)
        {
            logits[i] = MathF.Exp(logits[i] - max);
            sum += logits[i];
        }

        Single invSum = 1.0f / sum;
        for (Int32 i = 0; i < logits.Length; i++)
            logits[i] *= invSum;
    }
}
