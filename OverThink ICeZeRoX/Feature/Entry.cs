using PeNet;

using OverThink_ICeZeRoX.Feature.Graph;
using OverThink_ICeZeRoX.Feature.Image;
using OverThink_ICeZeRoX.Feature.Sequence;

namespace OverThink_ICeZeRoX.Feature;

public static class Entry
{
    public sealed record GenerationResult(String Tag, String Path, Int32 Bytes);

    public static IReadOnlyList<GenerationResult> GenerateAll(String pePath, String outputDir)
    {
        Directory.CreateDirectory(outputDir);
        String baseName = Path.GetFileNameWithoutExtension(pePath);
        String rootName = Path.GetFileName(pePath).ToLowerInvariant();
        Byte[] raw = File.ReadAllBytes(pePath);
        PeFile pe = new(pePath);

        List<GenerationResult> results = new(8);
        using PEAnalyzer analyzer = new(pe, raw);
        analyzer.DiscoverFunctions();

        results.Add(Write(outputDir, baseName, "RBGI", RawByteGrayscaleImage.Build(raw)));
        results.Add(Write(outputDir, baseName, "PESSI", PEStructuralSemanticImage.Build(raw, pe)));
        results.Add(Write(outputDir, baseName, "BEVI", EntropyVisualizationImage.Build(raw)));
        results.Add(Write(outputDir, baseName, "EFG", GraphBin.Serialize(EventFlowGraph.Build(analyzer))));
        results.Add(Write(outputDir, baseName, "IDG", GraphBin.Serialize(ImportDependencyGraph.Build(analyzer, rootName))));
        results.Add(Write(outputDir, baseName, "FCG", GraphBin.Serialize(FunctionCallGraph.Build(analyzer))));
        results.Add(Write(outputDir, baseName, "DIS", DisassemblySequence.Build(pe, raw)));
        results.Add(Write(outputDir, baseName, "CMS", CharacterMappingSequence.Build(pe, raw)));

        return results;
    }

    private static GenerationResult Write(String dir, String baseName, String tag, Byte[] data)
    {
        String path = Path.Combine(dir, $"{baseName}_{tag}.bin");
        File.WriteAllBytes(path, data);
        return new GenerationResult(tag, path, data.Length);
    }
}
