using System.Runtime.InteropServices;
using System.Threading.Tasks;
using XaocSirck_Core.Interface.Cloud;
using XaocSirck_Core.Native;

namespace XaocSirck_Core.Cloud;

public sealed unsafe class CloudClient : IDisposable
{
    private XsCommunication* _instance;
    private String _serverAddress = String.Empty;
    private Boolean _disposed;

    public Boolean IsConnected => _instance != null && _serverAddress.Length > 0;

    public Int32 RetryCount { get; set; } = 3;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

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

        Byte result = InvokeWithRetry<Byte>(() =>
        {
            IntPtr data = Marshal.AllocHGlobal(32);
            try
            {
                Marshal.Copy(sha256, 0, data, 32);
                XsApiPack* pack = OnlineService.XsApi_CacheQueryPack((Byte*)data, (UInt64)sha256.Length);
                if (pack == null)
                    return (Byte)2;

                try { return OnlineService.XsCommunication_CacheQuery(_instance, pack); }
                finally { OnlineService.XsApi_FreePack(pack); }
            }
            finally { Marshal.FreeHGlobal(data); }
        }, r => r == 2, (Byte)2, nameof(QueryCache));

        return result switch
        {
            0 => CloudCacheResult.Miss,
            1 => CloudCacheResult.Hit,
            2 => CloudCacheResult.Error,
            4 => CloudCacheResult.Unknown,
            _ => CloudCacheResult.Error
        };
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

        Byte result = InvokeWithRetry<Byte>(() =>
        {
            IntPtr data = Marshal.AllocHGlobal(32);
            try
            {
                Marshal.Copy(sha256, 0, data, 32);
                XsApiPack* pack = OnlineService.XsApi_ReportPack((Byte*)data, (UInt64)sha256.Length, filePath);
                if (pack == null)
                    return (Byte)1;

                try { return OnlineService.XsCommunication_Report(_instance, pack); }
                finally { OnlineService.XsApi_FreePack(pack); }
            }
            finally { Marshal.FreeHGlobal(data); }
        }, r => r != 0, (Byte)1, nameof(Report));

        return result == 0;
    }

    public CloudSignatureResult QuerySignatureTrust(Byte[] sha256)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(CloudClient));
        ArgumentNullException.ThrowIfNull(sha256);
        if (sha256.Length != 32)
            throw new ArgumentException("SHA256 must be 32 bytes.", nameof(sha256));
        if (_instance == null)
            return CloudSignatureResult.Error;

        Byte result = InvokeWithRetry<Byte>(() =>
        {
            IntPtr data = Marshal.AllocHGlobal(32);
            try
            {
                Marshal.Copy(sha256, 0, data, 32);
                XsApiPack* pack = OnlineService.XsApi_SignatureQueryPack((Byte*)data, (UInt64)sha256.Length);
                if (pack == null)
                    return (Byte)2;

                try { return OnlineService.XsCommunication_SignatureQuery(_instance, pack); }
                finally { OnlineService.XsApi_FreePack(pack); }
            }
            finally { Marshal.FreeHGlobal(data); }
        }, r => r == 2, (Byte)2, nameof(QuerySignatureTrust));

        return result switch
        {
            0 => CloudSignatureResult.Trusted,
            1 => CloudSignatureResult.Untrusted,
            2 => CloudSignatureResult.Error,
            4 => CloudSignatureResult.Unknown,
            _ => CloudSignatureResult.Error
        };
    }

    private T InvokeWithRetry<T>(Func<T> action, Func<T, Boolean> shouldRetry, T fallback, String operationName)
    {
        for (Int32 i = 0; i < RetryCount; i++)
        {
            try
            {
                T result = InvokeWithTimeout(action, Timeout);
                if (!shouldRetry(result))
                    return result;

                App.Logger.Warning($"{operationName} returned retryable result (attempt {i + 1}/{RetryCount})");
            }
            catch (Exception ex)
            {
                App.Logger.Error($"{operationName} failed (attempt {i + 1}/{RetryCount})", ex);
            }
        }
        return fallback;
    }

    private T InvokeWithTimeout<T>(Func<T> action, TimeSpan timeout)
    {
        Task<T> task = Task.Run(action);
        if (task.Wait(timeout))
            return task.Result;

        App.Logger.Warning("Cloud operation timed out");
        throw new TimeoutException("Cloud operation timed out");
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
