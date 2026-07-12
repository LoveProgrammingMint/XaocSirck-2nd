using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using PeNet;
using PeNet.Header.Pe;
using PeNet.Header.Resource;
using XaocSirck_Core.Interface.Feature.Zeroflows;
namespace XaocSirck_Core.Feature.Engineering.Zeroflows;
internal sealed unsafe class PeExtended : IPeEngineering, IDisposable
{
    private const Int32 ResultSize = 39;

    private const UInt16 MachineI386 = 0x014c;
    private const UInt16 MachineAmd64 = 0x8664;
    private const UInt16 MachineArm = 0x01c0;
    private const UInt16 MachineArm64 = 0xaa64;
    private const UInt16 CharacteristicsRelocsStripped = 0x0001;

    private const UInt32 ImageDirectoryEntryResource = 2;
    private const UInt32 ImageDirectoryEntryException = 3;
    private const UInt32 ImageDirectoryEntryTls = 9;
    private const UInt32 ImageDirectoryEntryLoadConfig = 10;
    private const UInt32 ImageDirectoryEntryBoundImport = 11;
    private const UInt32 ImageDirectoryEntryDelayImport = 13;

    private const UInt32 DebugTypeCodeView = 2;

    private readonly ShareFeatures _share;
    private readonly Int32 _offset;
    private Boolean _disposed;

    public PeExtended(ShareFeatures share, Int32 offset = FeatureLayout.PeExtendedOffset)
    {
        ArgumentNullException.ThrowIfNull(share);
        _share = share;
        _offset = offset;
    }

    public void Process(PeFile pe, Int64 fileSize)
    {
        Single* output = _share.FeatureTensor + _offset;
        NativeMemory.Clear(output, (UIntPtr)(ResultSize * sizeof(Single)));

        if (pe?.ImageNtHeaders?.FileHeader == null)
            return;

        ImageFileHeader fileHeader = pe.ImageNtHeaders.FileHeader;
        ImageOptionalHeader? optionalHeader = pe.ImageNtHeaders.OptionalHeader;
        ImageDataDirectory[]? directories = optionalHeader?.DataDirectory;

        UInt16 machine = (UInt16)fileHeader.Machine;
        output[0] = (Single)machine;
        output[1] = machine == MachineI386 || machine == MachineAmd64 || machine == MachineArm || machine == MachineArm64 ? 1.0f : 0.0f;
        output[2] = Log2Normalized((UInt64)fileHeader.NumberOfSymbols + 1);
        output[3] = Log2Normalized((UInt64)fileHeader.PointerToSymbolTable + 1);
        output[4] = ((UInt16)fileHeader.Characteristics & CharacteristicsRelocsStripped) != 0 ? 1.0f : 0.0f;

        ProcessExportDirectory(output, pe);
        ProcessDebugDirectory(output, pe);
        ProcessCertificate(output, pe);
        ProcessResourceDirectory(output, pe);
        ProcessTlsAndLoadConfig(output, pe);
        ProcessExceptionDirectory(output, directories);
        ProcessDelayAndBoundImports(output, pe);
        ProcessFileStructure(output, pe, fileSize);
        ProcessDataDirectories(output, directories);
    }

    private static void ProcessExportDirectory(Single* output, PeFile pe)
    {
        ImageExportDirectory? export = pe.ImageExportDirectory;
        output[5] = export != null ? 1.0f : 0.0f;
        if (export == null)
            return;

        output[6] = Log2Normalized(export.NumberOfFunctions + 1);
        output[7] = Log2Normalized(export.NumberOfNames + 1);
        output[8] = export.Name != 0 ? 1.0f : 0.0f;
        output[9] = Log2Normalized(export.Base + 1);
    }

