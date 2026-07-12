using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PeNet;
using PeNet.Header.Pe;
using XaocSirck_Core.Interface.Feature.Zeroflows;
namespace XaocSirck_Core.Feature.Engineering.Zeroflows;
internal sealed unsafe class PeMetadata : IPeEngineering, IDisposable
{
    private const Int32 ResultSize = 131;

    private const UInt16 ImageFileDll = 0x2000;

    private const UInt32 ImageScnMemExecute = 0x20000000;
    private const UInt32 ImageScnMemRead = 0x40000000;
    private const UInt32 ImageScnMemWrite = 0x80000000;

    private const UInt16 ImageDllcharDynamicBase = 0x0040;
    private const UInt16 ImageDllcharForceIntegrity = 0x0080;
    private const UInt16 ImageDllcharNxCompat = 0x0100;
    private const UInt16 ImageDllcharNoSeh = 0x0400;

    private const UInt16 SubsystemUnknown = 0;
    private const UInt16 SubsystemNative = 1;

    private const UInt16 Pe32Magic = 0x10b;
    private const UInt16 Pe32PlusMagic = 0x20b;

    private const Int32 MaxEntropyBytes = 262144;
    private const Int32 EntropySampleBlock = 4096;
    private const Int32 MaxStringScanBytes = 65536;
    private const Int32 MaxPatternStrings = 4096;
    private const Int32 MaxStringEntropyBytes = 65536;
    private const Int32 MaxApiNamesToScan = 1024;
    private const Int32 MaxApisPerDll = 4096;

    private const UInt64 HashVirtualAlloc = 0xFA55E32C9D72A921UL;
    private const UInt64 HashVirtualProtect = 0xED1006223ABBBD53UL;
    private const UInt64 HashWriteProcessMemory = 0x565A90AAA0DEAACAUL;
    private const UInt64 HashReadProcessMemory = 0x133228517DE45EE5UL;
    private const UInt64 HashCreateRemoteThread = 0x6829CB75CBA67403UL;
    private const UInt64 HashNtUnmapViewOfSection = 0x245263896720BBE5UL;
    private const UInt64 HashSetWindowsHookEx = 0x4D43BA61C0D4500AUL;
    private const UInt64 HashWinExec = 0x68AF786D5780A750UL;
    private const UInt64 HashShellExecuteA = 0x2F8130953282E3BFUL;
    private const UInt64 HashShellExecuteW = 0x2F813E953282FB89UL;
    private const UInt64 HashCreateProcessA = 0x9C757B1C853AEF89UL;
    private const UInt64 HashCreateProcessW = 0x9C756D1C853AD7BFUL;
    private const UInt64 HashInternetOpenA = 0x8261F0DF5FDC0887UL;
    private const UInt64 HashInternetOpenW = 0x8261DEDF5FDBE9F1UL;
    private const UInt64 HashURLDownloadToFileA = 0xC7A13AA8E5DF96FCUL;
    private const UInt64 HashURLDownloadToFileW = 0xC7A128A8E5DF7866UL;
    private const UInt64 HashCryptEncrypt = 0x42BB12DA842AA184UL;
    private const UInt64 HashRegSetValueExA = 0x0B9311092E0DA3FCUL;
    private const UInt64 HashRegSetValueExW = 0x0B92FF092E0D8566UL;
    private const UInt64 HashLoadLibrary = 0x77CFAE4F71D3ED74UL;
    private const UInt64 HashLoadLibraryA = 0x69D265FE6B1C110FUL;
    private const UInt64 HashLoadLibraryW = 0x69D253FE6B1BF279UL;
    private const UInt64 HashLoadLibraryExA = 0x8E5761F119058600UL;
    private const UInt64 HashLoadLibraryExW = 0x8E5777F11905AB62UL;
    private const UInt64 HashGetProcAddress = 0x578960F1FC7FFF25UL;

    private const UInt64 HashKernel32Dll = 0xE14B18A7ACF9C443UL;
    private const UInt64 HashNtdllDll = 0xBB7BB9A74C2F14FBUL;
    private const UInt64 HashAdvapi32Dll = 0xD98AEB041E16F7B5UL;
    private const UInt64 HashWs232Dll = 0x4C31D0DADDF9ADE3UL;
    private const UInt64 HashWininetDll = 0x32EACF116EB05411UL;
    private const UInt64 HashCrypt32Dll = 0x92BF98D2DF731CD8UL;

    private readonly ShareFeatures _share;
    private readonly Int32 _offset;
    private Boolean _disposed;

    public PeMetadata(ShareFeatures share, Int32 offset = FeatureLayout.PeMetadataOffset)
    {
        ArgumentNullException.ThrowIfNull(share);
        _share = share;
        _offset = offset;
    }

