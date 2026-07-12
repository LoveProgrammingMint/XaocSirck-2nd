using System.Runtime.InteropServices;

using XaocSirck_Core.Interface.Feature.Zeroflows;
namespace XaocSirck_Core.Feature.Engineering.Zeroflows;
internal sealed unsafe class ByteHistogram : IByteEngineering, IDisposable
{
    private const Int32 BucketCount = 16;
    private const Int32 ResultSize = 32;

    private readonly ShareFeatures _share;
    private readonly Int32 _offset;
    private Int32* _headBuckets;
    private Int32* _tailBuckets;
    private Int32 _headFilled;
    private Int32 _tailFilled;
    private Boolean _disposed;

    public ByteHistogram(ShareFeatures share, Int32 offset = FeatureLayout.ByteHistogramOffset)
    {
        ArgumentNullException.ThrowIfNull(share);
        _share = share;
        _offset = offset;
        _headBuckets = (Int32*)NativeMemory.AlignedAlloc((UIntPtr)(BucketCount * sizeof(Int32)), 64);
        _tailBuckets = (Int32*)NativeMemory.AlignedAlloc((UIntPtr)(BucketCount * sizeof(Int32)), 64);
    }

    public void ProcessHead(Byte* data, Int32 length)
    {
        _headFilled = length;
        Int32* buckets = _headBuckets;
        Byte* p = data;
        Byte* end = data + length;
        while (p < end)
            buckets[*p++ >> 4]++;
    }

    public void ProcessTail(Byte* data, Int32 length)
    {
        _tailFilled = length;
        Int32* buckets = _tailBuckets;
        Byte* p = data;
        Byte* end = data + length;
        while (p < end)
            buckets[*p++ >> 4]++;
    }

    public void Complete()
    {
        Single* output = _share.FeatureTensor + _offset;
        NativeMemory.Clear(output, (UIntPtr)(ResultSize * sizeof(Single)));
        if (_headFilled > 0)
        {
            Single invHead = 1.0f / _headFilled;
            for (Int32 i = 0; i < BucketCount; i++)
                output[i] = _headBuckets[i] * invHead;
        }
        if (_tailFilled > 0)
        {
            Single invTail = 1.0f / _tailFilled;
            for (Int32 i = 0; i < BucketCount; i++)
                output[BucketCount + i] = _tailBuckets[i] * invTail;
        }
    }

    public void Clear()
    {
        _headFilled = 0;
        _tailFilled = 0;
        NativeMemory.Clear(_headBuckets, (UIntPtr)(BucketCount * sizeof(Int32)));
        NativeMemory.Clear(_tailBuckets, (UIntPtr)(BucketCount * sizeof(Int32)));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_headBuckets != null) NativeMemory.AlignedFree(_headBuckets);
            if (_tailBuckets != null) NativeMemory.AlignedFree(_tailBuckets);
            _headBuckets = null;
            _tailBuckets = null;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
