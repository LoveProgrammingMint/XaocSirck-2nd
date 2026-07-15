using System;
using System.Collections.Generic;
using System.Text;
using XaocSirck_Core.Feature.Engineering;
using XaocSirck_Core.Feature.Engineering.Zeroflows;
using XaocSirck_Core.Feature.Extraction;
using XaocSirck_Core.Feature.Obtain;
using XaocSirck_Core.Interface.Engine;

namespace XaocSirck_Core.Feature;

internal struct FeaturesStruct
{
    public IntPtr RB;
    public IntPtr AL;
    public IntPtr IT;
    public IntPtr EM;
    public IntPtr Zeroflow;
}

internal struct FeaturesStruct_Cache
{
    public IntPtr RB;
    public const Int32 RB_L = 16384;
    public IntPtr AL;
    public const Int32 AL_L = 512;
    public IntPtr IT;
    public const Int32 IT_L = 417;
    public IntPtr EM;
    public const Int32 EM_L = 256;
    public IntPtr Zeroflow;
    public const Int32 Zeroflows_L = 256;
}

internal sealed unsafe class Features
{
    private EngineMode mode;
    private readonly SharePool BSP = new();
    // Obtains
    private readonly RawByteObtain RBO = new();
    private readonly AssemblyListObtain ALO = new();
    private readonly ImportTableObtain ITO = new();

    // Extractions
    private readonly RawByteExtraction RBE = new();
    private readonly AssemblyListExtraction ALE = new();

    // Engineerings
    private readonly EntropyMapEngineering EME = new();

    // Zeroflows
    private readonly ShareFeatures ZSF = new();
    private readonly ByteHistogram? ZBH;
    private readonly BytePatterns? ZBP;
    private readonly ByteRuns? ZBR;
    private readonly ByteStatistics? ZBS;
    private readonly Entropys? ZBE;
    private readonly ByteLoop? ZBL;

    private readonly PeMetadata? ZPM;
    private readonly PeExtended? ZPE;
    private readonly PeStatistics? ZPS;

    public Features()
    {
        RBO.Set(BSP);
        ALO.Set(BSP);
        ITO.Set(BSP);
        EME.Set(BSP);

        ZBH = new ByteHistogram(ZSF);
        ZBP = new BytePatterns(ZSF);
        ZBR = new ByteRuns(ZSF);
        ZBS = new ByteStatistics(ZSF);
        ZBE = new Entropys(ZSF);
        ZBL = new ByteLoop(ZSF);
        ZBL.Register(ZBH, ZBE, ZBS, ZBR, ZBP);
        ZPM = new PeMetadata(ZSF);
        ZPE = new PeExtended(ZSF);
        ZPS = new PeStatistics(ZSF);
        ZPS.Register(ZPM, ZPE);
    }

    public FeaturesStruct Execute()
    {
        FeaturesStruct features = new();
        switch (mode.Bitremal)
        {
            case _Mode_Bitremal.Disabled:
                break;
            case _Mode_Bitremal.Rb:
                RBO.Clear();
                RBO.Obtain();
                RBE.Clear();
                RBE.Set(RBO.GetResult());
                RBE.Extract();
                features.RB = RBE.GetResult();
                break;
            case _Mode_Bitremal.Al:
                ALO.Clear();
                ALO.Obtain();
                ALE.Clear();
                ALE.Set(ALO.GetResult());
                ALE.Extract();
                features.AL = ALE.GetResult();
                break;
            case _Mode_Bitremal.Ot:
                RBO.Clear();
                RBO.Obtain();
                RBE.Clear();
                RBE.Set(RBO.GetResult());
                RBE.Extract();
                features.RB = RBE.GetResult();
                ALO.Clear();
                ALO.Obtain();
                ALE.Clear();
                ALE.Set(ALO.GetResult());
                ALE.Extract();
                features.AL = ALE.GetResult();
                ITO.Clear();
                ITO.Obtain();
                features.IT = ITO.GetResult();
                EME.Clear();
                EME.Engineer();
                features.EM = EME.GetResult();
                break;
        }

        switch (mode.Zeroflow)
        {
            case _Mode_Zeroflows.Disabled:
                break;
            case _Mode_Zeroflows.Zf:
                ZBL?.LoadFromFile(BSP.FilePath ?? throw new ArgumentNullException(BSP.FilePath));
                ZBL?.Execute();
                ZPS?.LoadFromFile(BSP.FilePath ?? throw new ArgumentNullException(BSP.FilePath));
                ZPS?.Execute();
                features.Zeroflow = (IntPtr)ZSF.FeatureTensor;
                break;
        }
        return features;
    }

    public FeaturesStruct_Cache Execute_Cache()
    {
        FeaturesStruct_Cache features = new();

        RBO.Clear();
        RBO.Obtain();
        features.RB = RBO.GetResult();
        ALO.Clear();
        ALO.Obtain();
        features.AL = ALO.GetResult();
        ITO.Clear();
        ITO.Obtain();
        features.IT = ITO.GetResult();
        EME.Clear();
        EME.Engineer();
        features.EM = EME.GetResult();

        ZBL?.LoadFromFile(BSP.FilePath ?? throw new ArgumentNullException(BSP.FilePath));
        ZBL?.Execute();
        ZPS?.LoadFromFile(BSP.FilePath ?? throw new ArgumentNullException(BSP.FilePath));
        ZPS?.Execute();
        features.Zeroflow = (IntPtr)ZSF.FeatureTensor;
        return features;
    }

    public void Set(String path, EngineMode newmode)
    {
        BSP.FilePath = path;
        mode = newmode;
    }
}
