using Charwolf.AhoCorasick;

namespace Charwolf.XSRule;

public sealed class CompiledXsRuleString
{
    public Int32 RuleIndex { get; set; }
    public Int32 StringId { get; set; }
}

public sealed class CompiledXsRulePart
{
    public AcScanner? Scanner { get; set; }
    public List<CompiledXsRuleString>[] PatternOwners { get; set; } = [];
}

public sealed class CompiledXsRuleDocument : IDisposable
{
    private readonly Dictionary<XsRulePart, CompiledXsRulePart> _parts = new();
    private Boolean _disposed;

    public IReadOnlyList<XsRuleDefinition> Rules { get; set; } = [];

    public CompiledXsRulePart GetPart(XsRulePart part)
    {
        _parts.TryGetValue(part, out CompiledXsRulePart? value);
        return value ?? new CompiledXsRulePart();
    }

    public void SetPart(XsRulePart part, CompiledXsRulePart compiled)
    {
        _parts[part] = compiled;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (CompiledXsRulePart part in _parts.Values)
            {
                part.Scanner?.Dispose();
            }
            _disposed = true;
        }
    }
}

public sealed class XsRuleCompiler
{
    public const Int32 MaxHeadSize = 64 * 1024;
    public const Int32 MaxCodeSize = 32 * 1024;
    public const Int32 MaxResourceSize = 32 * 1024;
    public const Int32 MaxWildcardExpansion = 2;

    public CompiledXsRuleDocument Compile(XsRuleDocument document)
    {
        CompiledXsRuleDocument compiled = new() { Rules = document.Rules };

        foreach (XsRulePart part in Enum.GetValues<XsRulePart>())
        {
            List<(Byte[] Pattern, Int32 RuleIndex, Int32 StringId)> patterns = [];

            for (Int32 ruleIndex = 0; ruleIndex < document.Rules.Count; ruleIndex++)
            {
                XsRuleDefinition rule = document.Rules[ruleIndex];
                foreach (XsRuleString str in rule.Strings)
                {
                    if (str.Part != part)
                        continue;

                    foreach (Byte[] concrete in ToConcretePatterns(str))
                    {
                        patterns.Add((AsciiToLower(concrete), ruleIndex, str.Id));
                    }
                }
            }

            if (patterns.Count == 0)
                continue;

            CompiledXsRulePart compiledPart = BuildCompiledPart(patterns);
            compiled.SetPart(part, compiledPart);
        }

        return compiled;
    }

    private static CompiledXsRulePart BuildCompiledPart(List<(Byte[] Pattern, Int32 RuleIndex, Int32 StringId)> patterns)
    {
        ReadOnlyMemory<Byte>[] patternMemory = new ReadOnlyMemory<Byte>[patterns.Count];
        for (Int32 i = 0; i < patterns.Count; i++)
            patternMemory[i] = patterns[i].Pattern;

        TrieNode root = TrieBuilder.Build(patternMemory);
        DoubleArrayResult result = DoubleArrayBuilder.Build(root, patterns.Count);

        List<CompiledXsRuleString>[] owners = new List<CompiledXsRuleString>[patterns.Count];
        for (Int32 i = 0; i < owners.Length; i++)
            owners[i] = [];

        for (Int32 i = 0; i < patterns.Count; i++)
        {
            owners[i].Add(new CompiledXsRuleString
            {
                RuleIndex = patterns[i].RuleIndex,
                StringId = patterns[i].StringId
            });
        }

        return new CompiledXsRulePart
        {
            Scanner = new AcScanner(result),
            PatternOwners = owners
        };
    }

    private static IEnumerable<Byte[]> ToConcretePatterns(XsRuleString str)
    {
        if (!str.HasWildcard)
        {
            yield return str.Pattern;
            yield break;
        }

        Int32 wildcardCount = 0;
        foreach (Boolean b in str.WildcardMask)
        {
            if (b) wildcardCount++;
        }

        if (wildcardCount > MaxWildcardExpansion)
            throw new InvalidOperationException($"Rule string has too many wildcards ({wildcardCount} > {MaxWildcardExpansion})");

        Int32 total = 1 << (wildcardCount * 8);
        Byte[] pattern = str.Pattern;
        Boolean[] mask = str.WildcardMask;

        for (Int32 combination = 0; combination < total; combination++)
        {
            Byte[] concrete = new Byte[pattern.Length];
            Int32 bits = combination;
            for (Int32 i = 0; i < pattern.Length; i++)
            {
                if (mask[i])
                {
                    concrete[i] = (Byte)(bits & 0xFF);
                    bits >>= 8;
                }
                else
                {
                    concrete[i] = pattern[i];
                }
            }
            yield return concrete;
        }
    }

    private static Byte[] AsciiToLower(Byte[] data)
    {
        Byte[] result = new Byte[data.Length];
        for (Int32 i = 0; i < data.Length; i++)
        {
            Byte b = data[i];
            result[i] = b is >= (Byte)'A' and <= (Byte)'Z' ? (Byte)(b + 32) : b;
        }
        return result;
    }
}
