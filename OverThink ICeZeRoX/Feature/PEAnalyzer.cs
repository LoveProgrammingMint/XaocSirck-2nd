using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

using Gee.External.Capstone;
using Gee.External.Capstone.X86;

using PeNet;
using PeNet.Header.Pe;

namespace OverThink_ICeZeRoX.Feature;

internal sealed class PEAnalyzer : IDisposable
{
    public const Int32 NODE_TYPE_PE = 0;
    public const Int32 NODE_TYPE_DLL = 1;
    public const Int32 NODE_TYPE_FUNC = 2;
    public const Int32 NODE_TYPE_EVENT = 3;

    public const Int32 EDGE_TYPE_INTRABLOCK = 0;
    public const Int32 EDGE_TYPE_INTERBLOCK = 1;
    public const Int32 EDGE_TYPE_CALL = 2;
    public const Int32 EDGE_TYPE_CALL_INDIRECT = 3;
    public const Int32 EDGE_TYPE_CONTAINS = 4;

    public const Int32 CALL_DIRECT_WINAPI = 0;
    public const Int32 CALL_INDIRECT_WINAPI = 1;
    public const Int32 CALL_INTERNAL = 2;
    public const Int32 CALL_UNKNOWN = 3;

    public const Int32 FEATURE_DIM = 8;

    private static readonly Byte[][] ProloguesX86 =
    {
        new Byte[] { 0x55, 0x8B, 0xEC },
        new Byte[] { 0x55, 0x89, 0xE5 },
    };

    private static readonly Byte[][] ProloguesX64 =
    {
        new Byte[] { 0x48, 0x89, 0x5C, 0x24 },
        new Byte[] { 0x48, 0x89, 0x6C, 0x24 },
        new Byte[] { 0x48, 0x89, 0x74, 0x24 },
        new Byte[] { 0x40, 0x53 },
        new Byte[] { 0x40, 0x55 },
        new Byte[] { 0x40, 0x56 },
        new Byte[] { 0x40, 0x57 },
        new Byte[] { 0x48, 0x83, 0xEC },
        new Byte[] { 0x48, 0x81, 0xEC },
    };

    private static readonly HashSet<String> JccMnemonics = new(StringComparer.OrdinalIgnoreCase)
    {
        "je","jne","jz","jnz","jg","jge","jl","jle","ja","jae","jb","jbe",
        "jo","jno","js","jns","jp","jnp","jc","jnc","jcxz","jecxz","jrcxz"
    };
    private static readonly HashSet<String> JmpMnemonics = new(StringComparer.OrdinalIgnoreCase) { "jmp" };
    private static readonly HashSet<String> RetMnemonics = new(StringComparer.OrdinalIgnoreCase) { "ret","retn","retf" };
    private static readonly HashSet<String> CallMnemonics = new(StringComparer.OrdinalIgnoreCase) { "call" };
    private static readonly HashSet<String> TerminatorMnemonics = new(StringComparer.OrdinalIgnoreCase) { "hlt","int3","ud2" };

    public readonly PeFile Pe;
    public readonly Byte[] Raw;
    public readonly Boolean Is64Bit;
    public readonly UInt64 ImageBase;
    public readonly List<SectionInfo> Sections = new();
    public readonly Dictionary<UInt64, (String Dll, String Fname)> Iat = new();
    public readonly Dictionary<UInt64, String> Functions = new();

    private readonly CapstoneX86Disassembler _disassembler;
    private readonly Dictionary<UInt64, X86Instruction[]> _insnCache = new();
    private UInt64[] _fnSorted = Array.Empty<UInt64>();
    private Boolean _disposed;

    public PEAnalyzer(PeFile pe, Byte[] raw)
    {
        Pe = pe;
        Raw = raw;
        UInt16 machine = (UInt16)(pe.ImageNtHeaders?.FileHeader.Machine ?? 0);
        Is64Bit = machine == 0x8664;
        ImageBase = pe.ImageNtHeaders?.OptionalHeader.ImageBase ?? 0;
        X86DisassembleMode mode = Is64Bit ? X86DisassembleMode.Bit64 : X86DisassembleMode.Bit32;
        _disassembler = CapstoneDisassembler.CreateX86Disassembler(mode);
        _disassembler.EnableInstructionDetails = true;
        InitSections();
        ParseImports();
        ParseExports();
    }

    private void InitSections()
    {
        if (Pe.ImageSectionHeaders == null) return;
        foreach (ImageSectionHeader s in Pe.ImageSectionHeaders)
        {
            UInt64 start = ImageBase + s.VirtualAddress;
            UInt64 end = start + Math.Max(s.VirtualSize, s.SizeOfRawData);
            Sections.Add(new SectionInfo(start, end, (UInt32)s.Characteristics, s.PointerToRawData, s.SizeOfRawData, s.VirtualAddress, s.VirtualSize, s.Name ?? String.Empty));
        }
    }