    private static void ProcessDebugDirectory(Single* output, PeFile pe)
    {
        ImageDebugDirectory[]? debug = pe.ImageDebugDirectory;
        Int32 count = debug?.Length ?? 0;
        output[10] = count > 0 ? 1.0f : 0.0f;
        output[11] = (Single)count;

        Boolean hasCodeview = false;
        Boolean hasPdbPath = false;
        Int32 pdbPathLength = 0;
        Int32 debugDataSize = 0;

        if (debug != null)
        {
            for (Int32 i = 0; i < debug.Length; i++)
            {
                ImageDebugDirectory d = debug[i];
                debugDataSize += (Int32)d.SizeOfData;
                if (d.Type == DebugTypeCodeView)
                {
                    hasCodeview = true;
                    CvInfoPdb70? cv = d.CvInfoPdb70;
                    if (cv != null)
                    {
                        String? pdb = cv.PdbFileName;
                        if (!String.IsNullOrEmpty(pdb))
                        {
                            hasPdbPath = true;
                            pdbPathLength = pdb!.Length;
                        }
                    }
                }
            }
        }

        output[12] = hasCodeview ? 1.0f : 0.0f;
        output[13] = hasPdbPath ? 1.0f : 0.0f;
        output[14] = Log2Normalized((UInt32)pdbPathLength + 1);
    }

    private static void ProcessCertificate(Single* output, PeFile pe)
    {
        WinCertificate? cert = pe.WinCertificate;
        output[15] = cert != null ? 1.0f : 0.0f;
        output[16] = cert != null ? Log2Normalized(cert.DwLength + 1) : 0.0f;

        X509Certificate2? signing = pe.SigningAuthenticodeCertificate;
        output[17] = signing != null ? 1.0f : 0.0f;
        output[18] = signing != null ? Log2Normalized((UInt32)(signing.Subject?.Length ?? 0) + 1) : 0.0f;
        output[19] = signing != null ? Log2Normalized((UInt32)(signing.Issuer?.Length ?? 0) + 1) : 0.0f;
    }

    private static void ProcessResourceDirectory(Single* output, PeFile pe)
    {
        ImageResourceDirectory? resource = pe.ImageResourceDirectory;
        output[20] = resource != null ? 1.0f : 0.0f;
        if (resource?.DirectoryEntries == null)
            return;

        List<ImageResourceDirectoryEntry?> entries = resource.DirectoryEntries;
        Int32 typeCount = entries.Count;
        Boolean hasIcon = false;
        Boolean hasVersion = false;

        for (Int32 i = 0; i < typeCount; i++)
        {
            ImageResourceDirectoryEntry? e = entries[i];
            if (e == null)
                continue;

            UInt32 id = e.ID;
            if (!hasIcon && (id == 3 || id == 14))
            {
                hasIcon = true;
            }
            else if (!hasVersion && id == 16)
            {
                hasVersion = true;
            }
            else if ((!hasIcon || !hasVersion) && id == 0)
            {
                String? name = e.NameResolved;
                if (!hasIcon && (name == "Icon" || name == "GroupIcon"))
                    hasIcon = true;
                if (!hasVersion && name == "Version")
                    hasVersion = true;
            }

            if (hasIcon && hasVersion)
                break;
        }

        output[21] = (Single)typeCount;
        output[22] = (Single)typeCount;
        output[23] = hasIcon ? 1.0f : 0.0f;
        output[24] = hasVersion ? 1.0f : 0.0f;
    }

    private static void ProcessTlsAndLoadConfig(Single* output, PeFile pe)
    {
        ImageTlsDirectory? tls = pe.ImageTlsDirectory;
        output[25] = tls != null ? 1.0f : 0.0f;
        output[26] = tls?.TlsCallbacks != null ? (Single)tls.TlsCallbacks.Length : 0.0f;

        ImageLoadConfigDirectory? loadConfig = pe.ImageLoadConfigDirectory;
        output[27] = loadConfig != null ? 1.0f : 0.0f;
        output[28] = loadConfig != null ? Log2Normalized(loadConfig.Size + 1) : 0.0f;
        output[29] = loadConfig != null && loadConfig.GuardCFCheckFunctionPointer != 0 ? 1.0f : 0.0f;
    }

