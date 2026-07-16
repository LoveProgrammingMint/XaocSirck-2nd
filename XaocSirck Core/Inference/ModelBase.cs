namespace XaocSirck_Core.Inference;

internal abstract class ModelBase : IModelInterface
{
    public abstract Boolean EnableGpu { get; set; }
    public abstract void Load(String modelPath);
    public abstract Single[] Run(IntPtr data, Int64[] shape);
    public abstract void Dispose();
}
