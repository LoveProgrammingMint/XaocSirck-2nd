namespace XaocSirck_Core.Feature.Engineering.Zeroflows;
internal static class FeatureLayout
{
    public const Int32 ByteHistogramOffset = 0;
    public const Int32 ByteHistogramLength = 32;

    public const Int32 EntropysOffset = 32;
    public const Int32 EntropysLength = 31;

    public const Int32 ByteStatisticsOffset = 63;
    public const Int32 ByteStatisticsLength = 17;

    public const Int32 ByteRunsOffset = 80;
    public const Int32 ByteRunsLength = 5;

    public const Int32 BytePatternsOffset = 85;
    public const Int32 BytePatternsLength = 1;

    public const Int32 PeMetadataOffset = 86;
    public const Int32 PeMetadataLength = 131;

    public const Int32 PeExtendedOffset = 217;
    public const Int32 PeExtendedLength = 39;

    public const Int32 TotalUsedLength = 256;
}
