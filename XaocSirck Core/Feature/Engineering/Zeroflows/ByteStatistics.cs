using System.Runtime.InteropServices;
using XaocSirck_Core.Interface.Feature.Zeroflows;
namespace XaocSirck_Core.Feature.Engineering.Zeroflows;
internal sealed unsafe class ByteStatistics : IByteEngineering, IDisposable
{
    private const Int32 ResultSize = 24;
    private const Int32 BitPlanes = 8;

    private static ReadOnlySpan<Byte> ByteClass =>
    [
        0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x02, 0x02, 0x04, 0x04, 0x02, 0x04, 0x04,
        0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04,
        0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01,
        0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01,
        0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01,
        0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01,
        0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01,
        0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x04,
        0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08,
        0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08,
        0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08,
        0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08,
        0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08,
        0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08,
        0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08,
        0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08
    ];

    private readonly ShareFeatures _share;
    private readonly Int32 _offset;
    private Int32* _freq;
    private Int32* _freq1;
    private Int32* _freq2;
    private Int32* _freq3;
    private Int64 _localitySum;
    private Int32 _count;
    private Int32 _printableCount;
    private Int32 _asciiTextCount;
    private Int32 _controlCount;
    private Int32 _highByteCount;
    private Byte _previous;
    private Boolean _hasPrevious;
    private Boolean _disposed;

    public ByteStatistics(ShareFeatures share, Int32 offset = FeatureLayout.ByteStatisticsOffset)
    {
        ArgumentNullException.ThrowIfNull(share);
        _share = share;
        _offset = offset;
        _freq = (Int32*)NativeMemory.AlignedAlloc((UIntPtr)(256 * sizeof(Int32)), 64);
        _freq1 = (Int32*)NativeMemory.AlignedAlloc((UIntPtr)(256 * sizeof(Int32)), 64);
        _freq2 = (Int32*)NativeMemory.AlignedAlloc((UIntPtr)(256 * sizeof(Int32)), 64);
        _freq3 = (Int32*)NativeMemory.AlignedAlloc((UIntPtr)(256 * sizeof(Int32)), 64);
    }

    public void ProcessHead(Byte* data, Int32 length)
    {
        ProcessSpan(data, length);
    }

    public void ProcessTail(Byte* data, Int32 length)
    {
        ProcessSpan(data, length);
    }

    private void ProcessSpan(Byte* data, Int32 length)
    {
        if (length == 0)
            return;

        _count += length;

        Int32* f0 = _freq;
        Int32* f1 = _freq1;
        Int32* f2 = _freq2;
        Int32* f3 = _freq3;

        Byte prev = _previous;
        Int64 locality = _localitySum;
        Int32 printable = _printableCount;
        Int32 asciiText = _asciiTextCount;
        Int32 control = _controlCount;
        Int32 highByte = _highByteCount;
        Boolean hasPrevious = _hasPrevious;

        Byte* p = data;
        Byte* end = data + length;
        Byte* blockEnd = data + (length & ~3);

        while (p < blockEnd)
        {
            Byte v0 = p[0];
            Byte v1 = p[1];
            Byte v2 = p[2];
            Byte v3 = p[3];

            f0[v0]++;
            f1[v1]++;
            f2[v2]++;
            f3[v3]++;

            Byte flags0 = ByteClass[v0];
            Byte flags1 = ByteClass[v1];
            Byte flags2 = ByteClass[v2];
            Byte flags3 = ByteClass[v3];
            printable += (flags0 & 1) + (flags1 & 1) + (flags2 & 1) + (flags3 & 1);
            asciiText += ((flags0 | (flags0 >> 1)) & 1) + ((flags1 | (flags1 >> 1)) & 1) + ((flags2 | (flags2 >> 1)) & 1) + ((flags3 | (flags3 >> 1)) & 1);
            control += (flags0 >> 2) & 1;
            control += (flags1 >> 2) & 1;
            control += (flags2 >> 2) & 1;
            control += (flags3 >> 2) & 1;
            highByte += (flags0 >> 3) & 1;
            highByte += (flags1 >> 3) & 1;
            highByte += (flags2 >> 3) & 1;
            highByte += (flags3 >> 3) & 1;

            if (hasPrevious)
            {
                Int32 diff = v0 - prev;
                locality += (diff + (diff >> 31)) ^ (diff >> 31);
            }
            else
            {
                hasPrevious = true;
            }

            Int32 diff1 = v1 - v0;
            locality += (diff1 + (diff1 >> 31)) ^ (diff1 >> 31);
            Int32 diff2 = v2 - v1;
            locality += (diff2 + (diff2 >> 31)) ^ (diff2 >> 31);
            Int32 diff3 = v3 - v2;
            locality += (diff3 + (diff3 >> 31)) ^ (diff3 >> 31);

            prev = v3;
            p += 4;
        }

        while (p < end)
        {
            Byte value = *p++;
            f0[value]++;

            Byte flags = ByteClass[value];
            printable += flags & 1;
            asciiText += (flags | (flags >> 1)) & 1;
            control += (flags >> 2) & 1;
            highByte += (flags >> 3) & 1;

            if (hasPrevious)
            {
                Int32 diff = value - prev;
                locality += (diff + (diff >> 31)) ^ (diff >> 31);
            }
            else
            {
                hasPrevious = true;
            }

            prev = value;
        }

        _previous = prev;
        _hasPrevious = hasPrevious;
        _localitySum = locality;
        _printableCount = printable;
        _asciiTextCount = asciiText;
        _controlCount = control;
        _highByteCount = highByte;
    }

