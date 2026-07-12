using System.Runtime.InteropServices;
using XaocSirck_Core.Interface.Feature.Zeroflows;
namespace XaocSirck_Core.Feature.Engineering.Zeroflows;
internal sealed unsafe class Entropys : IByteEngineering, IDisposable
{
    private const Int32 BlockSize = 4096;
    private const Int32 BlockCount = 16;
    private const Int32 FreqSize = 256;
    private const Int32 ResultSize = 31;

    private const Single LowEntropyThreshold = 3.0f;
    private const Single HighEntropyThreshold = 7.0f;
    private const Int32 ModeBucketCount = 8;

    private readonly ShareFeatures _share;
    private readonly Int32 _offset;
    private Int32* _headFreq;
    private Int32* _tailFreq;
    private Int32* _blockFreq;
    private Int32 _headFilled;
    private Int32 _tailFilled;
    private Boolean _disposed;

    public Entropys(ShareFeatures share, Int32 offset = FeatureLayout.EntropysOffset)
    {
        ArgumentNullException.ThrowIfNull(share);
        _share = share;
        _offset = offset;
        _headFreq = (Int32*)NativeMemory.AlignedAlloc((UIntPtr)(FreqSize * sizeof(Int32)), 64);
        _tailFreq = (Int32*)NativeMemory.AlignedAlloc((UIntPtr)(FreqSize * sizeof(Int32)), 64);
        _blockFreq = (Int32*)NativeMemory.AlignedAlloc((UIntPtr)(BlockCount * FreqSize * sizeof(Int32)), 64);
    }

    public void ProcessHead(Byte* data, Int32 length)
    {
        _headFilled = length;
        Int32* headFreq = _headFreq;
        Int32* blockFreq = _blockFreq;

        Int32 blockStart = 0;
        for (Int32 block = 0; block < BlockCount && blockStart < length; block++)
        {
            Int32 blockEnd = Math.Min(blockStart + BlockSize, length);
            Int32* blockPtr = blockFreq + block * FreqSize;
            Byte* p = data + blockStart;
            Byte* end = data + blockEnd;
            while (p < end)
            {
                Byte value = *p++;
                headFreq[value]++;
                blockPtr[value]++;
            }
            blockStart = blockEnd;
        }
        Byte* rem = data + blockStart;
        Byte* remEnd = data + length;
        while (rem < remEnd)
            headFreq[*rem++]++;
    }

    public void ProcessTail(Byte* data, Int32 length)
    {
        _tailFilled = length;
        Int32* tailFreq = _tailFreq;
        Byte* p = data;
        Byte* end = data + length;
        while (p < end)
            tailFreq[*p++]++;
    }

