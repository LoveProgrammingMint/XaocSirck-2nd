using System.Text;

using PeNet;
using PeNet.Header.Pe;

namespace OverThink_ICeZeRoX.Feature.Sequence;

internal static class CharacterMappingSequence
{
    public const Int32 MaxStrings = 512;
    private const UInt32 IMAGE_SCN_CNT_INITIALIZED_DATA = 0x00000040;
    private const UInt32 IMAGE_SCN_MEM_EXECUTE = 0x20000000;
    private const Int32 MinStringLen = 4;

    public static Byte[] Build(PeFile pe, Byte[] raw)
    {
        ImageSectionHeader[]? sections = pe.ImageSectionHeaders;
        if (sections == null) return Serialize([]);

        List<String> strings = new(MaxStrings);
        foreach (ImageSectionHeader s in sections)
        {
            UInt32 chars = (UInt32)s.Characteristics;
            if ((chars & IMAGE_SCN_MEM_EXECUTE) != 0) continue;
            if ((chars & IMAGE_SCN_CNT_INITIALIZED_DATA) == 0) continue;

            Int32 start = (Int32)s.PointerToRawData;
            Int32 end = Math.Min(start + (Int32)s.SizeOfRawData, raw.Length);
            if (start >= raw.Length || end <= start) continue;

            ExtractStrings(raw, start, end, strings);
            if (strings.Count >= MaxStrings) break;
        }
        if (strings.Count > MaxStrings)
            strings.RemoveRange(MaxStrings, strings.Count - MaxStrings);
        return Serialize(strings);
    }

    private static void ExtractStrings(Byte[] raw, Int32 start, Int32 end, List<String> strings)
    {
        Int32 runStart = -1;
        for (Int32 i = start; i < end && strings.Count < MaxStrings; i++)
        {
            Byte b = raw[i];
            Boolean printable = b >= 0x20 && b < 0x7f;
            if (printable)
            {
                if (runStart < 0) runStart = i;
            }
            else
            {
                if (runStart >= 0 && i - runStart >= MinStringLen)
                    strings.Add(Encoding.ASCII.GetString(raw, runStart, i - runStart));
                runStart = -1;
            }
        }
        if (strings.Count < MaxStrings && runStart >= 0 && end - runStart >= MinStringLen)
            strings.Add(Encoding.ASCII.GetString(raw, runStart, end - runStart));
    }

    private static Byte[] Serialize(List<String> strings)
    {
        Int32 totalLen = sizeof(Int32);
        foreach (String s in strings)
            totalLen += sizeof(Int32) + Encoding.UTF8.GetByteCount(s);

        Byte[] buf = new Byte[totalLen];
        Int32 off = 0;
        WriteInt32(buf, ref off, strings.Count);
        foreach (String s in strings)
        {
            Byte[] utf8 = Encoding.UTF8.GetBytes(s);
            WriteInt32(buf, ref off, utf8.Length);
            Buffer.BlockCopy(utf8, 0, buf, off, utf8.Length);
            off += utf8.Length;
        }
        return buf;
    }

    private static void WriteInt32(Byte[] buf, ref Int32 off, Int32 v)
    {
        buf[off] = (Byte)v;
        buf[off + 1] = (Byte)(v >> 8);
        buf[off + 2] = (Byte)(v >> 16);
        buf[off + 3] = (Byte)(v >> 24);
        off += 4;
    }
}
