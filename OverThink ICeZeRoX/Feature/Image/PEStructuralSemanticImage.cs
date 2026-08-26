using PeNet;
using PeNet.Header.Pe;

namespace OverThink_ICeZeRoX.Feature.Image;

internal static class PEStructuralSemanticImage
{
    public const Int32 Size = 128;
    public const Int32 PixelCount = Size * Size;

    private const Byte CLASS_HEADER = 0;
    private const Byte CLASS_GAP = 1;
    private const Byte CLASS_ALIGN_PAD = 2;
    private const Byte CLASS_DATA = 3;
    private const Byte CLASS_RESOURCE = 4;
    private const Byte CLASS_RELOC = 5;
    private const Byte CLASS_IMPORT_EXPORT = 6;
    private const Byte CLASS_PACKER = 7;
    private const Byte CLASS_CODE = 8;

    private const UInt32 IMAGE_SCN_CNT_CODE = 0x00000020;
    private const UInt32 IMAGE_SCN_CNT_INITIALIZED_DATA = 0x00000040;
    private const UInt32 IMAGE_SCN_CNT_UNINITIALIZED_DATA = 0x00000080;
    private const UInt32 IMAGE_SCN_MEM_EXECUTE = 0x20000000;

    private const Int32 EntropyWindow = 256;
    private const Single BrightnessMin = 0.55f;
    private const Single BrightnessMax = 1.15f;
    private const Single HighEntropyThreshold = 7.5f;
    private const Int32 StringMinLen = 5;
    private const Single StringTextureDarken = 0.82f;

    private static readonly Single[][] BaseColors =
    {
        new Single[] { 128, 128, 128 },
        new Single[] { 255, 255, 255 },
        new Single[] {  64,  64,  64 },
        new Single[] {   0, 255,   0 },
        new Single[] {   0,   0, 255 },
        new Single[] {   0, 255, 255 },
        new Single[] { 255, 255,   0 },
        new Single[] { 255,   0, 255 },
        new Single[] { 255,   0,   0 },
    };

    private static readonly HashSet<String> StandardSections = new(StringComparer.OrdinalIgnoreCase)
    {
        ".text",".data",".rdata",".bss",".rsrc",".reloc",".idata",".edata",".tls",
        ".pdata",".xdata",".gfids",".didat",".debug",".CRT",".sdata",".srdata",
        ".sxdata",".gep",".gehcont",".00cfg",".sgi",".vmp","CODE","DATA",".textbss",
        ".itext",".mdata",".fptmp",".shared"
    };

    private static readonly String[] PackerHints =
    {
        "upx","vmp","aspack","nsp","themida","petite","mpress","pack","enigma","yoda","nsp1","svkp"
    };

    public static Byte[] Build(Byte[] raw, PeFile pe)
    {
        Int32 n = raw.Length;
        Byte[]? classMap = null;
        Single[]? entropyMap = null;
        Boolean[]? stringMask = null;

        if (pe.ImageSectionHeaders != null && pe.ImageNtHeaders != null)
        {
            (classMap, entropyMap, stringMask) = BuildSemanticMap(raw, pe);
        }

        Byte[] outBuf = new Byte[PixelCount * 3];

        if (classMap == null || entropyMap == null || stringMask == null)
        {
            for (Int32 i = 0; i < PixelCount; i++)
            {
                Int32 idx = SampleIndex(n, i);
                Byte g = n > 0 ? raw[idx] : (Byte)0;
                outBuf[i * 3] = g;
                outBuf[i * 3 + 1] = g;
                outBuf[i * 3 + 2] = g;
            }
            return outBuf;
        }

        for (Int32 i = 0; i < PixelCount; i++)
        {
            Int32 idx = n == PixelCount ? i : SampleIndex(n, i);
            Byte cls = classMap[idx];
            Single[] baseColor = BaseColors[cls];
            Single entropy = entropyMap[idx];
            Single entNorm = Math.Clamp(entropy / 8f, 0f, 1f);
            Single brightness = BrightnessMin + entNorm * (BrightnessMax - BrightnessMin);
            if (cls == CLASS_GAP || cls == CLASS_ALIGN_PAD)
                brightness = 1f;

            Single r = baseColor[0] * brightness;
            Single g = baseColor[1] * brightness;
            Single b = baseColor[2] * brightness;

            if (stringMask[idx] && cls == CLASS_CODE)
            {
                r *= StringTextureDarken;
                g *= StringTextureDarken;
                b *= StringTextureDarken;
            }

            outBuf[i * 3] = ClampByte(r);
            outBuf[i * 3 + 1] = ClampByte(g);
            outBuf[i * 3 + 2] = ClampByte(b);
        }
        return outBuf;
    }

