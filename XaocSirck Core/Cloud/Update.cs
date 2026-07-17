using System.Runtime.InteropServices;
using XaocSirck_Core.Native;

namespace XaocSirck_Core.Cloud;

public sealed unsafe class UpdateClient : IDisposable
{
    private readonly String _packagePath;
    private readonly String _extractPath;
    private readonly String _updaterPath;
    private XsCommunication* _instance;
    private String _serverAddress = String.Empty;
    private Boolean _disposed;

    public Boolean IsConnected => _instance != null && _serverAddress.Length > 0;

    public UpdateClient(String? packagePath = null, String? extractPath = null, String? updaterPath = null)
    {
        _packagePath = packagePath ?? "./update_temp/update_pkg.izxs";
        _extractPath = extractPath ?? "./update_temp/decompressed";
        _updaterPath = updaterPath ?? "./Updater.exe";
    }

    public void Connect(String address)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(UpdateClient));
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

    public String? CheckVersion()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(UpdateClient));
        if (_instance == null)
            throw new InvalidOperationException("Not connected.");

        XsApiPack* pack = OnlineService.XsApi_UpdateVersionPack();
        if (pack == null)
            return null;

        Char* versionPtr = null;
        try
        {
            versionPtr = OnlineService.XsCommunication_UpdateVersion(_instance, pack);
            if (versionPtr == null)
                return null;
            return Marshal.PtrToStringUni((IntPtr)versionPtr);
        }
        finally
        {
            OnlineService.XsApi_FreePack(pack);
            if (versionPtr != null)
                OnlineService.XsCommunication_FreeString(versionPtr);
        }
    }

    public void Download()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(UpdateClient));
        if (_instance == null)
            throw new InvalidOperationException("Not connected.");

        XsApiPack* pack = OnlineService.XsApi_UpdateDownloadPack();
        if (pack == null)
            throw new InvalidOperationException("Failed to build download pack.");

        Byte* buffer = null;
        UInt64 length = 0;
        try
        {
            buffer = OnlineService.XsCommunication_UpdateDownload(_instance, pack, &length);
            if (buffer == null || length == 0)
                throw new InvalidOperationException("Download returned empty data.");

            String? packageDir = Path.GetDirectoryName(_packagePath);
            if (!String.IsNullOrEmpty(packageDir))
                Directory.CreateDirectory(packageDir);

            using FileStream stream = new(_packagePath, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.Write(new ReadOnlySpan<Byte>(buffer, (Int32)length));
        }
        finally
        {
            OnlineService.XsApi_FreePack(pack);
            if (buffer != null)
                OnlineService.XsCommunication_FreeBuffer(buffer);
        }
    }

    public void Apply(String? serviceName = null)
    {
        if (Directory.Exists(_extractPath))
            Directory.Delete(_extractPath, true);
        Directory.CreateDirectory(_extractPath);

        System.IO.Compression.ZipFile.ExtractToDirectory(_packagePath, _extractPath, overwriteFiles: true);

        String updaterFullPath = Path.GetFullPath(_updaterPath);
        if (!File.Exists(updaterFullPath))
            throw new FileNotFoundException($"Updater not found at {updaterFullPath}", updaterFullPath);

        String arguments = serviceName is null ? _extractPath : $"{_extractPath} {serviceName}";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            UseShellExecute = true,
            FileName = updaterFullPath,
            Arguments = arguments,
        });
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