    public void Process(PeFile pe, Int64 fileSize)
    {
        Single* output = _share.FeatureTensor + _offset;
        NativeMemory.Clear(output, (UIntPtr)(ResultSize * sizeof(Single)));

        ImageNtHeaders? ntHeaders = pe?.ImageNtHeaders;
        Boolean isValid = ntHeaders?.FileHeader != null;
        output[0] = isValid ? 1.0f : 0.0f;
        if (!isValid)
            return;

        ImageFileHeader fileHeader = ntHeaders!.FileHeader!;
        ImageOptionalHeader optionalHeader = ntHeaders.OptionalHeader;
        ImageSectionHeader[]? sections = pe!.ImageSectionHeaders;

        UInt16 characteristics = (UInt16)fileHeader.Characteristics;
        output[1] = (characteristics & ImageFileDll) != 0 ? 1.0f : 0.0f;
        output[2] = (UInt16?)optionalHeader?.Magic == Pe32PlusMagic ? 1.0f : 0.0f;

        Int32 sectionCount = sections?.Length ?? 0;
        output[3] = (Single)sectionCount;
        output[4] = sectionCount > 16 || sectionCount < 1 ? 1.0f : 0.0f;

        output[5] = pe!.IsDotNet ? 1.0f : 0.0f;
        output[6] = pe!.IsDriver ? 1.0f : 0.0f;
        output[7] = pe!.IsAuthenticodeSigned ? 1.0f : 0.0f;

        UInt32 entryPoint = optionalHeader?.AddressOfEntryPoint ?? 0;
        output[8] = Log2Normalized(entryPoint + 1);
        output[12] = entryPoint == 0 ? 1.0f : 0.0f;

        UInt32 imageSize = optionalHeader?.SizeOfImage ?? 0;
        UInt32 headersSize = optionalHeader?.SizeOfHeaders ?? 0;
        UInt32 codeSize = optionalHeader?.SizeOfCode ?? 0;
        UInt32 initDataSize = optionalHeader?.SizeOfInitializedData ?? 0;
        UInt32 uninitDataSize = optionalHeader?.SizeOfUninitializedData ?? 0;
        UInt64 imageBase = optionalHeader?.ImageBase ?? 0;
        UInt32 sectionAlignment = optionalHeader?.SectionAlignment ?? 0x1000;
        UInt32 fileAlignment = optionalHeader?.FileAlignment ?? 0x200;
        UInt16 subsystem = (UInt16)(optionalHeader?.Subsystem ?? 0);
        UInt16 dllCharacteristics = (UInt16)(optionalHeader?.DllCharacteristics ?? 0);
        UInt32 checkSum = optionalHeader?.CheckSum ?? 0;
        UInt32 numberOfRvaAndSizes = optionalHeader?.NumberOfRvaAndSizes ?? 0;
        UInt16 optionalHeaderSize = fileHeader.SizeOfOptionalHeader;
        UInt16 magic = (UInt16)(optionalHeader?.Magic ?? 0);

        output[54] = sectionAlignment != 0x1000 && sectionAlignment != 0x2000 && sectionAlignment != 0x10000 ? 1.0f : 0.0f;
        output[55] = fileAlignment != 0x200 && fileAlignment != 0x1000 ? 1.0f : 0.0f;
        output[56] = fileAlignment > 0 ? sectionAlignment / (Single)fileAlignment : 0.0f;
        output[57] = Log2Normalized(imageBase + 1);
        output[58] = imageBase == 0x10000 || imageBase == 0x140000000 ? 1.0f : 0.0f;

        output[13] = Log2Normalized(imageSize + 1);
        output[14] = Log2Normalized(headersSize + 1);
        output[15] = Log2Normalized(codeSize + 1);
        output[16] = imageSize > 0 ? codeSize / (Single)imageSize : 0.0f;
        output[17] = imageSize > 0 ? (initDataSize + uninitDataSize) / (Single)imageSize : 0.0f;
        output[18] = imageSize > 0 ? uninitDataSize / (Single)imageSize : 0.0f;
        output[19] = codeSize / (Single)(initDataSize + uninitDataSize + 1);

        output[115] = imageSize > 0 ? entryPoint / (Single)imageSize : 0.0f;
        output[124] = entryPoint > 0 && entryPoint < 0x1000 ? 1.0f : 0.0f;
        output[126] = imageSize > 0 ? headersSize / (Single)imageSize : 0.0f;

        output[20] = (Single)subsystem;
        output[21] = subsystem == SubsystemUnknown || subsystem == SubsystemNative ? 1.0f : 0.0f;

        output[22] = (Single)dllCharacteristics;
        output[23] = (dllCharacteristics & ImageDllcharNxCompat) != 0 ? 1.0f : 0.0f;
        output[24] = (dllCharacteristics & ImageDllcharDynamicBase) != 0 ? 1.0f : 0.0f;
        output[25] = (dllCharacteristics & ImageDllcharNoSeh) == 0 ? 1.0f : 0.0f;
        output[26] = (dllCharacteristics & ImageDllcharForceIntegrity) != 0 ? 1.0f : 0.0f;

        output[27] = checkSum != 0 ? 1.0f : 0.0f;
        output[28] = IsChecksumMismatched(pe, checkSum) ? 1.0f : 0.0f;

        output[29] = (Single)(optionalHeader?.MajorLinkerVersion ?? 0);
        output[30] = (Single)(optionalHeader?.MinorLinkerVersion ?? 0);
        output[31] = (Single)(optionalHeader?.MajorOperatingSystemVersion ?? 0);
        output[32] = (Single)(optionalHeader?.MinorOperatingSystemVersion ?? 0);
        output[33] = (Single)(optionalHeader?.MajorSubsystemVersion ?? 0);
        output[34] = (optionalHeader?.MajorLinkerVersion == 0 && optionalHeader?.MinorLinkerVersion == 0) ||
                     (optionalHeader?.MajorOperatingSystemVersion > 20) ? 1.0f : 0.0f;

        ReadOnlySpan<Byte> rawSpan = pe.RawFile != null ? pe.RawFile.AsSpan(0, pe.RawFile.Length) : ReadOnlySpan<Byte>.Empty;
        Int32 rawLength = rawSpan.Length;
        UInt32 e_lfanew = pe.ImageDosHeader?.E_lfanew ?? 0;

        Int64 overlayStart = GetOverlayStart(sections);

        if (rawLength > 0)
        {
            fixed (Byte* rawPtr = rawSpan)
            {
                ProcessSections(output, sections, rawPtr, rawLength, entryPoint, imageSize, fileSize, headersSize);
                ProcessOverlay(output, rawPtr, rawLength, sections, fileSize, overlayStart);
                ProcessRichHeader(output, rawPtr, rawLength, e_lfanew);
                ProcessImports(output, optionalHeader, sections, rawPtr, rawLength);
                ProcessStrings(output, rawPtr, rawLength, overlayStart);
            }
        }
        else
        {
            ProcessSections(output, sections, null, 0, entryPoint, imageSize, fileSize, headersSize);
            ProcessOverlay(output, null, 0, sections, fileSize, overlayStart);
            ProcessRichHeader(output, null, 0, e_lfanew);
            ProcessImports(output, optionalHeader, sections, null, 0);
            ProcessStrings(output, null, 0, overlayStart);
        }

        ProcessDataDirectories(output, optionalHeader);

        output[80] = optionalHeaderSize;
        output[81] = magic != Pe32Magic && magic != Pe32PlusMagic ? 1.0f : 0.0f;
        output[82] = numberOfRvaAndSizes != 16 ? 1.0f : 0.0f;

        output[83] = (Single)Math.Max(0, e_lfanew - 64);
    }

