using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

using PeNet;
using PeNet.Header.Pe;
using Gee.External.Capstone;
using Gee.External.Capstone.Arm;
using Gee.External.Capstone.Arm64;
using Gee.External.Capstone.X86;

using XaocSirck_Core.Interface.Feature;

namespace XaocSirck_Core.Feature.Obtain;

internal sealed unsafe class AssemblyListObtain : IFeatureObtain
{
    private readonly Int32 _maxCount = 512;
    private readonly Int32 _compressedTokenCount = 512;
    private readonly UInt32 _maxDisassembleBytes = 256 * 1024;
    private readonly UInt32 _imageScnMemExecute = 0x20000000;
    private IntPtr _resultPtr = IntPtr.Zero;
    private String _inputData = String.Empty;
    private Boolean _disposed;
    private static readonly BpeEncoder _bpe = new();

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(AssemblyListObtain));
        if (_resultPtr != IntPtr.Zero)
        {
            NativeMemory.AlignedFree((void*)_resultPtr);
            _resultPtr = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Clear();
            _inputData = String.Empty;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public IntPtr GetResult()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(AssemblyListObtain));
        return _resultPtr;
    }

    public void Obtain()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(AssemblyListObtain));
        PeFile pe = new(_inputData);
        UInt16 machineType = (UInt16)(pe.ImageNtHeaders?.FileHeader.Machine ?? 0);
        ImageSectionHeader[]? sections = pe.ImageSectionHeaders;
        if (sections == null) return;

        ImageSectionHeader? execSection = null;
        UInt32 maxRawSize = 0;
        foreach (ImageSectionHeader s in sections)
        {
            if (((UInt32)s.Characteristics & _imageScnMemExecute) != 0 && s.SizeOfRawData > maxRawSize)
            {
                execSection = s;
                maxRawSize = s.SizeOfRawData;
            }
        }
        if (execSection == null || pe.ImageNtHeaders?.OptionalHeader.ImageBase == null) return;

        UInt32 rawSize = Math.Min(execSection.SizeOfRawData, _maxDisassembleBytes);
        UInt32 rawOffset = execSection.PointerToRawData;
        if (rawSize == 0)
            return;

        Byte[] sectionData = new Byte[rawSize];
        using (FileStream fs = new(_inputData, FileMode.Open, FileAccess.Read))
        {
            fs.Seek(rawOffset, SeekOrigin.Begin);
            fs.ReadExactly(sectionData);
        }
        Int64 baseAddr = (Int64)((pe.ImageNtHeaders?.OptionalHeader.ImageBase ?? 0) + execSection.VirtualAddress);
        String[] mnemonics = Disassemble(machineType, sectionData, baseAddr);
        if (mnemonics.Length == 0) return;

        String[] rawTokens = new String[_maxCount];
        Int32 tokenCount = 0;
        Int32 idx = 0;
        while (idx < mnemonics.Length && tokenCount < _maxCount)
        {
            if (mnemonics[idx] == "int3")
            {
                Int32 runLen = 1;
                while (idx + runLen < mnemonics.Length && mnemonics[idx + runLen] == "int3")
                    runLen++;
                rawTokens[tokenCount++] = runLen > 1 ? "int3_list" : "int3";
                idx += runLen;
            }
            else
            {
                rawTokens[tokenCount++] = mnemonics[idx];
                idx++;
            }
        }
        if (tokenCount == 0) return;

        if (tokenCount < _maxCount)
            Array.Resize(ref rawTokens, tokenCount);

        Int32[] encoded = _bpe.Encode(rawTokens);
        if (encoded.Length == 0) return;

        Int32 totalSize = sizeof(Int32) + _compressedTokenCount * sizeof(Int32);
        _resultPtr = (IntPtr)NativeMemory.AlignedAlloc((UIntPtr)totalSize, 64);
        NativeMemory.Clear((void*)_resultPtr, (UIntPtr)totalSize);

        *(Int32*)_resultPtr = totalSize;
        Int32* idPtr = (Int32*)_resultPtr + 1;
        Int32 copyLen = Math.Min(encoded.Length, _compressedTokenCount);
        for (Int32 i = 0; i < copyLen; i++)
            idPtr[i] = encoded[i];
    }

    private static String[] Disassemble(UInt16 machineType, Byte[] sectionData, Int64 baseAddr)
    {
        switch (machineType)
        {
            case 0x14c:
                {
                    using CapstoneX86Disassembler disassembler = CapstoneDisassembler.CreateX86Disassembler(X86DisassembleMode.Bit32);
                    X86Instruction[] instructions = disassembler.Disassemble(sectionData, baseAddr);
                    String[] result = new String[instructions.Length];
                    for (Int32 i = 0; i < instructions.Length; i++)
                        result[i] = instructions[i].Mnemonic;
                    return result;
                }
            case 0x8664:
                {
                    using CapstoneX86Disassembler disassembler = CapstoneDisassembler.CreateX86Disassembler(X86DisassembleMode.Bit64);
                    X86Instruction[] instructions = disassembler.Disassemble(sectionData, baseAddr);
                    String[] result = new String[instructions.Length];
                    for (Int32 i = 0; i < instructions.Length; i++)
                        result[i] = instructions[i].Mnemonic;
                    return result;
                }
            case 0x1c0:
                {
                    using CapstoneArmDisassembler disassembler = CapstoneDisassembler.CreateArmDisassembler(ArmDisassembleMode.LittleEndian);
                    ArmInstruction[] instructions = disassembler.Disassemble(sectionData, baseAddr);
                    String[] result = new String[instructions.Length];
                    for (Int32 i = 0; i < instructions.Length; i++)
                        result[i] = instructions[i].Mnemonic;
                    return result;
                }
            case 0xaa64:
                {
                    using CapstoneArm64Disassembler disassembler = CapstoneDisassembler.CreateArm64Disassembler(Arm64DisassembleMode.LittleEndian);
                    Arm64Instruction[] instructions = disassembler.Disassemble(sectionData, baseAddr);
                    String[] result = new String[instructions.Length];
                    for (Int32 i = 0; i < instructions.Length; i++)
                        result[i] = instructions[i].Mnemonic;
                    return result;
                }
            default:
                return [];
        }
    }

    public void Set(Object inputData)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(AssemblyListObtain));
        throw new NotImplementedException();
    }
}

