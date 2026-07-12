namespace XaocSirck_Core.Interface.Feature.Zeroflows;

internal unsafe interface IByteEngineering : IDisposable
{
    void Clear();
    void ProcessHead(Byte* data, Int32 length);
    void ProcessTail(Byte* data, Int32 length);
    void Complete();
}
