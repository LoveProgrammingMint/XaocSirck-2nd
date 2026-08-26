namespace OverThink_ICeZeRoX.Feature.Image;

internal static class EntropyVisualizationImage
{
    public const Int32 Size = 96;
    public const Int32 PixelCount = Size * Size;
    private const Int32 Window = 256;

    public static Byte[] Build(Byte[] raw)
    {
        Byte[] buf = new Byte[PixelCount];
        if (raw.Length == 0) return buf;

        Int32 numWindows = (raw.Length + Window - 1) / Window;
        Single[] ent = new Single[numWindows];
        Int32[] freq = new Int32[256];
        Int32 winStart = 0;
        for (Int32 w = 0; w < numWindows; w++)
        {
            Array.Clear(freq, 0, 256);
            Int32 winEnd = Math.Min(winStart + Window, raw.Length);
            Int32 len = winEnd - winStart;
            for (Int32 i = winStart; i < winEnd; i++)
                freq[raw[i]]++;
            ent[w] = ShannonEntropy(freq, len);
            winStart = winEnd;
        }

        if (numWindows == 1)
        {
            Byte b = EntropyToByte(ent[0]);
            Array.Fill(buf, b);
            return buf;
        }

        Int32 last = numWindows - 1;
        Int32 denom = PixelCount - 1;
        for (Int32 i = 0; i < PixelCount; i++)
        {
            Double frac = (Double)i / denom;
            Int32 idx = (Int32)Math.Round(frac * last);
            if (idx < 0) idx = 0;
            else if (idx > last) idx = last;
            buf[i] = EntropyToByte(ent[idx]);
        }
        return buf;
    }

    private static Single ShannonEntropy(Int32[] freq, Int32 len)
    {
        if (len <= 0) return 0f;
        Single entropy = 0f;
        Single inv = 1f / len;
        for (Int32 i = 0; i < 256; i++)
        {
            Int32 c = freq[i];
            if (c == 0) continue;
            Single p = c * inv;
            entropy -= p * MathF.Log2(p);
        }
        return entropy;
    }

    private static Byte EntropyToByte(Single entropy)
    {
        if (entropy <= 0f) return 0;
        if (entropy >= 8f) return 255;
        Int32 v = (Int32)(entropy / 8f * 255f + 0.5f);
        return (Byte)Math.Min(255, Math.Max(0, v));
    }
}