internal sealed class BpeEncoder
{
    private readonly String _vocabPath = Path.Combine(AppContext.BaseDirectory, "XaocSirck", "AssemblyList", "platforms_mnemonics_vocab.bin");

    private Dictionary<String, Int32>? _baseTokenToId;
    private List<(Int32 LeftId, Int32 RightId)>? _mergeRules;
    private Int32 _baseVocabEnd;
    private Boolean _loaded;
    private readonly Lock _lock = new();

    public Int32[] Encode(String[] rawTokens)
    {
        if (!_loaded) Load();

        Int32[] ids = new Int32[rawTokens.Length];
        for (Int32 i = 0; i < rawTokens.Length; i++)
            ids[i] = _baseTokenToId!.TryGetValue(rawTokens[i], out Int32 id) ? id : 0;

        if (_mergeRules != null)
        {
            Int32 mergeBase = _baseVocabEnd;
            for (Int32 mi = 0; mi < _mergeRules.Count; mi++)
            {
                var (left, right) = _mergeRules[mi];
                Int32 mergedId = mergeBase + mi;

                Int32 pairCount = 0;
                Int32 checkIdx = 0;
                while (checkIdx < ids.Length - 1)
                {
                    if (ids[checkIdx] == left && ids[checkIdx + 1] == right)
                    {
                        pairCount++;
                        checkIdx += 2;
                    }
                    else
                    {
                        checkIdx++;
                    }
                }

                if (pairCount == 0) continue;

                Int32 newLen = ids.Length - pairCount;
                Int32[] merged = new Int32[newLen];
                Int32 wi = 0, ri = 0;
                while (ri < ids.Length)
                {
                    if (ri < ids.Length - 1 && ids[ri] == left && ids[ri + 1] == right)
                    {
                        merged[wi++] = mergedId;
                        ri += 2;
                    }
                    else
                    {
                        merged[wi++] = ids[ri];
                        ri++;
                    }
                }
                ids = merged;
            }
        }
        return ids;
    }

    private void Load()
    {
        if (_loaded) return;
        lock (_lock)
        {
            if (_loaded) return;

            if (!File.Exists(_vocabPath))
                throw new FileNotFoundException($"BPE vocab not found: {_vocabPath}");
            String path = _vocabPath;

            using FileStream fs = new(path, FileMode.Open, FileAccess.Read);
            using BinaryReader br = new(fs);
            Int32 count = br.ReadInt32();

            _baseTokenToId = new Dictionary<String, Int32>(count, StringComparer.OrdinalIgnoreCase);
            _mergeRules = [];

            for (Int32 i = 0; i < count; i++)
            {
                Int32 len = br.ReadInt32();
                String name = Encoding.UTF8.GetString(br.ReadBytes(len));
                Int32 leftId = br.ReadInt32();
                Int32 rightId = br.ReadInt32();

                if (leftId == -1)
                {
                    _baseTokenToId[name] = i;
                    _baseVocabEnd = i + 1;
                }
                else
                {
                    _mergeRules.Add((leftId, rightId));
                }
            }

            _loaded = true;
        }
    }
}