    private static (Byte[] ClassMap, Single[] EntropyMap, Boolean[] StringMask) BuildSemanticMap(Byte[] raw, PeFile pe)
    {
        Int32 n = raw.Length;
        Byte[] classMap = new Byte[n];
        Array.Fill(classMap, CLASS_GAP);
        Single[] entropyMap = new Single[n];
        Boolean[] stringMask = new Boolean[n];

        UInt32 e_lfanew = pe.ImageDosHeader?.E_lfanew ?? 0;
        UInt32 sizeOfOptHeader = pe.ImageNtHeaders!.FileHeader.SizeOfOptionalHeader;
        UInt16 numSections = pe.ImageNtHeaders.FileHeader.NumberOfSections;
        Int32 headerSize = (Int32)Math.Max(
            (UInt64)e_lfanew + 4 + 20 + sizeOfOptHeader + 40UL * numSections,
            0x200UL);
        Int32 hdrEnd = Math.Min(headerSize, n);
        for (Int32 i = 0; i < hdrEnd; i++)
            classMap[i] = CLASS_HEADER;

        List<SectionEntry> secs = new();
        foreach (ImageSectionHeader s in pe.ImageSectionHeaders!)
        {
            String name = (s.Name ?? String.Empty);
            UInt32 rawPtr = s.PointerToRawData;
            UInt32 rawSize = s.SizeOfRawData;
            UInt32 virtSize = s.VirtualSize;
            UInt32 chars = (UInt32)s.Characteristics;
            Int32 start = (Int32)rawPtr;
            Int32 end = Math.Min(start + (Int32)rawSize, n);
            if (start >= n || end <= start)
            {
                secs.Add(new SectionEntry(name, rawPtr, rawSize, virtSize, chars, 0f, CLASS_DATA));
                continue;
            }
            Single ent = ShannonEntropy(raw, start, end);
            Byte cls = ClassifySection(name, chars, ent);
            secs.Add(new SectionEntry(name, rawPtr, rawSize, virtSize, chars, ent, cls));
        }
        secs.Sort((a, b) => a.RawPtr.CompareTo(b.RawPtr));

        foreach (SectionEntry sec in secs)
        {
            Int32 start = (Int32)sec.RawPtr;
            Int32 rsize = (Int32)sec.RawSize;
            Int32 vsize = (Int32)(sec.VirtualSize == 0 ? sec.RawSize : sec.VirtualSize);
            Int32 end = Math.Min(start + rsize, n);
            if (start >= n || end <= start) continue;

            for (Int32 i = start; i < end; i++)
                classMap[i] = sec.Class;

            Int32 pad = Math.Max(rsize - vsize, 0);
            if (pad > 0)
            {
                Int32 padStart = Math.Max(end - pad, start);
                for (Int32 i = padStart; i < end; i++)
                    classMap[i] = CLASS_ALIGN_PAD;
            }

            Int32 segLen = end - start;
            Int32 numWindows = (segLen + EntropyWindow - 1) / EntropyWindow;
            for (Int32 w = 0; w < numWindows; w++)
            {
                Int32 ws = start + w * EntropyWindow;
                Int32 we = Math.Min(ws + EntropyWindow, end);
                Single e = ShannonEntropy(raw, ws, we);
                for (Int32 i = ws; i < we; i++)
                    entropyMap[i] = e;
            }

            DetectStringRegions(raw, start, end, stringMask);
        }

        return (classMap, entropyMap, stringMask);
    }

    private static void DetectStringRegions(Byte[] raw, Int32 start, Int32 end, Boolean[] stringMask)
    {
        Int32 runStart = -1;
        for (Int32 i = start; i < end; i++)
        {
            Byte b = raw[i];
            Boolean printable = (b >= 0x20 && b < 0x7f) || b == 0x09 || b == 0x0A || b == 0x0D;
            if (printable)
            {
                if (runStart < 0) runStart = i;
            }
            else
            {
                if (runStart >= 0 && i - runStart >= StringMinLen)
                {
                    for (Int32 j = runStart; j < i; j++)
                        stringMask[j] = true;
                }
                runStart = -1;
            }
        }
        if (runStart >= 0 && end - runStart >= StringMinLen)
        {
            for (Int32 j = runStart; j < end; j++)
                stringMask[j] = true;
        }
    }

    private static Byte ClassifySection(String name, UInt32 chars, Single entropy)
    {
        String nm = name.TrimEnd('\0').ToLowerInvariant();
        if (nm.Contains(".rsrc")) return CLASS_RESOURCE;
        if (nm.Contains(".reloc")) return CLASS_RELOC;
        if (nm.Contains(".idata") || nm.Contains(".edata")) return CLASS_IMPORT_EXPORT;
        if (IsPackerSection(name, entropy)) return CLASS_PACKER;
        if ((chars & IMAGE_SCN_CNT_CODE) != 0 || (chars & IMAGE_SCN_MEM_EXECUTE) != 0) return CLASS_CODE;
        if ((chars & IMAGE_SCN_CNT_INITIALIZED_DATA) != 0 || (chars & IMAGE_SCN_CNT_UNINITIALIZED_DATA) != 0)
            return CLASS_DATA;
        return CLASS_DATA;
    }

    private static Boolean IsPackerSection(String name, Single entropy)
    {
        String nm = name.TrimEnd('\0').ToLowerInvariant();
        if (StandardSections.Contains(nm)) return false;
        foreach (String hint in PackerHints)
        {
            if (nm.Contains(hint)) return true;
        }
        return entropy >= HighEntropyThreshold;
    }

    private static Single ShannonEntropy(Byte[] raw, Int32 start, Int32 end)
    {
        Int32 len = end - start;
        if (len <= 0) return 0f;
        Span<Int32> freq = stackalloc Int32[256];
        freq.Clear();
        for (Int32 i = start; i < end; i++)
            freq[raw[i]]++;
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

    private static Int32 SampleIndex(Int32 n, Int32 i)
    {
        if (n <= 1) return 0;
        Int32 last = n - 1;
        Int32 denom = PixelCount - 1;
        Double frac = (Double)i / denom;
        Int32 idx = (Int32)Math.Round(frac * last);
        return idx < 0 ? 0 : (idx > last ? last : idx);
    }

    private static Byte ClampByte(Single v)
    {
        Int32 iv = (Int32)(v + 0.5f);
        return (Byte)Math.Clamp(iv, 0, 255);
    }

    private readonly record struct SectionEntry(String Name, UInt32 RawPtr, UInt32 RawSize, UInt32 VirtualSize, UInt32 Chars, Single Entropy, Byte Class);
}
