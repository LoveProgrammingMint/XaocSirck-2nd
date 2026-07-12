using System.Runtime.InteropServices;
using XaocSirck_Core.Interface.Feature.Zeroflows;
namespace XaocSirck_Core.Feature.Engineering.Zeroflows;
internal sealed unsafe class BytePatterns : IByteEngineering, IDisposable
{
    private const Int32 ResultSize = 1;
    private const Int32 MaxScanLength = 16 * 1024;

    private readonly ShareFeatures _share;
    private readonly Int32 _offset;
    private Int32* _zBuffer;
    private Int32 _headFilled;
    private Boolean _disposed;

    public BytePatterns(ShareFeatures share, Int32 offset = FeatureLayout.BytePatternsOffset)
    {
        ArgumentNullException.ThrowIfNull(share);
        _share = share;
        _offset = offset;
        _zBuffer = (Int32*)NativeMemory.AlignedAlloc((UIntPtr)(MaxScanLength * sizeof(Int32)), 64);
    }

    public void ProcessHead(Byte* data, Int32 length)
    {
        _headFilled = length;
    }

    public void ProcessTail(Byte* data, Int32 length)
    {
    }

    public void Complete()
    {
        Single* output = _share.FeatureTensor + _offset;
        output[0] = LongestRepeatedSubstring(_share.HeadPtr, _headFilled);
    }

    public void Clear()
    {
        _headFilled = 0;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_zBuffer != null) NativeMemory.AlignedFree(_zBuffer);
            _zBuffer = null;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    private Single LongestRepeatedSubstring(Byte* data, Int32 length)
    {
        if (length < 2)
            return 0.0f;

        Int32 scanLength = Math.Min(length, MaxScanLength);
        Int32* z = _zBuffer;
        NativeMemory.Clear(z, (UIntPtr)(scanLength * sizeof(Int32)));
        Int32 left = 0;
        Int32 right = 0;
        Int32 maxZ = 0;
        Byte* end = data + scanLength;

        for (Int32 i = 1; i < scanLength; i++)
        {
            if (i < right)
            {
                Int32 window = right - i;
                Int32 mirror = z[i - left];
                z[i] = mirror < window ? mirror : window;
            }

            Int32 match = z[i];
            Byte* a = data + match;
            Byte* b = data + i + match;

            while (b + 8 <= end)
            {
                if (*(UInt64*)a != *(UInt64*)b)
                    break;
                a += 8;
                b += 8;
                match += 8;
            }
            while (b < end && *a == *b)
            {
                a++;
                b++;
                match++;
            }

            z[i] = match;
            if (i + match > right)
            {
                left = i;
                right = i + match;
            }
            if (match > maxZ)
                maxZ = match;
        }

        return (Single)maxZ;
    }
}