    private static void ProcessExceptionDirectory(Single* output, ImageDataDirectory[]? directories)
    {
        Boolean present = directories != null && directories.Length > ImageDirectoryEntryException
            && (directories[ImageDirectoryEntryException].VirtualAddress != 0 || directories[ImageDirectoryEntryException].Size != 0);
        output[30] = present ? 1.0f : 0.0f;
        output[31] = present ? Log2Normalized((UInt32)directories![ImageDirectoryEntryException].Size + 1) : 0.0f;
    }

    private static void ProcessDelayAndBoundImports(Single* output, PeFile pe)
    {
        ImageDelayImportDescriptor[]? delay = pe.ImageDelayImportDescriptors;
        output[32] = delay != null && delay.Length > 0 ? 1.0f : 0.0f;
        output[33] = delay != null ? (Single)delay.Length : 0.0f;

        ImageBoundImportDescriptor? bound = pe.ImageBoundImportDescriptor;
        output[34] = bound != null ? 1.0f : 0.0f;
        output[35] = bound != null ? (Single)bound.NumberOfModuleForwarderRefs : 0.0f;
    }

    private static void ProcessFileStructure(Single* output, PeFile pe, Int64 fileSize)
    {
        output[36] = Log2Normalized((UInt64)fileSize + 1);

        ReadOnlySpan<Byte> rawSpan = pe.RawFile != null ? pe.RawFile.AsSpan(0, pe.RawFile.Length) : ReadOnlySpan<Byte>.Empty;
        UInt32 e_lfanew = pe.ImageDosHeader?.E_lfanew ?? 0;
        (Boolean present, Int32 entryCount) = ParseRichHeader(rawSpan, e_lfanew);
        output[37] = present ? 1.0f : 0.0f;
        output[38] = (Single)entryCount;
    }

    private static void ProcessDataDirectories(Single* output, ImageDataDirectory[]? directories)
    {
        if (directories == null)
            return;

        if (directories.Length > ImageDirectoryEntryResource && (directories[ImageDirectoryEntryResource].VirtualAddress != 0 || directories[ImageDirectoryEntryResource].Size != 0))
            output[20] = 1.0f;
        if (directories.Length > ImageDirectoryEntryTls && (directories[ImageDirectoryEntryTls].VirtualAddress != 0 || directories[ImageDirectoryEntryTls].Size != 0))
            output[25] = 1.0f;
        if (directories.Length > ImageDirectoryEntryLoadConfig && (directories[ImageDirectoryEntryLoadConfig].VirtualAddress != 0 || directories[ImageDirectoryEntryLoadConfig].Size != 0))
            output[27] = 1.0f;
        if (directories.Length > ImageDirectoryEntryDelayImport && (directories[ImageDirectoryEntryDelayImport].VirtualAddress != 0 || directories[ImageDirectoryEntryDelayImport].Size != 0))
            output[32] = 1.0f;
        if (directories.Length > ImageDirectoryEntryBoundImport && (directories[ImageDirectoryEntryBoundImport].VirtualAddress != 0 || directories[ImageDirectoryEntryBoundImport].Size != 0))
            output[34] = 1.0f;
    }

    private static (Boolean present, Int32 entryCount) ParseRichHeader(ReadOnlySpan<Byte> rawSpan, UInt32 e_lfanew)
    {
        Int32 rawLength = rawSpan.Length;
        if (rawLength < 0x80 || e_lfanew < 0x80)
            return (false, 0);

        Int32 scanEnd = Math.Min((Int32)e_lfanew, rawLength) - 8;
        fixed (Byte* rawPtr = rawSpan)
        {
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
                            Int32 entryEnd = i;
                            Int32 entryStart = 0x44;
                            Int32 entryCount = 0;
                            for (Int32 j = entryStart; j < entryEnd; j += 8)
                            {
                                UInt32 v = *(UInt32*)(rawPtr + j) ^ key;
                                if (v != 0)
                                    entryCount++;
                            }
                            return (true, entryCount);
                        }
                    }
                }
            }
        }
        return (false, 0);
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
}
