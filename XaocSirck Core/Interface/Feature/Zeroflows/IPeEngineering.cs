using PeNet;

namespace XaocSirck_Core.Interface.Feature.Zeroflows;

internal interface IPeEngineering : IDisposable
{
    void Clear();
    void Process(PeFile pe, Int64 fileSize);
}
