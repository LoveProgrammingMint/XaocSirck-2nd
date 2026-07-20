using XaocSirck_Core.Interface.Inference;
using XaocSirck_Core.Native;

namespace XaocSirck_Core.Inference;

internal sealed class ModelManager : IModelManagerInterface
{
    private readonly IntPtr _sessionManagement;
    private readonly Dictionary<String, OnnxModel> _models = new();
    private Boolean _disposed;

    public Boolean EnableGpu { get; set; }

    public ModelManager()
    {
        _sessionManagement = InferenceService.XaocSirckSessionManagementCreate();
        if (_sessionManagement == IntPtr.Zero)
        {
            App.Logger.Error("Native session management creation failed");
            throw new InvalidOperationException("Failed to create native session management.");
        }
    }

    public OnnxModel Load(String name, String modelPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(ModelManager));
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(modelPath);

        if (_models.TryGetValue(name, out OnnxModel? existingModel))
        {
            existingModel.Dispose();
            _models.Remove(name);
        }

        if (_models.Count == 0)
            InferenceService.XaocSirckSessionManagementSwitchDevice(_sessionManagement, EnableGpu ? "Gpu" : "Cpu");

        OnnxModel model = new(_sessionManagement, name) { EnableGpu = EnableGpu };
        model.Load(modelPath);
        _models[name] = model;
        return model;
    }

    public OnnxModel Get(String name) =>
        _models.TryGetValue(name, out OnnxModel? model)
            ? model
            : throw new KeyNotFoundException($"Model '{name}' is not loaded.");

    public Boolean TryGet(String name, out OnnxModel? model) => _models.TryGetValue(name, out model);

    public IEnumerable<KeyValuePair<String, OnnxModel>> GetAll() => _models;

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (OnnxModel model in _models.Values)
                model.Dispose();
            _models.Clear();

            if (_sessionManagement != IntPtr.Zero)
                InferenceService.XaocSirckSessionManagementDestroy(_sessionManagement);

            _disposed = true;
            GC.SuppressFinalize(this);
            App.Logger.Info("ModelManager disposed");
        }
    }
}
