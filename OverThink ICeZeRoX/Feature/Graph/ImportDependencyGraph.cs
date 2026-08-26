namespace OverThink_ICeZeRoX.Feature.Graph;

internal static class ImportDependencyGraph
{
    public static Graph Build(PEAnalyzer a, String rootName)
    {
        Graph g = new();
        g.AddNode(PEAnalyzer.NodeFeatureFromAttrs(PEAnalyzer.NODE_TYPE_PE, rootName, 0));

        List<String> dllSet = a.Iat.Values.Select(x => x.Dll).Distinct().OrderBy(x => x).ToList();
        Dictionary<String, Int32> dllId = new(dllSet.Count);
        Int32 idx = 1;
        foreach (String dll in dllSet)
        {
            dllId[dll] = idx;
            g.AddNode(PEAnalyzer.NodeFeatureFromAttrs(PEAnalyzer.NODE_TYPE_DLL, dll, 0));
            g.AddEdge(0, idx, PEAnalyzer.EDGE_TYPE_CONTAINS);
            idx++;
        }

        Int32 nextId = dllId.Count + 1;
        foreach (KeyValuePair<UInt64, (String Dll, String Fname)> kv in a.Iat)
        {
            Int32 fid = nextId++;
            g.AddNode(PEAnalyzer.NodeFeatureFromAttrs(PEAnalyzer.NODE_TYPE_FUNC, kv.Value.Fname, kv.Key));
            g.AddEdge(dllId[kv.Value.Dll], fid, PEAnalyzer.EDGE_TYPE_CONTAINS);
        }
        return g;
    }
}
