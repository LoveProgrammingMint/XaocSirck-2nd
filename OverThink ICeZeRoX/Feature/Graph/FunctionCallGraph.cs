using Gee.External.Capstone.X86;

namespace OverThink_ICeZeRoX.Feature.Graph;

internal static class FunctionCallGraph
{
    public static Graph Build(PEAnalyzer a)
    {
        Graph g = new();
        List<UInt64> fnVas = a.Functions.Keys.OrderBy(x => x).ToList();
        List<UInt64> iatVas = a.Iat.Keys.OrderBy(x => x).ToList();
        Dictionary<UInt64, Int32> vaToId = new(fnVas.Count + iatVas.Count);

        Int32 idx = 0;
        foreach (UInt64 va in fnVas)
        {
            g.AddNode(PEAnalyzer.NodeFeatureFromAttrs(PEAnalyzer.NODE_TYPE_FUNC, a.Functions[va], va));
            vaToId[va] = idx++;
        }
        foreach (UInt64 va in iatVas)
        {
            (String dll, String fname) = a.Iat[va];
            g.AddNode(PEAnalyzer.NodeFeatureFromAttrs(PEAnalyzer.NODE_TYPE_FUNC, fname, va));
            vaToId[va] = idx++;
        }

        foreach (UInt64 va in fnVas)
        {
            if (!vaToId.TryGetValue(va, out Int32 srcId)) continue;
            X86Instruction[] insns = a.DisasmFunctionCached(va, 0x4000);
            foreach (X86Instruction ins in insns)
            {
                if (!ins.Mnemonic.Equals("call", StringComparison.OrdinalIgnoreCase))
                    continue;
                X86Operand[] ops = PEAnalyzer.GetOperands(ins);
                if (ops.Length == 0) continue;
                X86Operand op = ops[0];
                if (op.Type == X86OperandType.Immediate)
                {
                    UInt64 callee = (UInt64)op.Immediate;
                    if (vaToId.TryGetValue(callee, out Int32 dstId))
                        g.AddEdge(srcId, dstId, PEAnalyzer.EDGE_TYPE_CALL);
                }
                else if (op.Type == X86OperandType.Memory)
                {
                    X86RegisterId baseId = op.Memory.Base?.Id ?? X86RegisterId.Invalid;
                    UInt64? target = null;
                    if (baseId == X86RegisterId.Invalid)
                        target = (UInt64)op.Memory.Displacement;
                    else if (baseId == X86RegisterId.X86_REG_RIP)
                        target = (UInt64)(ins.Address + ins.Bytes.Length + op.Memory.Displacement);
                    if (target.HasValue && vaToId.TryGetValue(target.Value, out Int32 dstId))
                        g.AddEdge(srcId, dstId, PEAnalyzer.EDGE_TYPE_CALL_INDIRECT);
                }
            }
        }
        return g;
    }
}
