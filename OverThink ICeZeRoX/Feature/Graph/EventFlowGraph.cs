using Gee.External.Capstone.X86;

namespace OverThink_ICeZeRoX.Feature.Graph;

internal static class EventFlowGraph
{
    public static Graph Build(PEAnalyzer a)
    {
        Graph g = new();
        Int32 nodeId = 0;
        UInt64[] fnVas = a.Functions.Keys.OrderBy(x => x).ToArray();
        foreach (UInt64 fnVa in fnVas)
        {
            X86Instruction[] insns = a.DisasmFunctionCached(fnVa, 0x4000);
            if (insns.Length == 0) continue;
            List<BasicBlock> blocks = a.SplitBasicBlocks(insns);
            if (blocks.Count == 0) continue;
            Dictionary<UInt64, BasicBlock> blocksByStart = blocks.ToDictionary(b => b.Leader);
            Dictionary<UInt64, List<Int32>> blockEvents = new();

            foreach (BasicBlock blk in blocks)
            {
                List<Int32> evIds = new();
                foreach (X86Instruction ins in blk.Instructions)
                {
                    if (!ins.Mnemonic.Equals("call", StringComparison.OrdinalIgnoreCase))
                        continue;
                    (Int32 cat, String? fname, String? _) = a.ResolveCall(ins);
                    Single[] feat = a.EventFeature(ins, cat, fname, fnVa);
                    g.AddNode(feat);
                    evIds.Add(nodeId);
                    nodeId++;
                }
                blockEvents[blk.Leader] = evIds;
                for (Int32 i = 0; i < evIds.Count - 1; i++)
                    g.AddEdge(evIds[i], evIds[i + 1], PEAnalyzer.EDGE_TYPE_INTRABLOCK);
            }

            foreach (BasicBlock blk in blocks)
            {
                if (!blockEvents.TryGetValue(blk.Leader, out List<Int32>? evIds) || evIds.Count == 0)
                    continue;
                Int32 lastEv = evIds[^1];
                foreach (UInt64 succ in a.BlockSuccessors(blk.Instructions, blocksByStart))
                {
                    if (blockEvents.TryGetValue(succ, out List<Int32>? succEvIds) && succEvIds.Count > 0)
                        g.AddEdge(lastEv, succEvIds[0], PEAnalyzer.EDGE_TYPE_INTERBLOCK);
                }
            }
        }
        return g;
    }
}
