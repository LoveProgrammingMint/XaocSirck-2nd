namespace XaocSirck_Core.Inference;

internal interface IModelManagerInterface : IDisposable
{
    public Boolean EnableGpu { get; set; }
    public OnnxModel Load(String name, String modelPath);
    public OnnxModel Get(String name);
}
