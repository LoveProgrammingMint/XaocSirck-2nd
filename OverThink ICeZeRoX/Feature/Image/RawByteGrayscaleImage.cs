namespace OverThink_ICeZeRoX.Feature.Image;

internal static class RawByteGrayscaleImage
{
    public const Int32 Size = 224;
    public const Int32 PixelCount = Size * Size;

    public static Byte[] Build(Byte[] raw)
    {
        Byte[] buf = new Byte[PixelCount];
        if (raw.Length == 0) return buf;
        if (raw.Length == 1)
        {
            Array.Fill(buf, raw[0]);
            return buf;
        }
        Int32 last = raw.Length - 1;
        Int32 denom = PixelCount - 1;
        for (Int32 i = 0; i < PixelCount; i++)
        {
            Double frac = (Double)i / denom;
            Int32 idx = (Int32)Math.Round(frac * last);
            if (idx < 0) idx = 0;
            else if (idx > last) idx = last;
            buf[i] = raw[idx];
        }
        return buf;
    }
}