    private void ParseImports()
    {
        if (Pe.ImportedFunctions == null) return;
        UInt32 iatDirRva = Pe.ImageNtHeaders?.OptionalHeader.DataDirectory[12].VirtualAddress ?? 0;
        foreach (ImportFunction imp in Pe.ImportedFunctions)
        {
            String dll = (imp.DLL ?? String.Empty).ToLowerInvariant();
            String fname = imp.Name ?? $"ord_{imp.Hint}";
            UInt64 va = ImageBase + iatDirRva + imp.IATOffset;
            Iat[va] = (dll, fname);
        }
    }

    private void ParseExports()
    {
        if (Pe.ExportedFunctions == null) return;
        foreach (ExportFunction exp in Pe.ExportedFunctions)
        {
            UInt64 va = ImageBase + exp.Address;
            String name = exp.Name ?? $"ord_{exp.Ordinal}";
            Functions[va] = name;
        }
    }

    public Boolean VaInExecutableSection(UInt64 va)
    {
        foreach (SectionInfo s in Sections)
        {
            if (s.Start <= va && va < s.End && (s.Chars & 0x20000000) != 0)
                return true;
        }
        return false;
    }

    public Byte[]? ReadBytesAtVA(UInt64 va, Int32 size)
    {
        UInt64 rva = va - ImageBase;
        foreach (SectionInfo s in Sections)
        {
            UInt32 sRva = s.VirtualAddress;
            UInt32 sEnd = sRva + s.RawSize;
            if (rva >= sRva && rva < sEnd)
            {
                Int32 offset = (Int32)(s.RawPtr + (rva - sRva));
                Int32 available = (Int32)(s.RawPtr + s.RawSize) - offset;
                Int32 count = Math.Min(size, available);
                if (count <= 0 || offset + count > Raw.Length) return null;
                Byte[] buf = new Byte[count];
                Array.Copy(Raw, offset, buf, 0, count);
                return buf;
            }
        }
        return null;
    }

    public UInt64 RvaToVa(UInt32 rva) => ImageBase + rva;

    private UInt64? NextFunctionVa(UInt64 va)
    {
        if (_fnSorted.Length == 0) return null;
        Int32 idx = Array.BinarySearch(_fnSorted, va);
        if (idx < 0) idx = ~idx; else idx++;
        return idx < _fnSorted.Length ? _fnSorted[idx] : null;
    }

    public static UInt64 NameHash(String name)
    {
        if (String.IsNullOrEmpty(name)) return 0;
        Byte[] h = MD5.HashData(Encoding.UTF8.GetBytes(name));
        return ((UInt64)h[0] << 24) | ((UInt64)h[1] << 16) | ((UInt64)h[2] << 8) | h[3];
    }

    public static (UInt16 Lo, UInt16 Hi) Split64(UInt64 v)
    {
        v &= 0xFFFFFFFFFFFFFFFFUL;
        return ((UInt16)(v & 0xFFFF), (UInt16)((v >> 16) & 0xFFFF));
    }

    public static X86Operand[] GetOperands(X86Instruction ins)
    {
        return ins.HasDetails ? ins.Details.Operands : Array.Empty<X86Operand>();
    }

    public UInt64? DirectTarget(X86Instruction ins)
    {
        X86Operand[] ops = GetOperands(ins);
        if (ops.Length == 0) return null;
        return ops[0].Type == X86OperandType.Immediate ? (UInt64)ops[0].Immediate : null;
    }

