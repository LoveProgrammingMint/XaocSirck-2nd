using System.Runtime.InteropServices;
namespace XaocSirck_Core.Feature.Engineering.Zeroflows;
internal static unsafe class Log2Approx
{
    private const Int32 TableSize = 2048;
    private static readonly Single* Table;

    static Log2Approx()
    {
        Table = (Single*)NativeMemory.AlignedAlloc((UIntPtr)(TableSize * sizeof(Single)), 64);
        Single log2 = MathF.Log(2.0f);
        for (Int32 i = 1; i < TableSize; i++)
            Table[i] = MathF.Log((Single)i / TableSize) / log2;
        Table[0] = 0.0f;
    }

    public static Single Lookup(Single p)
    {
        if (p <= 0.0f) return 0.0f;
        Int32 idx = (Int32)(p * (TableSize - 1));
        if (idx >= TableSize) idx = TableSize - 1;
        if (idx < 1) idx = 1;
        return Table[idx];
    }

    public static Single EntropyContribution(Single p)
    {
        return -p * Lookup(p);
    }
}
