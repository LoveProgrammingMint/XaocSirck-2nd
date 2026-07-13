using System;
using System.Collections.Generic;
using System.Text;

using PeNet;
using PeNet.Header.Pe;

using XaocSirck_Core.Interface.Engine;

namespace XaocSirck_Core.Engine;

internal sealed unsafe class Shell
{
    private PeFile? _pe;

    public void Set(PeFile pe)
    {
        _pe = pe;
    }

    public ShellHits CheckVMProtect()
    {
        if (_pe?.ImageSectionHeaders == null)
            return ShellHits.Emtpy;

        Int32 score = 0;

        foreach (ImageSectionHeader section in _pe.ImageSectionHeaders)
        {
            if (section.Name.Contains("vmp", StringComparison.OrdinalIgnoreCase))
            {
                score += 50;
            }
        }

        if (_pe.ImageNtHeaders?.OptionalHeader != null)
        {
            UInt32 ep = _pe.ImageNtHeaders.OptionalHeader.AddressOfEntryPoint;
            ImageSectionHeader? epSec = _pe.ImageSectionHeaders?.FirstOrDefault(s =>
                ep >= s.VirtualAddress && ep < s.VirtualAddress + s.VirtualSize);

            if (epSec != null)
            {
                if (epSec.Name.StartsWith("vmp", StringComparison.OrdinalIgnoreCase) ||
                    epSec.Name.StartsWith(".vmp", StringComparison.OrdinalIgnoreCase))
                {
                    score += 25;
                }
            }
        }

        if (_pe.ImportedFunctions == null || !(_pe.ImportedFunctions.Length == 0))
        {
            score += 30;
        }
        else
        {
            Int32 dllCount = _pe.ImportedFunctions.Select(f => f.DLL).Distinct().Count();
            if (dllCount <= 1)
                score += 25;
            else if (dllCount <= 2 && _pe.ImportedFunctions.Length <= 3)
                score += 20;
        }

        if (_pe.ImageTlsDirectory != null)
            score += 10;

        return score >= 55 ? ShellHits.VmProtect : ShellHits.Emtpy;
    }

    public ShellHits CheckThemida()
    {
        if (_pe?.ImageSectionHeaders == null)
            return ShellHits.Emtpy;

        Int32 score = 0;

        if (_pe.ImageSectionHeaders.Length > 8)
            score += 15;

        foreach (ImageSectionHeader section in _pe.ImageSectionHeaders)
        {

            if (section.Name.Contains("themida", StringComparison.OrdinalIgnoreCase))
                score += 50;

            if (String.IsNullOrWhiteSpace(section.Name) || section.Name.All(c => c == '\0'))
                score += 30;

            if ((section.Characteristics & (ScnCharacteristicsType.MemExecute | ScnCharacteristicsType.MemWrite)) ==
                (ScnCharacteristicsType.MemExecute | ScnCharacteristicsType.MemWrite))
            {
                score += 10;
            }
        }

        if (_pe.ImageNtHeaders?.OptionalHeader != null)
        {
            UInt32 ep = _pe.ImageNtHeaders.OptionalHeader.AddressOfEntryPoint;
            ImageSectionHeader? epSec = _pe.ImageSectionHeaders?.FirstOrDefault(s =>
                ep >= s.VirtualAddress && ep < s.VirtualAddress + s.VirtualSize);

            if (epSec != null)
            {
                if (!epSec.Name.Equals(".text", StringComparison.OrdinalIgnoreCase) &&
                    !epSec.Name.Equals("CODE", StringComparison.OrdinalIgnoreCase) &&
                    !epSec.Name.Equals("text", StringComparison.OrdinalIgnoreCase))
                {
                    score += 20;
                }
            }
        }

        if (_pe.ImportedFunctions == null || !(_pe.ImportedFunctions.Length == 0))
        {
            score += 35;
        }
        else
        {
            Int32 dllCount = _pe.ImportedFunctions.Select(f => f.DLL).Distinct().Count();
            if (dllCount <= 1)
                score += 30;
        }

        if (_pe.ImageTlsDirectory != null)
            score += 15;

        return score >= 60 ? ShellHits.Themida : ShellHits.Emtpy;
    }

    public ShellHits CheckEnigma()
    {
        if (_pe?.ImageSectionHeaders == null)
            return ShellHits.Emtpy;

        Int32 score = 0;

        foreach (ImageSectionHeader section in _pe.ImageSectionHeaders)
        {

            if (section.Name.Contains(".enigma", StringComparison.OrdinalIgnoreCase))
                score += 90;

            if (section.Name.Contains(".sg", StringComparison.OrdinalIgnoreCase))
                score += 40;
        }

        if (_pe.ImageNtHeaders?.OptionalHeader != null)
        {
            UInt32 ep = _pe.ImageNtHeaders.OptionalHeader.AddressOfEntryPoint;
            ImageSectionHeader? epSec = _pe.ImageSectionHeaders?.FirstOrDefault(s =>
                ep >= s.VirtualAddress && ep < s.VirtualAddress + s.VirtualSize);

            if (epSec != null)
            {
                if (epSec.Name.Equals(".enigma", StringComparison.OrdinalIgnoreCase) ||
                    epSec.Name.StartsWith(".sg", StringComparison.OrdinalIgnoreCase))
                {
                    score += 25;
                }
            }
        }

        if (_pe.ImportedFunctions == null || !(_pe.ImportedFunctions.Length == 0))
        {
            score += 30;
        }
        else
        {
            Int32 apiCount = _pe.ImportedFunctions.Length;
            if (apiCount <= 3)
                score += 25;
        }

        if (_pe.ImageTlsDirectory != null)
            score += 10;

        return score >= 55 ? ShellHits.Enigma : ShellHits.Emtpy;
    }

