using System.Runtime.InteropServices;
using XaocSirck_Core.Native;

namespace XaocSirck_Core.Cloud;

public sealed unsafe class UpdateClient : IDisposable
{
    private String _packagePath;
    private String _extractPath;
    private String _updaterPath;
    private XsCommunication* _instance;
    private String _serverAddress = String.Empty;
    private Boolean _disposed;

    public Boolean IsConnected => _instance != null && _serverAddress.Length > 0;

    public String PackagePath
    {
        get => _packagePath;
        set => _packagePath = value;
    }

    public String ExtractPath
    {
        get => _extractPath;
        set => _extractPath = value;
    }

    public String UpdaterPath
    {
        get => _updaterPath;
        set => _updaterPath = value;
    }

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

    public void Download() => Download(_packagePath);

    public void Download(String outputPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(UpdateClient));
        if (_instance == null)
            throw new InvalidOperationException("Not connected.");
        ArgumentNullException.ThrowIfNull(outputPath);

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

            String? packageDir = Path.GetDirectoryName(outputPath);
            if (!String.IsNullOrEmpty(packageDir))
                Directory.CreateDirectory(packageDir);

            using FileStream stream = new(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
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
        ObjectDisposedException.ThrowIf(_disposed, nameof(UpdateClient));
        if (!File.Exists(_packagePath))
            throw new FileNotFoundException($"Update package not found at {_packagePath}", _packagePath);

        App.Logger.Info($"Applying update package: {_packagePath}");

        if (Directory.Exists(_extractPath))
            Directory.Delete(_extractPath, true);
        Directory.CreateDirectory(_extractPath);

        System.IO.Compression.ZipFile.ExtractToDirectory(_packagePath, _extractPath, overwriteFiles: true);

        String listFile = Path.Combine(_extractPath, "update_list.updatelist");
        if (!File.Exists(listFile))
            throw new FileNotFoundException($"Update list not found at {listFile}", listFile);

        String updaterFullPath = Path.GetFullPath(_updaterPath);
        if (!File.Exists(updaterFullPath))
            throw new FileNotFoundException($"Updater not found at {updaterFullPath}", updaterFullPath);

        String arguments = serviceName is null ? _extractPath : $"{_extractPath} {serviceName}";
        String? updaterDir = Path.GetDirectoryName(updaterFullPath);

        using System.Diagnostics.Process process = new();
        process.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = updaterFullPath,
            Arguments = arguments,
            WorkingDirectory = updaterDir ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (!process.Start())
            throw new InvalidOperationException("Failed to start Updater process.");

        App.Logger.Info($"Updater started: {updaterFullPath} {arguments}");

        if (process.WaitForExit(5000))
        {
            if (process.ExitCode != 0)
                App.Logger.Warning($"Updater exited with code {process.ExitCode}");
            else
                App.Logger.Info("Updater completed");
        }
        else
        {
            App.Logger.Info("Updater is still running; service should exit to allow update completion");
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
