using System.Text;

using Gee.External.Capstone;
using Gee.External.Capstone.X86;

using PeNet;
using PeNet.Header.Pe;

namespace OverThink_ICeZeRoX.Feature.Sequence;

internal static class DisassemblySequence
{
    public const Int32 MaxTokens = 1024;
    private const UInt32 IMAGE_SCN_MEM_EXECUTE = 0x20000000;
    private const UInt32 MaxDisassembleBytes = 256 * 1024;

    public static Byte[] Build(PeFile pe, Byte[] raw)
    {
        ImageSectionHeader[]? sections = pe.ImageSectionHeaders;
        if (sections == null || pe.ImageNtHeaders == null) return Serialize([]);

        ImageSectionHeader? exec = null;
        UInt32 maxRaw = 0;
        foreach (ImageSectionHeader s in sections)
        {
            if (((UInt32)s.Characteristics & IMAGE_SCN_MEM_EXECUTE) != 0 && s.SizeOfRawData > maxRaw)
            {
                exec = s;
                maxRaw = s.SizeOfRawData;
            }
        }
        if (exec == null) return Serialize([]);

        UInt32 rawSize = Math.Min(exec.SizeOfRawData, MaxDisassembleBytes);
        Int32 rawOffset = (Int32)exec.PointerToRawData;
        if (rawSize == 0 || rawOffset < 0 || rawOffset + rawSize > raw.Length) return Serialize([]);

        Byte[] data = new Byte[rawSize];
        Array.Copy(raw, rawOffset, data, 0, (Int32)rawSize);

        UInt16 machine = (UInt16)pe.ImageNtHeaders.FileHeader.Machine;
        X86DisassembleMode mode = machine == 0x8664 ? X86DisassembleMode.Bit64 : X86DisassembleMode.Bit32;
        Int64 baseAddr = (Int64)(pe.ImageNtHeaders.OptionalHeader.ImageBase + exec.VirtualAddress);

        X86Instruction[] instructions;
        using (CapstoneX86Disassembler disassembler = CapstoneDisassembler.CreateX86Disassembler(mode))
            instructions = disassembler.Disassemble(data, baseAddr);

        List<String> tokens = new(Math.Min(instructions.Length, MaxTokens));
        Int32 idx = 0;
        while (idx < instructions.Length && tokens.Count < MaxTokens)
        {
            String mn = instructions[idx].Mnemonic;
            if (mn.Equals("int3", StringComparison.OrdinalIgnoreCase))
            {
                Int32 runLen = 1;
                while (idx + runLen < instructions.Length && tokens.Count + runLen <= MaxTokens &&
                       instructions[idx + runLen].Mnemonic.Equals("int3", StringComparison.OrdinalIgnoreCase))
                    runLen++;
                tokens.Add(runLen > 1 ? "int3_list" : "int3");
                idx += runLen;
            }
            else
            {
                tokens.Add(mn);
                idx++;
            }
        }
        return Serialize(tokens);
    }

    private static Byte[] Serialize(List<String> tokens)
    {
        Int32 totalLen = sizeof(Int32);
        foreach (String t in tokens)
            totalLen += sizeof(Int32) + Encoding.UTF8.GetByteCount(t);

        Byte[] buf = new Byte[totalLen];
        Int32 off = 0;
        WriteInt32(buf, ref off, tokens.Count);
        foreach (String t in tokens)
        {
            Byte[] utf8 = Encoding.UTF8.GetBytes(t);
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