    public void DiscoverFunctions()
    {
        Dictionary<UInt64, String> discovered = new(Functions);
        UInt32 epRva = Pe.ImageNtHeaders?.OptionalHeader.AddressOfEntryPoint ?? 0;
        if (epRva != 0)
        {
            UInt64 epVa = RvaToVa(epRva);
            if (VaInExecutableSection(epVa) && !discovered.ContainsKey(epVa))
                discovered[epVa] = "entry_point";
        }

        Byte[][] prologues = Is64Bit ? ProloguesX64 : ProloguesX86;
        foreach (SectionInfo s in Sections)
        {
            if ((s.Chars & 0x20000000) == 0) continue;
            Int32 sectionSize = (Int32)Math.Min(Math.Min((UInt64)(s.End - s.Start), (UInt64)Math.Max(s.RawSize, 0)), 0x1000000UL);
            Byte[]? data = ReadBytesAtVA(s.Start, sectionSize);
            if (data == null) continue;
            foreach (Byte[] pat in prologues)
            {
                Int32 idx = 0;
                while ((idx = IndexOf(data, pat, idx)) >= 0)
                {
                    UInt64 va = s.Start + (UInt64)idx;
                    if (!discovered.ContainsKey(va))
                        discovered[va] = $"sub_{va:X8}";
                    idx++;
                }
            }
        }

        Functions.Clear();
        foreach (KeyValuePair<UInt64, String> kv in discovered)
            Functions[kv.Key] = kv.Value;
        _fnSorted = Functions.Keys.OrderBy(x => x).ToArray();

        HashSet<UInt64> newTargets = new();
        foreach (UInt64 fnVa in Functions.Keys.ToArray())
        {
            foreach (X86Instruction ins in DisasmFunctionCached(fnVa, 0x4000))
            {
                String mn = ins.Mnemonic;
                if (CallMnemonics.Contains(mn) || JmpMnemonics.Contains(mn))
                {
                    UInt64? tgt = DirectTarget(ins);
                    if (tgt.HasValue && VaInExecutableSection(tgt.Value) && !Functions.ContainsKey(tgt.Value))
                        newTargets.Add(tgt.Value);
                }
            }
        }
        foreach (UInt64 t in newTargets)
            discovered[t] = $"sub_{t:X8}";

        Functions.Clear();
        foreach (KeyValuePair<UInt64, String> kv in discovered)
            Functions[kv.Key] = kv.Value;
        _fnSorted = Functions.Keys.OrderBy(x => x).ToArray();
    }

    public X86Instruction[] DisasmFunctionCached(UInt64 fnVa, Int32 maxBytes = 0x4000)
    {
        if (_insnCache.TryGetValue(fnVa, out X86Instruction[]? cached)) return cached;
        Int32 limit = maxBytes;
        UInt64? nxt = NextFunctionVa(fnVa);
        if (nxt.HasValue && nxt.Value > fnVa)
            limit = Math.Min(limit, (Int32)(nxt.Value - fnVa));
        Byte[]? data = ReadBytesAtVA(fnVa, limit);
        X86Instruction[] insns = data != null ? _disassembler.Disassemble(data, (Int64)fnVa) : Array.Empty<X86Instruction>();
        _insnCache[fnVa] = insns;
        return insns;
    }

    public (Int32 Category, String? Fname, String? Dll) ResolveCall(X86Instruction ins)
    {
        X86Operand[] ops = GetOperands(ins);
        if (ops.Length == 0) return (CALL_UNKNOWN, null, null);
        X86Operand op = ops[0];

        if (op.Type == X86OperandType.Immediate)
        {
            UInt64 tgt = (UInt64)op.Immediate;
            if (Iat.TryGetValue(tgt, out (String Dll, String Fname) v))
                return (CALL_DIRECT_WINAPI, v.Fname, v.Dll);
            if (Functions.TryGetValue(tgt, out String? fn))
                return (CALL_INTERNAL, fn, null);
            return (CALL_UNKNOWN, null, null);
        }

        if (op.Type == X86OperandType.Memory)
        {
            X86RegisterId baseId = op.Memory.Base?.Id ?? X86RegisterId.Invalid;
            UInt64? target = null;
            if (baseId == X86RegisterId.Invalid)
                target = (UInt64)op.Memory.Displacement;
            else if (baseId == X86RegisterId.X86_REG_RIP)
                target = (UInt64)(ins.Address + ins.Bytes.Length + op.Memory.Displacement);
            if (target.HasValue && Iat.TryGetValue(target.Value, out (String Dll, String Fname) v))
                return (CALL_INDIRECT_WINAPI, v.Fname, v.Dll);
            return (CALL_UNKNOWN, null, null);
        }

        return (CALL_UNKNOWN, null, null);
    }

    public List<BasicBlock> SplitBasicBlocks(X86Instruction[] insns)
    {
        List<BasicBlock> result = new();
        if (insns.Length == 0) return result;
        UInt64 fnVa = (UInt64)insns[0].Address;
        Int64 fnEndMax = insns[^1].Address + insns[^1].Bytes.Length;

        SortedSet<UInt64> leaders = new() { fnVa };
        foreach (X86Instruction ins in insns)
        {
            String mn = ins.Mnemonic;
            Int64 fall = ins.Address + ins.Bytes.Length;
            if (JccMnemonics.Contains(mn))
            {
                UInt64? tgt = DirectTarget(ins);
                if (tgt.HasValue && fnVa <= tgt.Value && tgt.Value < (UInt64)fnEndMax + 0x100)
                    leaders.Add(tgt.Value);
                if (fall < fnEndMax + 1)
                    leaders.Add((UInt64)fall);
            }
            else if (JmpMnemonics.Contains(mn))
            {
                UInt64? tgt = DirectTarget(ins);
                if (tgt.HasValue && fnVa <= tgt.Value && tgt.Value < (UInt64)fnEndMax + 0x100)
                    leaders.Add(tgt.Value);
            }
            else if (CallMnemonics.Contains(mn))
            {
                if (fall < fnEndMax + 1)
                    leaders.Add((UInt64)fall);
            }
        }

        UInt64[] leadersArr = leaders.ToArray();
        for (Int32 i = 0; i < leadersArr.Length; i++)
        {
            UInt64 leader = leadersArr[i];
            UInt64 end = i + 1 < leadersArr.Length ? leadersArr[i + 1] : (UInt64)fnEndMax;
            List<X86Instruction> blk = new();
            foreach (X86Instruction ins in insns)
            {
                if ((UInt64)ins.Address >= leader && (UInt64)ins.Address < end)
                    blk.Add(ins);
            }
            if (blk.Count > 0)
            {
                X86Instruction last = blk[^1];
                result.Add(new BasicBlock(leader, (UInt64)(last.Address + last.Bytes.Length), blk));
            }
        }
        return result;
    }