    public ShellHits CheckUPX()
    {
        if (_pe?.ImageSectionHeaders == null)
            return ShellHits.Emtpy;

        Int32 score = 0;

        foreach (ImageSectionHeader section in _pe.ImageSectionHeaders)
        {

            if (section.Name.Equals("UPX0", StringComparison.OrdinalIgnoreCase))
            {
                score += 20;
                if (section.SizeOfRawData == 0 && section.VirtualSize > 0)
                    score += 15;
            }

            if (section.Name.Contains("UPX", StringComparison.OrdinalIgnoreCase))
                score += 20;
        }

        if (_pe.ImportedFunctions != null)
        {
            Int32 dllCount = _pe.ImportedFunctions.Select(f => f.DLL).Distinct().Count();
            if (dllCount <= 2)
                score += 10;
        }

        if (_pe.ImageTlsDirectory != null)
            score += 5;

        return score >= 30 ? ShellHits.UPX : ShellHits.Emtpy;
    }

    public ShellHits CheckASProtect()
    {
        if (_pe?.ImageSectionHeaders == null)
            return ShellHits.Emtpy;

        Int32 score = 0;

        foreach (ImageSectionHeader section in _pe.ImageSectionHeaders)
        {
            if (section.Name.Equals(".aspack", StringComparison.OrdinalIgnoreCase))
                score += 30;

            if (section.Name.Equals(".adata", StringComparison.OrdinalIgnoreCase))
                score += 25;

            if (section.SizeOfRawData == 0 && section.VirtualSize > 0)
                score += 10;
        }

        if (_pe.ImageNtHeaders?.OptionalHeader != null)
        {
            UInt32 ep = _pe.ImageNtHeaders.OptionalHeader.AddressOfEntryPoint;
            ImageSectionHeader? epSec = _pe.ImageSectionHeaders?.FirstOrDefault(s =>
                ep >= s.VirtualAddress && ep < s.VirtualAddress + s.VirtualSize);

            if (epSec != null)
            {
                if (!epSec.Name.Equals(".text", StringComparison.OrdinalIgnoreCase) &&
                    !epSec.Name.Equals("CODE", StringComparison.OrdinalIgnoreCase) &&
                    !epSec.Name.Equals("text", StringComparison.OrdinalIgnoreCase))
                {
                    score += 20;
                }
            }
        }

        if (_pe.ImportedFunctions != null)
        {
            Int32 apiCount = _pe.ImportedFunctions.Length;
            if (apiCount <= 5)
                score += 20;
            else if (apiCount <= 10)
                score += 10;
        }

        return score >= 35 ? ShellHits.ASProtect : ShellHits.Emtpy;
    }

    public ShellHits Check()
    {
        if (_pe == null)
            return ShellHits.Emtpy;

        ShellHits hit = CheckVMProtect();
        if (hit != ShellHits.Emtpy) return hit;

        hit = CheckThemida();
        if (hit != ShellHits.Emtpy) return hit;

        hit = CheckEnigma();
        if (hit != ShellHits.Emtpy) return hit;

        hit = CheckASProtect();
        if (hit != ShellHits.Emtpy) return hit;

        hit = CheckUPX();
        if (hit != ShellHits.Emtpy) return hit;

        Int32 suspiciousScore = 0;

        if (_pe.ImageNtHeaders?.OptionalHeader != null && _pe.ImageSectionHeaders != null)
        {
            UInt32 ep = _pe.ImageNtHeaders.OptionalHeader.AddressOfEntryPoint;
            ImageSectionHeader? epSec = _pe.ImageSectionHeaders.FirstOrDefault(s =>
                ep >= s.VirtualAddress && ep < s.VirtualAddress + s.VirtualSize);

            if (epSec != null)
            {
                if (!epSec.Name.Equals(".text", StringComparison.OrdinalIgnoreCase) &&
                    !epSec.Name.Equals("CODE", StringComparison.OrdinalIgnoreCase) &&
                    !epSec.Name.Equals("text", StringComparison.OrdinalIgnoreCase))
                {
                    suspiciousScore += 15;
                }
            }
        }

        if (_pe.ImportedFunctions == null || !(_pe.ImportedFunctions.Length == 0))
        {
            suspiciousScore += 25;
        }
        else
        {
            Int32 dllCount = _pe.ImportedFunctions.Select(f => f.DLL).Distinct().Count();
            Int32 apiCount = _pe.ImportedFunctions.Length;
            if (dllCount <= 2 && apiCount <= 5)
                suspiciousScore += 20;
            else if (dllCount <= 3)
                suspiciousScore += 10;
        }

        if (_pe.ImageSectionHeaders != null && _pe.ImageSectionHeaders.Length > 7)
            suspiciousScore += 10;

        if (_pe.ImageTlsDirectory != null)
            suspiciousScore += 10;

        if (_pe.ImageSectionHeaders != null)
        {
            HashSet<String> standardNames = new(StringComparer.OrdinalIgnoreCase)
            {
                ".text", "text", "CODE", ".data", "DATA", ".rsrc", "rsrc",
                ".reloc", "reloc", ".pdata", "pdata", ".xdata", "xdata",
                ".edata", "edata", ".idata", "idata", ".bss", "bss",
                ".crt", "crt", ".tls", "tls"
            };

            foreach (ImageSectionHeader? sec in _pe.ImageSectionHeaders)
            {
                if (!String.IsNullOrEmpty(sec.Name) && !standardNames.Contains(sec.Name))
                {
                    suspiciousScore += 5;
                    break;
                }
            }
        }

        return suspiciousScore >= 30 ? ShellHits.Suspicious : ShellHits.Emtpy;
    }
}
