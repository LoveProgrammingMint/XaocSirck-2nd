using System.Runtime.InteropServices;
using XaocSirck_Core.Interface.Cloud;
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
        {
            App.Logger.Error($"Cloud service instance creation failed: {address}");
            throw new InvalidOperationException("Failed to create online service instance.");
        }

        OnlineService.XsCommunication_SetServerAddress(_instance, address);
        _serverAddress = address;
        App.Logger.Info($"Cloud service connected: {address}");
    }

    public void Disconnect()
    {
        if (_instance != null)
        {
            OnlineService.XsCommunication_Destroy(_instance);
            _instance = null;
            App.Logger.Info("Cloud service disconnected");
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
            {
                App.Logger.Warning("Cloud cache query pack creation failed");
                return CloudCacheResult.Error;
            }

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

    public Boolean Report(Byte[] sha256, String filePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(CloudClient));
        ArgumentNullException.ThrowIfNull(sha256);
        ArgumentNullException.ThrowIfNull(filePath);
        if (sha256.Length != 32)
            throw new ArgumentException("SHA256 must be 32 bytes.", nameof(sha256));
        if (_instance == null)
            throw new InvalidOperationException("Not connected.");

        fixed (Byte* data = sha256)
        {
            XsApiPack* pack = OnlineService.XsApi_ReportPack(data, (UInt64)sha256.Length, filePath);
            if (pack == null)
            {
                App.Logger.Warning("Cloud report pack creation failed");
                return false;
            }

            try
            {
                Byte result = OnlineService.XsCommunication_Report(_instance, pack);
                return result == 0;
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
            {
                App.Logger.Warning("Cloud signature query pack creation failed");
                return CloudSignatureResult.Error;
            }

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
            App.Logger.Info("CloudClient disposed");
        }
    }
}
