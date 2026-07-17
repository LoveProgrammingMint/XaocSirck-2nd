using System.Buffers;
using Charwolf.AhoCorasick;
using PeNet;
using PeNet.Header.Pe;

namespace Charwolf.XSRule;

public sealed class SignatureMatch
{
    public String RuleName { get; set; } = String.Empty;
    public Int32 RuleIndex { get; set; }
    public Int32 StringId { get; set; }
    public XsRulePart Part { get; set; }
    public Int32 Offset { get; set; }
    public Int32 Length { get; set; }
}

public sealed class SignatureResult
{
    public String RuleName { get; set; } = String.Empty;
    public Int32 RuleIndex { get; set; }
    public Boolean Matched { get; set; }
    public List<SignatureMatch> Matches { get; set; } = [];
}

public sealed class SignatureEngine : IDisposable
{
    private readonly CompiledXsRuleDocument _compiled;
    private Boolean _disposed;

    public SignatureEngine(CompiledXsRuleDocument compiled)
    {
        _compiled = compiled ?? throw new ArgumentNullException(nameof(compiled));
    }

    public IReadOnlyList<SignatureResult> ScanFile(String filePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SignatureEngine));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}", filePath);

        Byte[] fileBytes = File.ReadAllBytes(filePath);
        PeFile? pe = null;
        try
        {
            pe = new PeFile(fileBytes);
        }
        catch
        {
            pe = null;
        }

        HashSet<(Int32 RuleIndex, Int32 StringId)> hits = [];

        ScanPart(XsRulePart.Head, GetHeadRegion(fileBytes), hits);
        ScanPart(XsRulePart.Code, GetCodeRegion(pe), hits);
        ScanPart(XsRulePart.Res, GetResourceRegion(pe), hits);
        ScanPart(XsRulePart.Imp, GetImportRegion(pe), hits);
        ScanPart(XsRulePart.Exp, GetExportRegion(pe), hits);

        return EvaluateRules(hits);
    }

    private void ScanPart(XsRulePart part, ReadOnlyMemory<Byte> data, HashSet<(Int32 RuleIndex, Int32 StringId)> hits)
    {
        if (data.Length == 0)
            return;

        CompiledXsRulePart compiledPart = _compiled.GetPart(part);
        if (compiledPart.Scanner == null)
            return;

        Byte[] lower = ArrayPool<Byte>.Shared.Rent(data.Length);
        try
        {
            AsciiToLower(data.Span, lower);

            Int32 maxMatches = data.Length;
            AcMatch[] buffer = ArrayPool<AcMatch>.Shared.Rent(maxMatches);
            try
            {
                Int32 count = compiledPart.Scanner.Scan(lower.AsSpan(0, data.Length), buffer);
                for (Int32 i = 0; i < count; i++)
                {
                    AcMatch match = buffer[i];
                    foreach (CompiledXsRuleString owner in compiledPart.PatternOwners[match.PatternId])
                    {
                        hits.Add((owner.RuleIndex, owner.StringId));
                    }
                }
            }
            finally
            {
                ArrayPool<AcMatch>.Shared.Return(buffer);
            }
        }
        finally
        {
            ArrayPool<Byte>.Shared.Return(lower);
        }
    }

    private List<SignatureResult> EvaluateRules(HashSet<(Int32 RuleIndex, Int32 StringId)> hits)
    {
        List<SignatureResult> results = [];
        for (Int32 i = 0; i < _compiled.Rules.Count; i++)
        {
            XsRuleDefinition rule = _compiled.Rules[i];
            Boolean matched = EvaluateCondition(rule.Condition, i, hits);
            SignatureResult result = new() { RuleName = rule.Name, RuleIndex = i, Matched = matched };
            if (matched)
            {
                foreach ((Int32 ruleIndex, Int32 stringId) hit in hits)
                {
                    if (hit.ruleIndex == i)
                    {
                        result.Matches.Add(new SignatureMatch
                        {
                            RuleName = rule.Name,
                            RuleIndex = i,
                            StringId = hit.stringId
                        });
                    }
                }
            }
            results.Add(result);
        }
        return results;
    }

    private static Boolean EvaluateCondition(XsRuleCondition condition, Int32 ruleIndex, HashSet<(Int32 RuleIndex, Int32 StringId)> hits)
    {
        return condition switch
        {
            XsRuleConditionLiteral literal => hits.Contains((ruleIndex, literal.StringId)),
            XsRuleConditionNot not => !EvaluateCondition(not.Operand, ruleIndex, hits),
            XsRuleConditionAnd and => EvaluateCondition(and.Left, ruleIndex, hits) && EvaluateCondition(and.Right, ruleIndex, hits),
            XsRuleConditionOr or => EvaluateCondition(or.Left, ruleIndex, hits) || EvaluateCondition(or.Right, ruleIndex, hits),
            _ => false
        };
    }

    private static ReadOnlyMemory<Byte> GetHeadRegion(Byte[] fileBytes)
    {
        Int32 length = Math.Min(fileBytes.Length, XsRuleCompiler.MaxHeadSize);
        return fileBytes.AsMemory(0, length);
    }

    private static ReadOnlyMemory<Byte> GetCodeRegion(PeFile? pe)
    {
        if (pe?.ImageSectionHeaders == null)
            return ReadOnlyMemory<Byte>.Empty;

        using MemoryStream ms = new();
        foreach (ImageSectionHeader section in pe.ImageSectionHeaders)
        {
            if ((section.Characteristics & ScnCharacteristicsType.MemExecute) == ScnCharacteristicsType.MemExecute)
            {
                Byte[] data = pe.RawFile.AsSpan((Int32)section.PointerToRawData, (Int32)section.SizeOfRawData).ToArray();
                ms.Write(data, 0, data.Length);
                if (ms.Length >= XsRuleCompiler.MaxCodeSize)
                    break;
            }
        }

        Byte[] result = ms.ToArray();
        if (result.Length > XsRuleCompiler.MaxCodeSize)
            result = result[..XsRuleCompiler.MaxCodeSize];
        return result;
    }

    private static ReadOnlyMemory<Byte> GetResourceRegion(PeFile? pe)
    {
        if (pe?.ImageSectionHeaders == null)
            return ReadOnlyMemory<Byte>.Empty;

        ImageSectionHeader? rsrc = Array.Find(pe.ImageSectionHeaders, s => s.Name.Equals(".rsrc", StringComparison.OrdinalIgnoreCase));
        if (rsrc == null)
            return ReadOnlyMemory<Byte>.Empty;

        Byte[] data = pe.RawFile.AsSpan((Int32)rsrc.PointerToRawData, (Int32)rsrc.SizeOfRawData).ToArray();
        if (data.Length > XsRuleCompiler.MaxResourceSize)
            data = data[..XsRuleCompiler.MaxResourceSize];
        return data;
    }

    private static ReadOnlyMemory<Byte> GetImportRegion(PeFile? pe)
    {
        if (pe?.ImportedFunctions == null)
            return ReadOnlyMemory<Byte>.Empty;

        using MemoryStream ms = new();
        foreach (var import in pe.ImportedFunctions)
        {
            if (!String.IsNullOrEmpty(import.DLL))
            {
                Byte[] bytes = System.Text.Encoding.ASCII.GetBytes(import.DLL + "\0");
                ms.Write(bytes, 0, bytes.Length);
            }
            if (!String.IsNullOrEmpty(import.Name))
            {
                Byte[] bytes = System.Text.Encoding.ASCII.GetBytes(import.Name + "\0");
                ms.Write(bytes, 0, bytes.Length);
            }
        }
        return ms.ToArray();
    }

    private static ReadOnlyMemory<Byte> GetExportRegion(PeFile? pe)
    {
        if (pe?.ExportedFunctions == null)
            return ReadOnlyMemory<Byte>.Empty;

        using MemoryStream ms = new();
        foreach (var export in pe.ExportedFunctions)
        {
            if (!String.IsNullOrEmpty(export.Name))
            {
                Byte[] bytes = System.Text.Encoding.ASCII.GetBytes(export.Name + "\0");
                ms.Write(bytes, 0, bytes.Length);
            }
        }
        return ms.ToArray();
    }

    private static void AsciiToLower(ReadOnlySpan<Byte> source, Byte[] destination)
    {
        for (Int32 i = 0; i < source.Length; i++)
        {
            Byte b = source[i];
            destination[i] = b is >= (Byte)'A' and <= (Byte)'Z' ? (Byte)(b + 32) : b;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _compiled.Dispose();
            _disposed = true;
        }
    }
}
