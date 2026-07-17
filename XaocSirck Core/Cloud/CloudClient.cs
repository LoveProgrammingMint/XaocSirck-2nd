using System.Runtime.InteropServices;
using XaocSirck_Core.Native;

namespace XaocSirck_Core.Cloud;

public sealed unsafe class CloudClient : IDisposable
{
    private XsCommunication* _instance;
    private String _serverAddress = String.Empty;
    private Boolean _disposed;

    public Boolean IsConnected => _instance != null && _serverAddress.Length > 0;

    public void Connect(String address)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(CloudClient));
        ArgumentNullException.ThrowIfNull(address);

        Disconnect();
        _instance = OnlineService.XsCommunication_Create();
        if (_instance == null)
            throw new InvalidOperationException("Failed to create online service instance.");

        OnlineService.XsCommunication_SetServerAddress(_instance, address);
        _serverAddress = address;
    }

    public void Disconnect()
    {
        if (_instance != null)
        {
            OnlineService.XsCommunication_Destroy(_instance);
            _instance = null;
        }
        _serverAddress = String.Empty;
    }

    public CloudCacheResult QueryCache(Byte[] sha256)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(CloudClient));
        ArgumentNullException.ThrowIfNull(sha256);
        if (sha256.Length != 32)
            throw new ArgumentException("SHA256 must be 32 bytes.", nameof(sha256));
        if (_instance == null)
            throw new InvalidOperationException("Not connected.");

        fixed (Byte* data = sha256)
        {
            XsApiPack* pack = OnlineService.XsApi_CacheQueryPack(data, (UInt64)sha256.Length);
            if (pack == null)
                return CloudCacheResult.Error;

            try
            {
                Byte result = OnlineService.XsCommunication_CacheQuery(_instance, pack);
                return result switch
                {
                    0 => CloudCacheResult.Miss,
                    1 => CloudCacheResult.Hit,
                    2 => CloudCacheResult.Error,
                    4 => CloudCacheResult.Unknown,
                    _ => CloudCacheResult.Error
                };
            }
            finally
            {
                OnlineService.XsApi_FreePack(pack);
            }
        }
    }

    public CloudSignatureResult QuerySignatureTrust(Byte[] sha256)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(CloudClient));
        ArgumentNullException.ThrowIfNull(sha256);
        if (sha256.Length != 32)
            throw new ArgumentException("SHA256 must be 32 bytes.", nameof(sha256));
        if (_instance == null)
            return CloudSignatureResult.Error;

        fixed (Byte* data = sha256)
        {
            XsApiPack* pack = OnlineService.XsApi_SignatureQueryPack(data, (UInt64)sha256.Length);
            if (pack == null)
                return CloudSignatureResult.Error;

            try
            {
                Byte result = OnlineService.XsCommunication_SignatureQuery(_instance, pack);
                return result switch
                {
                    0 => CloudSignatureResult.Trusted,
                    1 => CloudSignatureResult.Untrusted,
                    2 => CloudSignatureResult.Error,
                    4 => CloudSignatureResult.Unknown,
                    _ => CloudSignatureResult.Error
                };
            }
            finally
            {
                OnlineService.XsApi_FreePack(pack);
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Disconnect();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

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