    private static void ProcessSections(Single* output, ImageSectionHeader[]? sections, Byte* rawPtr, Int32 rawLength, UInt32 entryPoint, UInt32 imageSize, Int64 fileSize, UInt32 headersSize)
    {
        Int32 sectionCount = sections?.Length ?? 0;
        if (sectionCount == 0 || rawPtr == null)
        {
            output[35] = 0.0f;
            output[36] = 0.0f;
            output[37] = 0.0f;
            output[38] = 0.0f;
            output[39] = 0.0f;
            output[40] = 0.0f;
            output[41] = 0.0f;
            output[42] = 0.0f;
            output[43] = 0.0f;
            output[44] = 0.0f;
            output[45] = 0.0f;
            output[46] = 0.0f;
            output[47] = 0.0f;
            output[48] = 0.0f;
            output[49] = 0.0f;
            output[50] = 0.0f;
            output[51] = 0.0f;
            output[52] = 0.0f;
            output[53] = 0.0f;
            output[54] = 0.0f;
        output[55] = 0.0f;
        output[116] = 0.0f;
        output[117] = 0.0f;
        output[118] = 0.0f;
        output[119] = 0.0f;
        output[120] = 0.0f;
        output[121] = 0.0f;
        output[122] = 0.0f;
        output[123] = 0.0f;
        output[125] = 0.0f;
        output[127] = 0.0f;
        output[128] = 0.0f;
        output[129] = 0.0f;
        output[130] = 0.0f;
            return;
        }

        Single totalNameEntropy = 0.0f;
        Single maxNameEntropy = 0.0f;
        Int32 suspiciousNameCount = 0;

        Single maxSectionEntropy = 0.0f;
        Single minSectionEntropy = Single.MaxValue;
        Single sumSectionEntropy = 0.0f;
        Int32 sectionEntropyCount = 0;

        Single codeSectionEntropy = 0.0f;
        Single dataSectionEntropy = 0.0f;
        Single rsrcSectionEntropy = 0.0f;
        Boolean rsrcPresent = false;
        Single rsrcSizeRatio = 0.0f;
        Boolean relocPresent = false;
        Single relocSizeRatio = 0.0f;
        Boolean tlsPresent = false;
        Boolean writableCode = false;
        Boolean executableData = false;
        Boolean readableCode = false;

        Int32* permBuckets = stackalloc Int32[8];
        Single* sectionEntropies = null;
        Boolean allocatedEntropies = false;
        if (sectionCount > 0)
        {
            sectionEntropies = (Single*)NativeMemory.AlignedAlloc((UIntPtr)(sectionCount * sizeof(Single)), 64);
            allocatedEntropies = true;
        }

        Boolean epInCode = false;
        Boolean epInLast = false;
        Boolean epInFirst = false;
        Boolean epInAnySection = false;

        UInt64 codeSectionsTotalSize = 0;
        Int32 readableCount = 0;
        Int32 writableCount = 0;
        Int32 executableCount = 0;
        Int32 rwxCount = 0;
        Int32 nonAsciiNameCount = 0;
        Int32 virtualRawMismatchCount = 0;
        Int32 duplicateNameCount = 0;
        Int32 zeroVirtualSizeCount = 0;
        Int32 zeroRawSizeCount = 0;
        Int32 rawLargerThanVirtualCount = 0;
        Int32 wxCount = 0;

        const Int32 MaxTrackedNames = 128;
        UInt64* nameHashes = stackalloc UInt64[MaxTrackedNames];
        Int32 trackedNames = 0;

        for (Int32 i = 0; i < sectionCount; i++)
        {
            ImageSectionHeader s = sections![i];
            String name = s.Name ?? String.Empty;
            Single nameEntropy = CalculateStringEntropy(name);
            totalNameEntropy += nameEntropy;
            maxNameEntropy = Math.Max(maxNameEntropy, nameEntropy);
            if (IsSuspiciousSectionName(name))
                suspiciousNameCount++;

            UInt32 rawSize = s.SizeOfRawData;
            UInt32 rawOffset = s.PointerToRawData;
            UInt32 virtualAddress = s.VirtualAddress;
            UInt32 virtualSize = s.VirtualSize;
            UInt32 characteristics = (UInt32)s.Characteristics;

            Boolean isReadable = (characteristics & ImageScnMemRead) != 0;
            Boolean isWritable = (characteristics & ImageScnMemWrite) != 0;
            Boolean isExecutable = (characteristics & ImageScnMemExecute) != 0;
            if (isReadable) readableCount++;
            if (isWritable) writableCount++;
            if (isExecutable) executableCount++;
            if (isReadable && isWritable && isExecutable) rwxCount++;

            if (rawSize > 0 && virtualSize > rawSize * 4)
                virtualRawMismatchCount++;
            if (virtualSize == 0)
                zeroVirtualSizeCount++;
            if (rawSize == 0)
                zeroRawSizeCount++;
            if (rawSize > 0 && rawSize > virtualSize)
                rawLargerThanVirtualCount++;
            if (isWritable && isExecutable)
                wxCount++;

            if (isExecutable)
                codeSectionsTotalSize += rawSize;

            Byte* nameBytes = (Byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(name.AsSpan()));
            Int32 nameLen = name.Length;
            Boolean hasNonAscii = false;
            for (Int32 n = 0; n < nameLen; n++)
            {
                if (nameBytes[n] > 126 || (nameBytes[n] < 32 && nameBytes[n] != 0))
                {
                    hasNonAscii = true;
                    break;
                }
            }
            if (hasNonAscii)
                nonAsciiNameCount++;

            UInt64 nameHash = 0;
            Int32 hashLen = Math.Min(nameLen, 8);
            for (Int32 n = 0; n < hashLen; n++)
                nameHash |= (UInt64)nameBytes[n] << (n * 8);
            Boolean isDuplicate = false;
            for (Int32 k = 0; k < trackedNames && k < MaxTrackedNames; k++)
            {
                if (nameHashes[k] == nameHash)
                {
                    isDuplicate = true;
                    break;
                }
            }
            if (isDuplicate)
                duplicateNameCount++;
            if (trackedNames < MaxTrackedNames)
                nameHashes[trackedNames++] = nameHash;

            Boolean isCode = IsCodeSectionName(name) || isExecutable;
            Boolean isData = IsDataSectionName(name);
            Boolean isRsrc = name.Equals(".rsrc", StringComparison.OrdinalIgnoreCase);
            Boolean isReloc = name.Equals(".reloc", StringComparison.OrdinalIgnoreCase);
            Boolean isTls = name.Equals(".tls", StringComparison.OrdinalIgnoreCase);

            Single sectionEntropy = -1.0f;
            if (rawSize > 0 && rawOffset + rawSize <= (UInt32)rawLength)
                sectionEntropy = CalculateEntropy(rawPtr + rawOffset, (Int32)rawSize);
            sectionEntropies![i] = sectionEntropy;

            if (sectionEntropy >= 0.0f)
            {
                maxSectionEntropy = Math.Max(maxSectionEntropy, sectionEntropy);
                minSectionEntropy = Math.Min(minSectionEntropy, sectionEntropy);
                sumSectionEntropy += sectionEntropy;
                sectionEntropyCount++;
            }

            if (isCode)
            {
                codeSectionEntropy = sectionEntropy;
                if ((characteristics & ImageScnMemWrite) != 0)
                    writableCode = true;
                if ((characteristics & ImageScnMemRead) != 0)
                    readableCode = true;
            }
            if (isData && (characteristics & ImageScnMemExecute) != 0)
                executableData = true;

            if (isRsrc)
            {
                rsrcPresent = true;
                rsrcSectionEntropy = sectionEntropy;
                rsrcSizeRatio = imageSize > 0 ? rawSize / (Single)imageSize : 0.0f;
            }
            if (isReloc)
            {
                relocPresent = true;
                relocSizeRatio = imageSize > 0 ? rawSize / (Single)imageSize : 0.0f;
            }
            if (isTls)
                tlsPresent = true;

            Int32 permBucket = 0;
            if ((characteristics & ImageScnMemRead) != 0) permBucket |= 1;
            if ((characteristics & ImageScnMemWrite) != 0) permBucket |= 2;
            if ((characteristics & ImageScnMemExecute) != 0) permBucket |= 4;
            permBuckets[permBucket]++;

            if (entryPoint != 0 && virtualAddress <= entryPoint && entryPoint < virtualAddress + virtualSize)
            {
                epInAnySection = true;
                if (isCode)
                    epInCode = true;
                if (i == 0)
                    epInFirst = true;
                if (i == sectionCount - 1)
                    epInLast = true;
            }
        }

        output[9] = epInCode ? 1.0f : 0.0f;
        output[10] = epInLast ? 1.0f : 0.0f;
        output[11] = epInFirst ? 1.0f : 0.0f;

        Single meanNameEntropy = sectionCount > 0 ? totalNameEntropy / sectionCount : 0.0f;
        output[35] = totalNameEntropy;
        output[36] = (Single)suspiciousNameCount;
        output[37] = maxNameEntropy;
        output[38] = meanNameEntropy;

        Single meanSectionEntropy = sectionEntropyCount > 0 ? sumSectionEntropy / sectionEntropyCount : 0.0f;
        Single varianceSectionEntropy = 0.0f;
        for (Int32 i = 0; i < sectionCount; i++)
        {
            Single sectionEntropy = sectionEntropies![i];
            if (sectionEntropy < 0.0f)
                continue;
            Single d = sectionEntropy - meanSectionEntropy;
            varianceSectionEntropy += d * d;
        }
        varianceSectionEntropy = sectionEntropyCount > 0 ? varianceSectionEntropy / sectionEntropyCount : 0.0f;

        if (allocatedEntropies)
            NativeMemory.AlignedFree(sectionEntropies);

        output[39] = maxSectionEntropy;
        output[40] = minSectionEntropy == Single.MaxValue ? 0.0f : minSectionEntropy;
        output[41] = varianceSectionEntropy;
        output[42] = codeSectionEntropy;
        output[43] = dataSectionEntropy;
        output[44] = rsrcPresent ? 1.0f : 0.0f;
        output[45] = rsrcSizeRatio;
        output[46] = rsrcSectionEntropy;
        output[47] = relocPresent ? 1.0f : 0.0f;
        output[48] = relocSizeRatio;
        output[49] = tlsPresent ? 1.0f : 0.0f;
        output[50] = writableCode ? 1.0f : 0.0f;
        output[51] = executableData ? 1.0f : 0.0f;
        output[52] = readableCode ? 1.0f : 0.0f;
        output[53] = CalculateBucketEntropy(permBuckets, 8, sectionCount);

        output[116] = fileSize > 0 ? codeSectionsTotalSize / (Single)fileSize : 0.0f;
        output[117] = (Single)readableCount;
        output[118] = (Single)writableCount;
        output[119] = (Single)executableCount;
        output[120] = (Single)rwxCount;
        output[121] = (Single)nonAsciiNameCount;
        output[122] = (Single)virtualRawMismatchCount;
        output[123] = entryPoint != 0 && !epInAnySection ? 1.0f : 0.0f;
        output[125] = (Single)duplicateNameCount;
        output[127] = (Single)zeroVirtualSizeCount;
        output[128] = (Single)zeroRawSizeCount;
        output[129] = (Single)rawLargerThanVirtualCount;
        output[130] = (Single)wxCount;
    }

