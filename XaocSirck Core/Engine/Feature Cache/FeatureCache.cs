using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using XaocSirck_Core.Feature;

namespace XaocSirck_Core.Engine.Feature_Cache;

internal sealed unsafe class FeatureCache
{
    private readonly DatabaseManagement database = new("./database.db");
    private readonly SHA256 sha256 = SHA256.Create();

    public Int32 Count { get => database.Count(); }

    private (Byte[], Single[], Single[], Int32[], Single[]) GetArray(FeaturesStruct features)
    {
        return (new Span<Byte>((Byte*)features.RB, 16384).ToArray(),
                new Span<Single>((Single*)features.EM, 256).ToArray(),
                new Span<Single>((Single*)features.IT, 417).ToArray(),
                new Span<Int32>((Int32*)features.AL, 512).ToArray(),
                new Span<Single>((Single*)features.Zeroflow, 256).ToArray());
    }

    public void Insert(FeaturesStruct features)
    {
        (Byte[] rbData, Single[] emData, Single[] itData, Int32[] alData, Single[] zfData) = GetArray(features);
        String SHA = sha256.ComputeHash(rbData).ToString() ?? "";
        database.Insert(SHA, rbData, emData, itData, alData, zfData);
    }

    public FeatureRecord? Get(String SHA)
    {
        return database.Read(SHA);
    }
}
