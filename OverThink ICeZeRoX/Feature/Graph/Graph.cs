using System.Text;

namespace OverThink_ICeZeRoX.Feature.Graph;

internal sealed class Graph
{
    public List<Single[]> Nodes { get; } = new();
    public List<(Int32 Src, Int32 Dst, Single Type)> Edges { get; } = new();

    public Int32 AddNode(Single[] feature)
    {
        Nodes.Add(feature);
        return Nodes.Count - 1;
    }

    public void AddEdge(Int32 src, Int32 dst, Single type) => Edges.Add((src, dst, type));
}

internal static class GraphBin
{
    private static readonly Byte[] Magic = Encoding.ASCII.GetBytes("GRPH");

    public static Byte[] Serialize(Graph g)
    {
        Int32 nodeCount = g.Nodes.Count;
        Int32 edgeCount = g.Edges.Count;
        Int32 size = 4 + sizeof(Int32) * 2
                   + nodeCount * PEAnalyzer.FEATURE_DIM * sizeof(Single)
                   + edgeCount * 2 * sizeof(Int32)
                   + edgeCount * sizeof(Single);
        Byte[] buf = new Byte[size];
        using MemoryStream ms = new(buf);
        using BinaryWriter bw = new(ms);
        bw.Write(Magic);
        bw.Write(nodeCount);
        bw.Write(edgeCount);
        foreach (Single[] f in g.Nodes)
        {
            Int32 len = Math.Min(f.Length, PEAnalyzer.FEATURE_DIM);
            for (Int32 i = 0; i < len; i++) bw.Write(f[i]);
            for (Int32 i = len; i < PEAnalyzer.FEATURE_DIM; i++) bw.Write(0f);
        }
        foreach ((Int32 src, Int32 dst, _) in g.Edges)
        {
            bw.Write(src);
            bw.Write(dst);
        }
        foreach ((_, _, Single type) in g.Edges)
            bw.Write(type);
        return buf;
    }
}