    public void Complete()
    {
        Single* output = _share.FeatureTensor + _offset;
        NativeMemory.Clear(output, (UIntPtr)(ResultSize * sizeof(Single)));
        if (_count == 0)
            return;

        Int32* f0 = _freq;
        Int32* f1 = _freq1;
        Int32* f2 = _freq2;
        Int32* f3 = _freq3;
        Int32 i = 0;
        while (i + 4 <= 256)
        {
            f0[i] += f1[i] + f2[i] + f3[i];
            f0[i + 1] += f1[i + 1] + f2[i + 1] + f3[i + 1];
            f0[i + 2] += f1[i + 2] + f2[i + 2] + f3[i + 2];
            f0[i + 3] += f1[i + 3] + f2[i + 3] + f3[i + 3];
            i += 4;
        }
        while (i < 256)
        {
            f0[i] += f1[i] + f2[i] + f3[i];
            i++;
        }

        Single invCount = 1.0f / _count;
        Single entropy = 0.0f;
        Int32 unique = 0;
        Int32 top1 = 0, top2 = 0, top3 = 0, top4 = 0;
        Single mean = 0.0f;
        Single chiSquare = 0.0f;
        Single expected = _count / 256.0f;

        Double sum = 0.0;
        Double sum2 = 0.0;
        Double sum3 = 0.0;
        Double sum4 = 0.0;

        i = 0;
        while (i + 4 <= 256)
        {
            Int32 c0 = _freq[i];
            Int32 c1 = _freq[i + 1];
            Int32 c2 = _freq[i + 2];
            Int32 c3 = _freq[i + 3];

            if (c0 != 0) { unique++; Single p = c0 * invCount; entropy -= p * Log2Approx.Lookup(p); UpdateTop(ref top1, ref top2, ref top3, ref top4, c0); }
            if (c1 != 0) { unique++; Single p = c1 * invCount; entropy -= p * Log2Approx.Lookup(p); UpdateTop(ref top1, ref top2, ref top3, ref top4, c1); }
            if (c2 != 0) { unique++; Single p = c2 * invCount; entropy -= p * Log2Approx.Lookup(p); UpdateTop(ref top1, ref top2, ref top3, ref top4, c2); }
            if (c3 != 0) { unique++; Single p = c3 * invCount; entropy -= p * Log2Approx.Lookup(p); UpdateTop(ref top1, ref top2, ref top3, ref top4, c3); }

            Double v0 = i;
            Double v1 = i + 1;
            Double v2 = i + 2;
            Double v3 = i + 3;
            Double s01 = c0 * v0 + c1 * v1;
            Double s23 = c2 * v2 + c3 * v3;
            sum += s01 + s23;
            mean += (Single)(s01 + s23);

            Double v0_2 = v0 * v0;
            Double v1_2 = v1 * v1;
            Double v2_2 = v2 * v2;
            Double v3_2 = v3 * v3;
            sum2 += c0 * v0_2 + c1 * v1_2 + c2 * v2_2 + c3 * v3_2;
            sum3 += c0 * v0_2 * v0 + c1 * v1_2 * v1 + c2 * v2_2 * v2 + c3 * v3_2 * v3;
            sum4 += c0 * v0_2 * v0_2 + c1 * v1_2 * v1_2 + c2 * v2_2 * v2_2 + c3 * v3_2 * v3_2;

            Single d0 = c0 - expected;
            Single d1 = c1 - expected;
            Single d2 = c2 - expected;
            Single d3 = c3 - expected;
            chiSquare += d0 * d0 + d1 * d1 + d2 * d2 + d3 * d3;

            i += 4;
        }
        while (i < 256)
        {
            Int32 c = _freq[i];
            if (c != 0)
            {
                unique++;
                Single p = c * invCount;
                entropy -= p * Log2Approx.Lookup(p);
                UpdateTop(ref top1, ref top2, ref top3, ref top4, c);
            }
            Double vd = i;
            Double vd2 = vd * vd;
            Double cv = c * vd;
            sum += cv;
            mean += (Single)cv;
            sum2 += c * vd2;
            sum3 += c * vd2 * vd;
            sum4 += c * vd2 * vd2;
            Single d = c - expected;
            chiSquare += d * d;
            i++;
        }

        mean *= invCount;
        chiSquare /= expected;

        Double meanD = sum / (Double)_count;
        Double varianceD = sum2 / _count - meanD * meanD;
        Single variance = (Single)varianceD;
        Single std = MathF.Sqrt(variance);
        Single skewness = 0.0f;
        Single kurtosis = 0.0f;
        if (std > 1e-6f)
        {
            Double m3 = sum3 / _count - 3.0 * meanD * varianceD - meanD * meanD * meanD;
            Double m4 = sum4 / _count - 4.0 * meanD * (sum3 / _count) + 6.0 * meanD * meanD * (sum2 / _count) - 3.0 * meanD * meanD * meanD * meanD;
            Double std3 = std * std * std;
            Double std4 = std3 * std;
            skewness = (Single)(m3 / std3);
            kurtosis = (Single)(m4 / std4 - 3.0);
        }

        Int32 bitSums0 = 0, bitSums1 = 0, bitSums2 = 0, bitSums3 = 0;
        Int32 bitSums4 = 0, bitSums5 = 0, bitSums6 = 0, bitSums7 = 0;
        Int32 j = 0;
        while (j < 256)
        {
            Int32 c = _freq[j];
            if (c != 0)
            {
                Int32 mask = j;
                bitSums0 += (mask & 0x01) * c;
                bitSums1 += (mask & 0x02) * c;
                bitSums2 += (mask & 0x04) * c;
                bitSums3 += (mask & 0x08) * c;
                bitSums4 += (mask & 0x10) * c;
                bitSums5 += (mask & 0x20) * c;
                bitSums6 += (mask & 0x40) * c;
                bitSums7 += (mask & 0x80) * c;
            }
            j++;
        }

        Single bitEntropy = 0.0f;
        AccumulateBitEntropy(ref bitEntropy, bitSums0, invCount);
        AccumulateBitEntropy(ref bitEntropy, bitSums1, invCount);
        AccumulateBitEntropy(ref bitEntropy, bitSums2, invCount);
        AccumulateBitEntropy(ref bitEntropy, bitSums3, invCount);
        AccumulateBitEntropy(ref bitEntropy, bitSums4, invCount);
        AccumulateBitEntropy(ref bitEntropy, bitSums5, invCount);
        AccumulateBitEntropy(ref bitEntropy, bitSums6, invCount);
        AccumulateBitEntropy(ref bitEntropy, bitSums7, invCount);
        bitEntropy *= 0.125f;

        output[0] = entropy;
        output[1] = (top1 + top2 + top3 + top4) * invCount;
        output[2] = unique / 256.0f;
        output[3] = _freq[0] * invCount;
        output[4] = _freq[255] * invCount;
        output[5] = _printableCount * invCount;
        output[6] = _highByteCount * invCount;
        output[7] = _asciiTextCount * invCount;
        output[8] = _controlCount * invCount;
        output[9] = mean / 255.0f;
        output[10] = variance / (255.0f * 255.0f);
        output[11] = skewness;
        output[12] = kurtosis;
        output[13] = chiSquare / _count;
        output[14] = entropy / 8.0f;
        output[15] = bitEntropy;
        output[16] = _count > 1 ? _localitySum / (Single)(_count - 1) / 255.0f : 0.0f;
    }