    public void Complete()
    {
        Single* output = _share.FeatureTensor + _offset;
        NativeMemory.Clear(output, (UIntPtr)(ResultSize * sizeof(Single)));

        output[0] = CalculateEntropy(_headFreq, _headFilled);
        output[1] = CalculateEntropy(_tailFreq, _tailFilled);

        Int32 actualBlocks = Math.Min(BlockCount, (_headFilled + BlockSize - 1) / BlockSize);
        if (actualBlocks == 0)
            return;

        Single blockMax = Single.MinValue;
        Single blockMin = Single.MaxValue;
        Single blockSum = 0.0f;
        Int32 lowCount = 0;
        Int32 highCount = 0;
        Int32 inversionCount = 0;
        Int32* modeBuckets = stackalloc Int32[ModeBucketCount];

        Single previousEntropy = 0.0f;
        Int32 previousDirection = 0;
        Single firstEntropy = 0.0f;
        Single lastEntropy = 0.0f;

        Int32* blockPtr = _blockFreq;
        for (Int32 b = 0; b < actualBlocks; b++)
        {
            Int32 blockBytes = Math.Min(BlockSize, _headFilled - b * BlockSize);
            Single entropy = CalculateEntropy(blockPtr, blockBytes);
            output[2 + b] = entropy;
            blockMax = Math.Max(blockMax, entropy);
            blockMin = Math.Min(blockMin, entropy);
            blockSum += entropy;

            if (entropy < LowEntropyThreshold) lowCount++;
            if (entropy > HighEntropyThreshold) highCount++;

            Int32 bucket = Math.Min((Int32)(entropy / 1.0f), ModeBucketCount - 1);
            modeBuckets[bucket]++;

            if (b == 0)
                firstEntropy = entropy;
            if (b == actualBlocks - 1)
                lastEntropy = entropy;

            if (b > 0)
            {
                Single delta = entropy - previousEntropy;
                Int32 direction = delta > 1e-6f ? 1 : (delta < -1e-6f ? -1 : 0);
                if (direction != 0 && previousDirection != 0 && direction != previousDirection)
                    inversionCount++;
                if (direction != 0)
                    previousDirection = direction;
            }
            previousEntropy = entropy;
            blockPtr += FreqSize;
        }

        output[18] = blockMax;
        output[19] = blockMin;
        output[20] = blockSum / actualBlocks;

        Single maxDiff = 0.0f;
        Single minDiff = Single.MaxValue;
        Single* blockValues = output + 2;
        Int32 remaining = actualBlocks - 1;
        while (remaining >= 4)
        {
            Single d0 = Math.Abs(blockValues[1] - blockValues[0]);
            Single d1 = Math.Abs(blockValues[2] - blockValues[1]);
            Single d2 = Math.Abs(blockValues[3] - blockValues[2]);
            Single d3 = Math.Abs(blockValues[4] - blockValues[3]);
            maxDiff = Math.Max(maxDiff, Math.Max(Math.Max(d0, d1), Math.Max(d2, d3)));
            minDiff = Math.Min(minDiff, Math.Min(Math.Min(d0, d1), Math.Min(d2, d3)));
            blockValues += 4;
            remaining -= 4;
        }
        while (remaining > 0)
        {
            Single d = Math.Abs(blockValues[1] - blockValues[0]);
            maxDiff = Math.Max(maxDiff, d);
            minDiff = Math.Min(minDiff, d);
            blockValues++;
            remaining--;
        }

        output[21] = actualBlocks > 1 ? maxDiff : 0.0f;
        output[22] = actualBlocks > 1 ? minDiff : 0.0f;

        Int32 modeBucket = 0;
        Int32 modeCount = 0;
        for (Int32 b = 0; b < ModeBucketCount; b++)
        {
            if (modeBuckets[b] > modeCount)
            {
                modeCount = modeBuckets[b];
                modeBucket = b;
            }
        }

        output[24] = (Single)lowCount;
        output[25] = (Single)highCount;
        output[26] = lastEntropy - firstEntropy;
        output[27] = (Single)inversionCount;
        output[28] = firstEntropy;
        output[29] = lastEntropy;
        output[30] = (Single)modeBucket;
    }

    public void Clear()
    {
        _headFilled = 0;
        _tailFilled = 0;
        NativeMemory.Clear(_headFreq, (UIntPtr)(FreqSize * sizeof(Int32)));
        NativeMemory.Clear(_tailFreq, (UIntPtr)(FreqSize * sizeof(Int32)));
        NativeMemory.Clear(_blockFreq, (UIntPtr)(BlockCount * FreqSize * sizeof(Int32)));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_headFreq != null) NativeMemory.AlignedFree(_headFreq);
            if (_tailFreq != null) NativeMemory.AlignedFree(_tailFreq);
            if (_blockFreq != null) NativeMemory.AlignedFree(_blockFreq);
            _headFreq = null;
            _tailFreq = null;
            _blockFreq = null;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    private static Single CalculateEntropy(Int32* freq, Int32 length)
    {
        if (length <= 0)
            return 0.0f;
        Single entropy = 0.0f;
        Single invLength = 1.0f / length;
        Int32 i = 0;
        while (i + 4 <= 256)
        {
            Int32 c0 = freq[i];
            Int32 c1 = freq[i + 1];
            Int32 c2 = freq[i + 2];
            Int32 c3 = freq[i + 3];
            if (c0 != 0) { Single p = c0 * invLength; entropy -= p * Log2Approx.Lookup(p); }
            if (c1 != 0) { Single p = c1 * invLength; entropy -= p * Log2Approx.Lookup(p); }
            if (c2 != 0) { Single p = c2 * invLength; entropy -= p * Log2Approx.Lookup(p); }
            if (c3 != 0) { Single p = c3 * invLength; entropy -= p * Log2Approx.Lookup(p); }
            i += 4;
        }
        while (i < 256)
        {
            Int32 c = freq[i];
            if (c != 0) { Single p = c * invLength; entropy -= p * Log2Approx.Lookup(p); }
            i++;
        }
        return entropy;
    }
}