    public List<UInt64> BlockSuccessors(List<X86Instruction> blkIns, Dictionary<UInt64, BasicBlock> blocksByStart)
    {
        List<UInt64> succs = new();
        if (blkIns.Count == 0) return succs;
        X86Instruction last = blkIns[^1];
        String mn = last.Mnemonic;
        Int64 fall = last.Address + last.Bytes.Length;

        if (JccMnemonics.Contains(mn))
        {
            UInt64? tgt = DirectTarget(last);
            if (tgt.HasValue && blocksByStart.ContainsKey(tgt.Value))
                succs.Add(tgt.Value);
            if (blocksByStart.ContainsKey((UInt64)fall))
                succs.Add((UInt64)fall);
        }
        else if (JmpMnemonics.Contains(mn))
        {
            UInt64? tgt = DirectTarget(last);
            if (tgt.HasValue && blocksByStart.ContainsKey(tgt.Value))
                succs.Add(tgt.Value);
        }
        else if (CallMnemonics.Contains(mn))
        {
            if (blocksByStart.ContainsKey((UInt64)fall))
                succs.Add((UInt64)fall);
        }
        else if (!RetMnemonics.Contains(mn) && !TerminatorMnemonics.Contains(mn))
        {
            if (blocksByStart.ContainsKey((UInt64)fall))
                succs.Add((UInt64)fall);
        }
        return succs;
    }

    public Single[] EventFeature(X86Instruction ins, Int32 cat, String? fname, UInt64 fnVa)
    {
        UInt64 nh = NameHash(fname ?? String.Empty);
        (UInt16 nhLo, _) = Split64(nh);
        (UInt16 fnLo, _) = Split64(fnVa);
        (UInt16 callLo, _) = Split64((UInt64)ins.Address);
        Int32 isWinapi = (cat == CALL_DIRECT_WINAPI || cat == CALL_INDIRECT_WINAPI) ? 1 : 0;
        return new Single[]
        {
            NODE_TYPE_EVENT,
            nhLo & 0xFF,
            (nhLo >> 8) & 0xFF,
            fnLo & 0xFF,
            (fnLo >> 8) & 0xFF,
            callLo & 0xFF,
            isWinapi,
            cat
        };
    }

    public static Single[] NodeFeatureFromAttrs(Int32 nodeType, String name, UInt64 va)
    {
        UInt64 nh = NameHash(name);
        (UInt16 nhLo, UInt16 nhHi) = Split64(nh);
        if (nodeType == NODE_TYPE_PE || nodeType == NODE_TYPE_DLL)
            return new Single[] { nodeType, nhLo & 0xFF, (nhLo >> 8) & 0xFF, nhHi & 0xFF, (nhHi >> 8) & 0xFF, 0, 0, 0 };
        (UInt16 vaLo, UInt16 vaHi) = Split64(va);
        return new Single[] { nodeType, nhLo & 0xFF, (nhLo >> 8) & 0xFF, vaLo & 0xFF, (vaLo >> 8) & 0xFF, vaHi & 0xFF, (vaHi >> 8) & 0xFF, 0 };
    }

    private static Int32 IndexOf(Byte[] data, Byte[] pattern, Int32 start)
    {
        Int32 max = data.Length - pattern.Length;
        for (Int32 i = start; i <= max; i++)
        {
            Boolean match = true;
            for (Int32 j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disassembler.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

internal readonly record struct SectionInfo(UInt64 Start, UInt64 End, UInt32 Chars, UInt32 RawPtr, UInt32 RawSize, UInt32 VirtualAddress, UInt32 VirtualSize, String Name);

internal readonly record struct BasicBlock(UInt64 Leader, UInt64 End, List<Gee.External.Capstone.X86.X86Instruction> Instructions);
