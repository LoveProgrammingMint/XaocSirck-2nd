using System.Runtime.InteropServices;
namespace XaocSirck_Core.Feature.Engineering.Zeroflows;
internal sealed unsafe class ShareFeatures : IDisposable
{
    private const Int32 ByteHeadLength = 64 * 1024;
    private const Int32 ByteTailLength = 16 * 1024;
    private const Int32 TensorLength = 256;

    private Byte* _byteHead;
    private Byte* _byteTail;
    private Single* _featureTensor;
    private Boolean _disposed;

    public ShareFeatures()
    {
        _byteHead = (Byte*)NativeMemory.AlignedAlloc((UIntPtr)ByteHeadLength, 64);
        _byteTail = (Byte*)NativeMemory.AlignedAlloc((UIntPtr)ByteTailLength, 64);
        _featureTensor = (Single*)NativeMemory.AlignedAlloc((UIntPtr)(TensorLength * sizeof(Single)), 64);
    }

    public Int32 HeadLength => ByteHeadLength;
    public Int32 TailLength => ByteTailLength;
    public Int32 TensorSize => TensorLength;
    public Byte* HeadPtr => _byteHead;
    public Byte* TailPtr => _byteTail;
    public Span<Byte> HeadSpan => new(_byteHead, ByteHeadLength);
    public Span<Byte> TailSpan => new(_byteTail, ByteTailLength);
    public Single* FeatureTensor => _featureTensor;
    public Span<Single> TensorSpan => new(_featureTensor, TensorLength);

    public void Clear()
    {
        NativeMemory.Clear(_byteHead, (UIntPtr)ByteHeadLength);
        NativeMemory.Clear(_byteTail, (UIntPtr)ByteTailLength);
        NativeMemory.Clear(_featureTensor, (UIntPtr)(TensorLength * sizeof(Single)));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_byteHead != null) NativeMemory.AlignedFree(_byteHead);
            if (_byteTail != null) NativeMemory.AlignedFree(_byteTail);
            if (_featureTensor != null) NativeMemory.AlignedFree(_featureTensor);
            _byteHead = null;
            _byteTail = null;
            _featureTensor = null;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
