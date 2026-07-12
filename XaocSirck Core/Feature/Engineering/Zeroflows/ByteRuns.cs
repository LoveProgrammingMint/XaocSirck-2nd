using System.Runtime.InteropServices;
using XaocSirck_Core.Interface.Feature.Zeroflows;
namespace XaocSirck_Core.Feature.Engineering.Zeroflows;
internal sealed unsafe class ByteRuns : IByteEngineering, IDisposable
{
    private const Int32 ResultSize = 5;
    private const Int32 BucketCount = 16;

    private readonly ShareFeatures _share;
    private readonly Int32 _offset;
    private Int32* _runBuckets;
    private Int32 _currentType;
    private Int32 _currentLength;
    private Int32 _maxZeroRun;
    private Int32 _maxNonzeroRun;
    private Int64 _zeroRunSum;
    private Int64 _nonzeroRunSum;
    private Int32 _zeroRunCount;
    private Int32 _nonzeroRunCount;
    private Boolean _wasTail;
    private Boolean _hasCurrent;
    private Boolean _disposed;

    public ByteRuns(ShareFeatures share, Int32 offset = FeatureLayout.ByteRunsOffset)
    {
        ArgumentNullException.ThrowIfNull(share);
        _share = share;
        _offset = offset;
        _runBuckets = (Int32*)NativeMemory.AlignedAlloc((UIntPtr)(BucketCount * sizeof(Int32)), 64);
    }

    public void ProcessHead(Byte* data, Int32 length)
    {
        ProcessSpan(data, length);
    }

    public void ProcessTail(Byte* data, Int32 length)
    {
        if (!_wasTail)
        {
            EndRun();
            _wasTail = true;
        }
        ProcessSpan(data, length);
    }

    private void ProcessSpan(Byte* data, Int32 length)
    {
        if (length == 0)
            return;

        Int32 currentType = _currentType;
        Int32 currentLength = _currentLength;
        Boolean hasCurrent = _hasCurrent;
        Int32* buckets = _runBuckets;

        Byte* p = data;
        Byte* end = data + length;
        while (p < end)
        {
            Byte value = *p++;
            Int32 type = value == 0 ? 0 : 1;
            if (!hasCurrent || type != currentType)
            {
                if (hasCurrent)
                {
                    if (currentType == 0)
                    {
                        _zeroRunCount++;
                        _zeroRunSum += currentLength;
                        if (currentLength > _maxZeroRun)
                            _maxZeroRun = currentLength;
                    }
                    else
                    {
                        _nonzeroRunCount++;
                        _nonzeroRunSum += currentLength;
                        if (currentLength > _maxNonzeroRun)
                            _maxNonzeroRun = currentLength;
                    }
                    buckets[GetBucket(currentLength)]++;
                }
                currentType = type;
                currentLength = 0;
                hasCurrent = true;
            }
            currentLength++;
        }

        _currentType = currentType;
        _currentLength = currentLength;
        _hasCurrent = hasCurrent;
    }

    public void Complete()
    {
        Single* output = _share.FeatureTensor + _offset;
        NativeMemory.Clear(output, (UIntPtr)(ResultSize * sizeof(Single)));

        EndRun();

        if (_zeroRunCount > 0)
            output[1] = (Single)_zeroRunSum / _zeroRunCount;
        if (_nonzeroRunCount > 0)
            output[3] = (Single)_nonzeroRunSum / _nonzeroRunCount;

        output[0] = _maxZeroRun;
        output[2] = _maxNonzeroRun;

        Int32 totalRuns = _zeroRunCount + _nonzeroRunCount;
        if (totalRuns > 0)
        {
            Single entropy = 0.0f;
            Single invTotal = 1.0f / totalRuns;
            for (Int32 i = 0; i < BucketCount; i++)
            {
                Int32 c = _runBuckets[i];
                if (c == 0) continue;
                Single p = c * invTotal;
                entropy -= p * Log2Approx.Lookup(p);
            }
            output[4] = entropy;
        }
    }

    public void Clear()
    {
        _currentType = 0;
        _currentLength = 0;
        _maxZeroRun = 0;
        _maxNonzeroRun = 0;
        _zeroRunSum = 0;
        _nonzeroRunSum = 0;
        _zeroRunCount = 0;
        _nonzeroRunCount = 0;
        _wasTail = false;
        _hasCurrent = false;
        NativeMemory.Clear(_runBuckets, (UIntPtr)(BucketCount * sizeof(Int32)));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_runBuckets != null) NativeMemory.AlignedFree(_runBuckets);
            _runBuckets = null;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    private void EndRun()
    {
        if (!_hasCurrent || _currentLength <= 0)
            return;

        if (_currentType == 0)
        {
            _zeroRunCount++;
            _zeroRunSum += _currentLength;
            if (_currentLength > _maxZeroRun)
                _maxZeroRun = _currentLength;
        }
        else
        {
            _nonzeroRunCount++;
            _nonzeroRunSum += _currentLength;
            if (_currentLength > _maxNonzeroRun)
                _maxNonzeroRun = _currentLength;
        }

        _runBuckets[GetBucket(_currentLength)]++;
        _currentLength = 0;
        _hasCurrent = false;
    }

    private static Int32 GetBucket(Int32 length)
    {
        if (length <= 0) return 0;
        if (length >= BucketCount) return BucketCount - 1;
        return length - 1;
    }
}