    private static Int64 GetOverlayStart(ImageSectionHeader[]? sections)
    {
        Int64 overlayStart = 0;
        if (sections != null)
        {
            foreach (ImageSectionHeader s in sections)
            {
                Int64 end = (Int64)s.PointerToRawData + s.SizeOfRawData;
                if (end > overlayStart)
                    overlayStart = end;
            }
        }
        if (overlayStart < 0x40)
            overlayStart = 0x40;
        return overlayStart;
    }

    private static void ProcessOverlay(Single* output, Byte* rawPtr, Int32 rawLength, ImageSectionHeader[]? sections, Int64 fileSize, Int64 overlayStart)
    {
        Int64 overlaySize = Math.Max(0, fileSize - overlayStart);
        output[59] = overlaySize;
        output[60] = fileSize > 0 ? overlaySize / (Single)fileSize : 0.0f;
        output[61] = overlaySize > 0 && rawPtr != null && overlayStart < rawLength
            ? CalculateEntropy(rawPtr + overlayStart, (Int32)overlaySize)
            : 0.0f;
        output[62] = overlaySize > 0 ? 1.0f : 0.0f;
        output[63] = overlaySize % 512;
    }

    private static void ProcessDataDirectories(Single* output, ImageOptionalHeader? optionalHeader)
    {
        ImageDataDirectory[]? directories = optionalHeader?.DataDirectory;
        Int32 dirCount = directories?.Length ?? 0;
        Int32 nonzeroCount = 0;
        Int32 presentMask = 0;
        for (Int32 i = 0; i < dirCount; i++)
        {
            if (directories![i].VirtualAddress != 0 || directories[i].Size != 0)
            {
                nonzeroCount++;
                if (i < 16)
                    presentMask |= 1 << i;
            }
        }

        output[64] = (presentMask & (1 << 0)) != 0 ? 1.0f : 0.0f;
        output[65] = (presentMask & (1 << 1)) != 0 ? 1.0f : 0.0f;
        output[66] = (presentMask & (1 << 2)) != 0 ? 1.0f : 0.0f;
        output[67] = (presentMask & (1 << 3)) != 0 ? 1.0f : 0.0f;
        output[68] = (presentMask & (1 << 4)) != 0 ? 1.0f : 0.0f;
        output[69] = (presentMask & (1 << 5)) != 0 ? 1.0f : 0.0f;
        output[70] = (presentMask & (1 << 6)) != 0 ? 1.0f : 0.0f;
        output[71] = (presentMask & (1 << 7)) != 0 ? 1.0f : 0.0f;
        output[72] = (presentMask & (1 << 8)) != 0 ? 1.0f : 0.0f;
        output[73] = (presentMask & (1 << 9)) != 0 ? 1.0f : 0.0f;
        output[74] = (presentMask & (1 << 10)) != 0 ? 1.0f : 0.0f;
        output[75] = (presentMask & (1 << 11)) != 0 ? 1.0f : 0.0f;
        output[76] = (presentMask & (1 << 12)) != 0 ? 1.0f : 0.0f;
        output[77] = (presentMask & (1 << 13)) != 0 ? 1.0f : 0.0f;
        output[78] = (presentMask & (1 << 14)) != 0 ? 1.0f : 0.0f;

        output[79] = (Single)nonzeroCount;
        output[80] = CalculateDataDirectoryEntropy(directories);
    }

    private static void ProcessRichHeader(Single* output, Byte* rawPtr, Int32 rawLength, UInt32 e_lfanew)
    {
        output[84] = 0.0f;
        output[85] = 0.0f;
        output[86] = 0.0f;

        if (rawPtr == null)
            return;

        Int32 scanEnd = Math.Min((Int32)e_lfanew, rawLength) - 8;
        for (Int32 i = 0x40; i < scanEnd; i++)
        {
            if (rawPtr[i] == 0x52 && rawPtr[i + 1] == 0x69 && rawPtr[i + 2] == 0x63 && rawPtr[i + 3] == 0x68)
            {
                UInt32 key = *(UInt32*)(rawPtr + i + 4);
                if (i >= 0x44)
                {
                    UInt32 firstDword = *(UInt32*)(rawPtr + 0x40) ^ key;
                    if (firstDword == 0x536E6144)
                    {
                        output[84] = 1.0f;
                        output[85] = key;
                        output[86] = key == 0 ? 1.0f : 0.0f;
                        return;
                    }
                }
            }
        }
    }

