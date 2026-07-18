namespace XaocSirck_Core.Interface.Cloud;

public enum CloudCacheResult : Byte
{
    Miss = 0,
    Hit = 1,
    Error = 2,
    Unknown = 4
}

public enum CloudSignatureResult : Byte
{
    Trusted = 0,
    Untrusted = 1,
    Error = 2,
    Unknown = 4
}
