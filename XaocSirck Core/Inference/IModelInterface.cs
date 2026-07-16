namespace XaocSirck_Core.Inference;

internal interface IModelInterface : IDisposable
{
    public Boolean EnableGpu { get; set; }
    public void Load(String modelPath);
    public Single[] Run(IntPtr data, Int64[] shape);
}