    public void Clear()
    {
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    private static Single Log2Normalized(UInt64 value)
    {
        if (value <= 1)
            return 0.0f;
        Int32 bits = 64 - System.Numerics.BitOperations.LeadingZeroCount(value);
        UInt64 power = 1UL << (bits - 1);
        Single ratio = (value - power) / (Single)power;
        return (bits - 1) + ratio;
    }

    private static Boolean IsChecksumMismatched(PeFile pe, UInt32 checkSum)
    {
        if (checkSum == 0)
            return false;
        return false; // lightweight: skip actual checksum verification
    }

    private static Single CalculateEntropy(Byte* data, Int32 length)
    {
        if (length <= 0)
            return 0.0f;

        Int32* f0 = stackalloc Int32[256];
        Int32* f1 = stackalloc Int32[256];
        Int32* f2 = stackalloc Int32[256];
        Int32* f3 = stackalloc Int32[256];

        if (length <= MaxEntropyBytes)
        {
            AccumulateFrequency4(f0, f1, f2, f3, data, length);
            MergeFrequency4(f0, f1, f2, f3);
            return EntropyFromFrequency(f0, length);
        }

        Int32 sampleBlockCount = MaxEntropyBytes / EntropySampleBlock;
        Int32 step = length / sampleBlockCount;
        Int32 totalSampled = 0;
        Int32 b = 0;

        while (b + 4 <= sampleBlockCount)
        {
            AccumulateFrequency4(f0, f1, f2, f3, data + (b + 0) * step, EntropySampleBlock);
            AccumulateFrequency4(f0, f1, f2, f3, data + (b + 1) * step, EntropySampleBlock);
            AccumulateFrequency4(f0, f1, f2, f3, data + (b + 2) * step, EntropySampleBlock);
            AccumulateFrequency4(f0, f1, f2, f3, data + (b + 3) * step, EntropySampleBlock);
            b += 4;
            totalSampled += 4 * EntropySampleBlock;
        }
        while (b < sampleBlockCount)
        {
            AccumulateFrequency4(f0, f1, f2, f3, data + b * step, EntropySampleBlock);
            b++;
            totalSampled += EntropySampleBlock;
        }

        MergeFrequency4(f0, f1, f2, f3);
        return EntropyFromFrequency(f0, totalSampled);
    }

    private static void AccumulateFrequency4(Int32* f0, Int32* f1, Int32* f2, Int32* f3, Byte* data, Int32 length)
    {
        Int32 i = 0;
        while (i + 16 <= length)
        {
            f0[data[i]]++;
            f1[data[i + 1]]++;
            f2[data[i + 2]]++;
            f3[data[i + 3]]++;
            f0[data[i + 4]]++;
            f1[data[i + 5]]++;
            f2[data[i + 6]]++;
            f3[data[i + 7]]++;
            f0[data[i + 8]]++;
            f1[data[i + 9]]++;
            f2[data[i + 10]]++;
            f3[data[i + 11]]++;
            f0[data[i + 12]]++;
            f1[data[i + 13]]++;
            f2[data[i + 14]]++;
            f3[data[i + 15]]++;
            i += 16;
        }
        while (i + 4 <= length)
        {
            f0[data[i]]++;
            f1[data[i + 1]]++;
            f2[data[i + 2]]++;
            f3[data[i + 3]]++;
            i += 4;
        }
        while (i < length)
        {
            f0[data[i]]++;
            i++;
        }
    }

    private static void MergeFrequency4(Int32* f0, Int32* f1, Int32* f2, Int32* f3)
    {
        Int32 i = 0;
        while (i + 4 <= 256)
        {
            f0[i] += f1[i] + f2[i] + f3[i];
            f0[i + 1] += f1[i + 1] + f2[i + 1] + f3[i + 1];
            f0[i + 2] += f1[i + 2] + f2[i + 2] + f3[i + 2];
            f0[i + 3] += f1[i + 3] + f2[i + 3] + f3[i + 3];
            i += 4;
        }
    }

    private static Single EntropyFromFrequency(Int32* freq, Int32 length)
    {
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

    private static Single CalculateStringEntropy(String value)
    {
        if (String.IsNullOrEmpty(value))
            return 0.0f;

        Int32* freq = stackalloc Int32[256];
        for (Int32 i = 0; i < value.Length; i++)
            freq[(Byte)value[i]]++;

        Single entropy = 0.0f;
        Single invLength = 1.0f / value.Length;
        for (Int32 i = 0; i < 256; i++)
        {
            if (freq[i] == 0)
                continue;
            Single p = freq[i] * invLength;
            entropy -= p * Log2Approx.Lookup(p);
        }
        return entropy;
    }

    private static Single CalculateBucketEntropy(Int32* buckets, Int32 bucketCount, Int32 total)
    {
        if (total <= 0)
            return 0.0f;
        Single entropy = 0.0f;
        Single invTotal = 1.0f / total;
        for (Int32 i = 0; i < bucketCount; i++)
        {
            if (buckets[i] == 0)
                continue;
            Single p = buckets[i] * invTotal;
            entropy -= p * Log2Approx.Lookup(p);
        }
        return entropy;
    }

    private static Single CalculateDataDirectoryEntropy(ImageDataDirectory[]? directories)
    {
        if (directories == null || directories.Length == 0)
            return 0.0f;
        Single sum = 0.0f;
        for (Int32 i = 0; i < directories.Length; i++)
            sum += directories[i].Size;
        if (sum <= 0)
            return 0.0f;
        Single entropy = 0.0f;
        for (Int32 i = 0; i < directories.Length; i++)
        {
            Single p = directories[i].Size / sum;
            if (p > 0.0f)
                entropy -= p * Log2Approx.Lookup(p);
        }
        return entropy;
    }

    private static Boolean IsSuspiciousSectionName(String name)
    {
        return name.Equals("UPX0", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("UPX1", StringComparison.OrdinalIgnoreCase) ||
               name.Equals(".vmp0", StringComparison.OrdinalIgnoreCase) ||
               name.Equals(".vmp1", StringComparison.OrdinalIgnoreCase) ||
               name.Equals(".petite", StringComparison.OrdinalIgnoreCase) ||
               name.Equals(".aspack", StringComparison.OrdinalIgnoreCase);
    }

    private static Boolean IsCodeSectionName(String name)
    {
        return name.Equals(".text", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("CODE", StringComparison.OrdinalIgnoreCase) ||
               name.Equals(".code", StringComparison.OrdinalIgnoreCase);
    }

    private static Boolean IsDataSectionName(String name)
    {
        return name.Equals(".data", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("DATA", StringComparison.OrdinalIgnoreCase) ||
               name.Equals(".rdata", StringComparison.OrdinalIgnoreCase);
    }

    private static void ProcessImports(Single* output, ImageOptionalHeader? optionalHeader,
        ImageSectionHeader[]? sections, Byte* rawPtr, Int32 rawLength)
    {
        for (Int32 i = 84; i < 100; i++)
            output[i] = 0.0f;

        ImageDataDirectory[]? directories = optionalHeader?.DataDirectory;
        if (directories == null || directories.Length <= 1 || sections == null || rawPtr == null || sections.Length == 0)
            return;

        UInt32 importRva = directories[1].VirtualAddress;
        UInt32 importSize = directories[1].Size;
        if (importRva == 0 || importSize < 20)
            return;

        Int32 importOffset = RvaToFileOffset(sections, importRva, rawLength);
        if (importOffset < 0 || importOffset + 20 > rawLength)
            return;

        output[99] = (importRva < 0x1000 || importRva > 0x20000000) ? 1.0f : 0.0f;

        Boolean is64Bit = (UInt16?)optionalHeader?.Magic == Pe32PlusMagic;
        Int32 thunkSize = is64Bit ? 8 : 4;
        UInt64 ordinalMask = is64Bit ? 0x8000000000000000UL : 0x80000000U;

        Int32 dllCount = 0;
        Int32 totalApis = 0;
        Int32 maxApisPerDll = 0;
        Int32 kernel32Apis = 0;
        Int32 ntdllApis = 0;
        Int32 advapi32Apis = 0;
        Boolean ws2_32Present = false;
        Boolean wininetPresent = false;
        Boolean crypt32Present = false;
        Int32 suspiciousApiCount = 0;
        Boolean loadLibraryPresent = false;
        Boolean getProcAddressPresent = false;
        Int32 ordinalImports = 0;
        Int32 namedImports = 0;
        Int32* dllNameFreq = stackalloc Int32[256];
        Int32 dllNameBytes = 0;

        Byte* importPtr = rawPtr + importOffset;
        Int32 maxDescriptors = (Int32)Math.Min(importSize / 20, (rawLength - importOffset) / 20);

        for (Int32 d = 0; d < maxDescriptors; d++)
        {
            Byte* desc = importPtr + d * 20;
            UInt32 originalFirstThunk = *(UInt32*)(desc + 0);
            UInt32 nameRva = *(UInt32*)(desc + 12);
            UInt32 firstThunk = *(UInt32*)(desc + 16);

            if (originalFirstThunk == 0 && nameRva == 0 && firstThunk == 0)
                break;

            if (nameRva == 0)
                continue;

            Int32 nameOffset = RvaToFileOffset(sections, nameRva, rawLength);
            if (nameOffset < 0 || nameOffset >= rawLength)
                continue;

            Byte* namePtr = rawPtr + nameOffset;
            Int32 nameLen = 0;
            while (nameLen < 64 && nameOffset + nameLen < rawLength && namePtr[nameLen] != 0)
                nameLen++;

            dllCount++;
            dllNameBytes += nameLen;
            for (Int32 i = 0; i < nameLen; i++)
            {
                Byte c = namePtr[i];
                if (c >= 'A' && c <= 'Z')
                    c += 32;
                dllNameFreq[c]++;
            }

            UInt64 dllHash = HashDllNameLower(namePtr, nameLen);
            Boolean isKernel32 = dllHash == HashKernel32Dll;
            Boolean isNtdll = dllHash == HashNtdllDll;
            Boolean isAdvapi = dllHash == HashAdvapi32Dll;
            if (dllHash == HashWs232Dll)
                ws2_32Present = true;
            if (dllHash == HashWininetDll)
                wininetPresent = true;
            if (dllHash == HashCrypt32Dll)
                crypt32Present = true;

            UInt32 thunkRva = originalFirstThunk != 0 ? originalFirstThunk : firstThunk;
            if (thunkRva == 0)
                continue;

            Int32 thunkOffset = RvaToFileOffset(sections, thunkRva, rawLength);
            if (thunkOffset < 0 || thunkOffset >= rawLength)
                continue;

            Int32 apiCount = 0;
            Byte* thunkPtr = rawPtr + thunkOffset;
            Int32 remaining = rawLength - thunkOffset;

            for (Int32 t = 0; t < remaining / thunkSize && t < MaxApisPerDll; t++)
            {
                UInt64 thunkValue = is64Bit ? *(UInt64*)(thunkPtr + t * thunkSize) : *(UInt32*)(thunkPtr + t * thunkSize);
                if (thunkValue == 0)
                    break;

                apiCount++;
                totalApis++;

                if ((thunkValue & ordinalMask) != 0)
                {
                    ordinalImports++;
                    continue;
                }

                namedImports++;

                if (isKernel32)
                    kernel32Apis++;
                else if (isNtdll)
                    ntdllApis++;
                else if (isAdvapi)
                    advapi32Apis++;

                if (apiCount > MaxApiNamesToScan)
                    continue;

                UInt32 nameEntryRva = (UInt32)(thunkValue & 0x7FFFFFFF);
                Int32 nameEntryOffset = RvaToFileOffset(sections, nameEntryRva, rawLength);
                if (nameEntryOffset < 0 || nameEntryOffset + 2 >= rawLength)
                    continue;

                Byte* apiNamePtr = rawPtr + nameEntryOffset + 2;
                Int32 apiNameLen = 0;
                while (apiNameLen < 256 && nameEntryOffset + 2 + apiNameLen < rawLength && apiNamePtr[apiNameLen] != 0)
                    apiNameLen++;

                if (apiNameLen == 0)
                    continue;

                if (IsSuspiciousApi(apiNamePtr, apiNameLen))
                    suspiciousApiCount++;
                if (IsLoadLibraryApi(apiNamePtr, apiNameLen))
                    loadLibraryPresent = true;
                if (IsGetProcAddressApi(apiNamePtr, apiNameLen))
                    getProcAddressPresent = true;
            }

            if (apiCount > maxApisPerDll)
                maxApisPerDll = apiCount;
        }

        output[84] = (Single)dllCount;
        output[85] = (Single)totalApis;
        output[86] = dllCount > 0 ? totalApis / (Single)dllCount : 0.0f;
        output[87] = (Single)maxApisPerDll;
        output[88] = totalApis > 0 ? kernel32Apis / (Single)totalApis : 0.0f;
        output[89] = totalApis > 0 ? ntdllApis / (Single)totalApis : 0.0f;
        output[90] = totalApis > 0 ? advapi32Apis / (Single)totalApis : 0.0f;
        output[91] = ws2_32Present ? 1.0f : 0.0f;
        output[92] = wininetPresent ? 1.0f : 0.0f;
        output[93] = crypt32Present ? 1.0f : 0.0f;
        output[94] = (Single)suspiciousApiCount;
        output[95] = loadLibraryPresent ? 1.0f : 0.0f;
        output[96] = getProcAddressPresent ? 1.0f : 0.0f;
        output[97] = dllNameBytes > 0 ? EntropyFromFrequency(dllNameFreq, dllNameBytes) : 0.0f;
        output[98] = totalApis > 0 ? ordinalImports / (Single)totalApis : 0.0f;
    }

    private static void ProcessStrings(Single* output, Byte* rawPtr, Int32 rawLength, Int64 overlayStart)
    {
        for (Int32 i = 100; i < 112; i++)
            output[i] = 0.0f;

        if (rawPtr == null || rawLength <= 0)
            return;

        Int32 scanLength = Math.Min(rawLength, MaxStringScanBytes);
        Byte* scanPtr = rawPtr;
        Byte* scanEnd = scanPtr + scanLength;

        Int32 stringCount = 0;
        Int32 totalLength = 0;
        Int32 urlCount = 0;
        Int32 ipCount = 0;
        Int32 pathCount = 0;
        Int32 registryCount = 0;
        Int32 cmdCount = 0;
        Int32 httpCount = 0;
        Int32 suspiciousCount = 0;
        Int32* charFreq = stackalloc Int32[256];
        Int32 totalStringBytes = 0;

        Byte* start = null;
        Byte* p = scanPtr;
        while (p < scanEnd)
        {
            Byte c = *p;
            if (c >= 32 && c <= 126)
            {
                if (start == null)
                    start = p;
            }
            else if (start != null)
            {
                Int32 len = (Int32)(p - start);
                if (len >= 5)
                {
                    stringCount++;
                    totalLength += len;

                    if (totalStringBytes < MaxStringEntropyBytes)
                    {
                        Int32 copyLen = Math.Min(len, MaxStringEntropyBytes - totalStringBytes);
                        Byte* s = start;
                        Byte* end = start + copyLen;
                        while (s < end)
                            charFreq[*s++]++;
                        totalStringBytes += copyLen;
                    }

                    if (stringCount <= MaxPatternStrings)
                    {
                        StringPatterns patterns = ScanStringPatterns(start, len);
                        if ((patterns & StringPatterns.Url) != 0) urlCount++;
                        if ((patterns & StringPatterns.Http) != 0) httpCount++;
                        if ((patterns & StringPatterns.Path) != 0) pathCount++;
                        if ((patterns & StringPatterns.Registry) != 0) registryCount++;
                        if ((patterns & StringPatterns.Cmd) != 0) cmdCount++;
                        if ((patterns & StringPatterns.Ip) != 0) ipCount++;
                        if ((patterns & StringPatterns.Suspicious) != 0) suspiciousCount++;
                    }
                }
                start = null;
            }
            p++;
        }

        if (start != null)
        {
            Int32 len = (Int32)(scanEnd - start);
            if (len >= 5)
            {
                stringCount++;
                totalLength += len;

                if (totalStringBytes < MaxStringEntropyBytes)
                {
                    Int32 copyLen = Math.Min(len, MaxStringEntropyBytes - totalStringBytes);
                    Byte* s = start;
                    Byte* end = start + copyLen;
                    while (s < end)
                        charFreq[*s++]++;
                    totalStringBytes += copyLen;
                }

                if (stringCount <= MaxPatternStrings)
                {
                    StringPatterns patterns = ScanStringPatterns(start, len);
                    if ((patterns & StringPatterns.Url) != 0) urlCount++;
                    if ((patterns & StringPatterns.Http) != 0) httpCount++;
                    if ((patterns & StringPatterns.Path) != 0) pathCount++;
                    if ((patterns & StringPatterns.Registry) != 0) registryCount++;
                    if ((patterns & StringPatterns.Cmd) != 0) cmdCount++;
                    if ((patterns & StringPatterns.Ip) != 0) ipCount++;
                    if ((patterns & StringPatterns.Suspicious) != 0) suspiciousCount++;
                }
            }
        }

        output[100] = (Single)stringCount;
        output[101] = (Single)totalLength;
        output[102] = stringCount > 0 ? totalLength / (Single)stringCount : 0.0f;
        output[103] = totalStringBytes > 0 ? EntropyFromFrequency(charFreq, totalStringBytes) : 0.0f;
        output[104] = (Single)urlCount;
        output[105] = (Single)ipCount;
        output[106] = (Single)pathCount;
        output[107] = (Single)registryCount;
        output[108] = (Single)cmdCount;
        output[109] = (Single)httpCount;
        output[110] = (Single)suspiciousCount;

        if (overlayStart > 0 && overlayStart + 2 <= rawLength)
        {
            Byte* overlay = rawPtr + overlayStart;
            Int32 overlayLength = (Int32)Math.Min(rawLength - overlayStart, 65536);
            output[111] = ContainsSequence(overlay, overlayLength, "MZ") ? 1.0f : 0.0f;
        }
    }

    private static Int32 RvaToFileOffset(ImageSectionHeader[] sections, UInt32 rva, Int32 rawLength)
    {
        for (Int32 i = 0; i < sections.Length; i++)
        {
            ImageSectionHeader s = sections[i];
            UInt32 va = s.VirtualAddress;
            UInt32 size = Math.Max(s.VirtualSize, s.SizeOfRawData);
            if (rva >= va && rva < va + size)
            {
                UInt64 offset64 = (UInt64)rva - va + s.PointerToRawData;
                if (offset64 < (UInt32)rawLength)
                    return (Int32)offset64;
                return -1;
            }
        }
        if (rva < 0x1000)
            return (Int32)rva;
        return -1;
    }

    private static UInt64 HashApiName(Byte* name, Int32 length)
    {
        UInt64 hash = 0xcbf29ce484222325UL;
        for (Int32 i = 0; i < length; i++)
        {
            hash ^= name[i];
            hash *= 0x100000001b3UL;
        }
        return hash;
    }

    private static UInt64 HashDllNameLower(Byte* name, Int32 length)
    {
        UInt64 hash = 0xcbf29ce484222325UL;
        for (Int32 i = 0; i < length; i++)
        {
            Byte c = name[i];
            if (c >= 'A' && c <= 'Z')
                c += 32;
            hash ^= c;
            hash *= 0x100000001b3UL;
        }
        return hash;
    }

    private static Boolean IsSuspiciousApi(Byte* name, Int32 length)
    {
        if (length < 7 || length > 20)
            return false;
        UInt64 hash = HashApiName(name, length);
        return hash == HashVirtualAlloc ||
               hash == HashVirtualProtect ||
               hash == HashWriteProcessMemory ||
               hash == HashReadProcessMemory ||
               hash == HashCreateRemoteThread ||
               hash == HashNtUnmapViewOfSection ||
               hash == HashSetWindowsHookEx ||
               hash == HashWinExec ||
               hash == HashShellExecuteA ||
               hash == HashShellExecuteW ||
               hash == HashCreateProcessA ||
               hash == HashCreateProcessW ||
               hash == HashInternetOpenA ||
               hash == HashInternetOpenW ||
               hash == HashURLDownloadToFileA ||
               hash == HashURLDownloadToFileW ||
               hash == HashCryptEncrypt ||
               hash == HashRegSetValueExA ||
               hash == HashRegSetValueExW;
    }

    private static Boolean IsLoadLibraryApi(Byte* name, Int32 length)
    {
        if (length < 11 || length > 14)
            return false;
        UInt64 hash = HashApiName(name, length);
        return hash == HashLoadLibrary ||
               hash == HashLoadLibraryA ||
               hash == HashLoadLibraryW ||
               hash == HashLoadLibraryExA ||
               hash == HashLoadLibraryExW;
    }

    private static Boolean IsGetProcAddressApi(Byte* name, Int32 length)
    {
        return length == 14 && HashApiName(name, length) == HashGetProcAddress;
    }

    private static Boolean ContainsSequence(Byte* data, Int32 length, String sequence)
    {
        if (length < sequence.Length)
            return false;
        Int32 seqLen = sequence.Length;
        for (Int32 i = 0; i <= length - seqLen; i++)
        {
            Int32 j = 0;
            while (j < seqLen && data[i + j] == (Byte)sequence[j])
                j++;
            if (j == seqLen)
                return true;
        }
        return false;
    }

    private static Boolean StartsWithIgnoreCase(Byte* data, Int32 length, String prefix)
    {
        if (length < prefix.Length)
            return false;
        for (Int32 i = 0; i < prefix.Length; i++)
        {
            Byte c = data[i];
            if (c >= 'A' && c <= 'Z')
                c += 32;
            if (c != (Byte)prefix[i])
                return false;
        }
        return true;
    }

    [Flags]
    private enum StringPatterns : Byte
    {
        None = 0,
        Url = 1,
        Http = 2,
        Path = 4,
        Registry = 8,
        Cmd = 16,
        Ip = 32,
        Suspicious = 64
    }

    private static StringPatterns ScanStringPatterns(Byte* str, Int32 len)
    {
        StringPatterns result = StringPatterns.None;
        Int32 digits = 0;
        Int32 dots = 0;

        for (Int32 i = 0; i < len; i++)
        {
            Byte c = str[i];

            if (i <= len - 3 && c == ':' && str[i + 1] == '/' && str[i + 2] == '/')
                result |= StringPatterns.Url;

            if (i <= len - 7 && c == 'h' && str[i + 1] == 't' && str[i + 2] == 't' && str[i + 3] == 'p')
            {
                if (str[i + 4] == ':' && str[i + 5] == '/' && str[i + 6] == '/')
                    result |= StringPatterns.Http;
                else if (i <= len - 8 && str[i + 4] == 's' && str[i + 5] == ':' && str[i + 6] == '/' && str[i + 7] == '/')
                    result |= StringPatterns.Http;
            }

            if (i < len - 1)
            {
                Byte c1 = str[i + 1];
                if ((c == '\\' && c1 == '\\') || (c == ':' && c1 == '\\') || (c == '/' && i > 0))
                    result |= StringPatterns.Path;
            }

            if (c >= '0' && c <= '9')
            {
                digits++;
            }
            else if (c == '.')
            {
                if (digits > 0 && digits <= 3)
                    dots++;
                else
                    dots = 0;
                digits = 0;
            }
            else
            {
                if (dots >= 3 && digits > 0 && digits <= 3)
                    result |= StringPatterns.Ip;
                digits = 0;
                dots = 0;
            }
        }

        if (dots >= 3 && digits > 0 && digits <= 3)
            result |= StringPatterns.Ip;

        if (ContainsSequence(str, len, ".exe") || ContainsSequence(str, len, ".dll") || ContainsSequence(str, len, ".sys"))
            result |= StringPatterns.Path;
        if (StartsWithIgnoreCase(str, len, "HKEY_") || ContainsSequence(str, len, "SOFTWARE\\") || ContainsSequence(str, len, "Registry"))
            result |= StringPatterns.Registry;
        if (StartsWithIgnoreCase(str, len, "cmd.exe") || StartsWithIgnoreCase(str, len, "cmd /") || StartsWithIgnoreCase(str, len, "powershell") || ContainsSequence(str, len, ".bat") || ContainsSequence(str, len, ".cmd"))
            result |= StringPatterns.Cmd;
        if (StartsWithIgnoreCase(str, len, "powershell") || StartsWithIgnoreCase(str, len, "base64") || StartsWithIgnoreCase(str, len, "-enc") || ContainsSequence(str, len, "FromBase64String") || ContainsSequence(str, len, "Invoke-Expression") || ContainsSequence(str, len, "WScript") || ContainsSequence(str, len, "cmd.exe"))
            result |= StringPatterns.Suspicious;

        return result;
    }

    private static Boolean HasPathPattern(Byte* data, Int32 length)
    {
        for (Int32 i = 0; i < length - 1; i++)
        {
            Byte c0 = data[i];
            Byte c1 = data[i + 1];
            if ((c0 == '\\' && c1 == '\\') ||
                (c0 == ':' && c1 == '\\') ||
                (c0 == '/' && i > 0))
                return true;
        }
        return ContainsSequence(data, length, ".exe") || ContainsSequence(data, length, ".dll");
    }

    private static Boolean HasRegistryPattern(Byte* data, Int32 length)
    {
        return StartsWithIgnoreCase(data, length, "HKEY_") ||
               ContainsSequence(data, length, "SOFTWARE\\") ||
               ContainsSequence(data, length, "Registry");
    }

    private static Boolean HasCmdPattern(Byte* data, Int32 length)
    {
        return StartsWithIgnoreCase(data, length, "cmd.exe") ||
               StartsWithIgnoreCase(data, length, "cmd /") ||
               StartsWithIgnoreCase(data, length, "powershell") ||
               ContainsSequence(data, length, ".bat") ||
               ContainsSequence(data, length, ".cmd");
    }

    private static Boolean HasIpPattern(Byte* data, Int32 length)
    {
        Int32 digits = 0;
        Int32 dots = 0;
        for (Int32 i = 0; i < length; i++)
        {
            Byte c = data[i];
            if (c >= '0' && c <= '9')
            {
                digits++;
            }
            else if (c == '.')
            {
                if (digits > 0 && digits <= 3)
                    dots++;
                else
                    dots = 0;
                digits = 0;
            }
            else
            {
                if (dots >= 3 && digits > 0 && digits <= 3)
                    return true;
                digits = 0;
                dots = 0;
            }
        }
        return dots >= 3 && digits > 0 && digits <= 3;
    }

    private static Boolean HasSuspiciousStringPattern(Byte* data, Int32 length)
    {
        return StartsWithIgnoreCase(data, length, "powershell") ||
               StartsWithIgnoreCase(data, length, "base64") ||
               StartsWithIgnoreCase(data, length, "-enc") ||
               ContainsSequence(data, length, "FromBase64String") ||
               ContainsSequence(data, length, "Invoke-Expression") ||
               ContainsSequence(data, length, "WScript") ||
               ContainsSequence(data, length, "cmd.exe");
    }
}
