using OpenMcdf;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;

namespace XaocSirck_Core.Engine;

internal class DocVBA
{
    private static readonly Byte[] _ole2Magic = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 ];
    private static readonly Byte[] _zipMagic = [0x50, 0x4B, 0x03, 0x04 ];

    public static Boolean HasMacro(String filePath)
    {
        Byte[] header = new Byte[8];
        using (FileStream fs = File.OpenRead(filePath))
        {
            if (fs.Length < 8) return false;
            fs.ReadExactly(header, 0, 8);
        }

        if (header.Take(8).SequenceEqual(_ole2Magic))
            return HasMacroOLE2(filePath);

        if (header.Take(4).SequenceEqual(_zipMagic))
            return IsOOXML(filePath) && HasMacroOOXML(filePath);

        return false;
    }

    private static Boolean HasMacroOLE2(String filePath)
    {
        using RootStorage root = RootStorage.OpenRead(filePath);
        return ScanStorage(root);
    }

    private static Boolean ScanStorage(Storage storage)
    {
        foreach (EntryInfo entry in storage.EnumerateEntries())
        {
            String name = entry.Name.ToUpperInvariant();
            if (name.Contains("VBA") || name.Contains("_VBA_PROJECT") || name == "MACROS")
                return true;

            if (entry.Type == EntryType.Storage && ScanStorage(storage.OpenStorage(entry.Name)))
                return true;
        }
        return false;
    }

    private static Boolean IsOOXML(String filePath)
    {
        using ZipArchive zip = ZipFile.OpenRead(filePath);
        return zip.GetEntry("[Content_Types].xml") != null;
    }

    private static Boolean HasMacroOOXML(String filePath)
    {
        using ZipArchive zip = ZipFile.OpenRead(filePath);
        String[] macroPaths = ["word/vbaProject.bin", "xl/vbaProject.bin", "ppt/vbaProject.bin"];
        if (macroPaths.Any(p => zip.GetEntry(p) != null))
            return true;

        return zip.Entries.Any(e => e.FullName.StartsWith("xl/macrosheets/", StringComparison.OrdinalIgnoreCase));
    }
}