    public void Clear()
    {
        _count = 0;
        _localitySum = 0;
        _printableCount = 0;
        _asciiTextCount = 0;
        _controlCount = 0;
        _highByteCount = 0;
        _previous = 0;
        _hasPrevious = false;
        NativeMemory.Clear(_freq, (UIntPtr)(256 * sizeof(Int32)));
        NativeMemory.Clear(_freq1, (UIntPtr)(256 * sizeof(Int32)));
        NativeMemory.Clear(_freq2, (UIntPtr)(256 * sizeof(Int32)));
        NativeMemory.Clear(_freq3, (UIntPtr)(256 * sizeof(Int32)));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_freq != null) NativeMemory.AlignedFree(_freq);
            if (_freq1 != null) NativeMemory.AlignedFree(_freq1);
            if (_freq2 != null) NativeMemory.AlignedFree(_freq2);
            if (_freq3 != null) NativeMemory.AlignedFree(_freq3);
            _freq = null;
            _freq1 = null;
            _freq2 = null;
            _freq3 = null;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    private static void AccumulateBitEntropy(ref Single bitEntropy, Int32 ones, Single invCount)
    {
        Single p = ones * invCount;
        if (p > 0.0f && p < 1.0f)
            bitEntropy += -(p * Log2Approx.Lookup(p) + (1.0f - p) * Log2Approx.Lookup(1.0f - p));
    }

    private static void UpdateTop(ref Int32 top1, ref Int32 top2, ref Int32 top3, ref Int32 top4, Int32 value)
    {
        if (value > top1)
        {
            top4 = top3;
            top3 = top2;
            top2 = top1;
            top1 = value;
        }
        else if (value > top2)
        {
            top4 = top3;
            top3 = top2;
            top2 = value;
        }
        else if (value > top3)
        {
            top4 = top3;
            top3 = value;
        }
        else if (value > top4)
        {
            top4 = value;
        }
    }
}
