using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DevConsole;

internal sealed unsafe class NativeEngine : IDisposable
{
    private readonly IntPtr _module;
    private readonly delegate* unmanaged[Cdecl]<IntPtr*, Int32> _create;
    private readonly delegate* unmanaged[Cdecl]<IntPtr, Int32> _free;
    private readonly delegate* unmanaged[Cdecl]<IntPtr, Int32> _initialize;
    private readonly delegate* unmanaged[Cdecl]<IntPtr, Char*, Int32> _loadSettings;
    private readonly delegate* unmanaged[Cdecl]<IntPtr, IntPtr, Int32> _scan;
    private readonly delegate* unmanaged[Cdecl]<IntPtr, IntPtr, UInt32, Int32> _scanWithMode;
    private readonly delegate* unmanaged[Cdecl]<IntPtr, Int32*, Int32> _getResultCount;
    private readonly delegate* unmanaged[Cdecl]<IntPtr, Int32, XsScanResult*, Int32> _getResult;
    private readonly delegate* unmanaged[Cdecl]<IntPtr, void> _freeString;
    private readonly delegate* unmanaged[Cdecl]<IntPtr, IntPtr*, Int32> _checkForUpdate;
    private readonly delegate* unmanaged[Cdecl]<IntPtr, IntPtr, Int32> _downloadUpdate;
    private readonly delegate* unmanaged[Cdecl]<IntPtr, IntPtr, Int32> _applyUpdate;
    private readonly delegate* unmanaged[Cdecl]<Char*, Int32, Int32> _getLastError;

    private IntPtr _handle;
    private Boolean _disposed;

    public IntPtr Handle => _handle;

    public NativeEngine(String dllPath)
    {
        _module = LoadLibraryW(dllPath);
        if (_module == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to load {dllPath}");

        _create = (delegate* unmanaged[Cdecl]<IntPtr*, Int32>)GetExport("XsEngine_Create");
        _free = (delegate* unmanaged[Cdecl]<IntPtr, Int32>)GetExport("XsEngine_Free");
        _initialize = (delegate* unmanaged[Cdecl]<IntPtr, Int32>)GetExport("XsEngine_Initialize");
        _loadSettings = (delegate* unmanaged[Cdecl]<IntPtr, Char*, Int32>)GetExport("XsEngine_LoadSettings");
        _scan = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, Int32>)GetExport("XsEngine_Scan");
        _scanWithMode = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, UInt32, Int32>)GetExport("XsEngine_ScanWithMode");
        _getResultCount = (delegate* unmanaged[Cdecl]<IntPtr, Int32*, Int32>)GetExport("XsEngine_GetResultCount");
        _getResult = (delegate* unmanaged[Cdecl]<IntPtr, Int32, XsScanResult*, Int32>)GetExport("XsEngine_GetResult");
        _freeString = (delegate* unmanaged[Cdecl]<IntPtr, void>)GetExport("XsEngine_FreeString");
        _checkForUpdate = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr*, Int32>)GetExport("XsEngine_CheckForUpdate");
        _downloadUpdate = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, Int32>)GetExport("XsEngine_DownloadUpdate");
        _applyUpdate = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, Int32>)GetExport("XsEngine_ApplyUpdate");
        _getLastError = (delegate* unmanaged[Cdecl]<Char*, Int32, Int32>)GetExport("XsExport_GetLastError");
    }

    public void Create()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(NativeEngine));
        IntPtr handle = IntPtr.Zero;
        Int32 rc = _create(&handle);
        if (rc != 0)
            throw new InvalidOperationException($"XsEngine_Create failed: {rc} - {FetchLastError()}");
        _handle = handle;
    }

    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(NativeEngine));
        Int32 rc = _initialize(_handle);
        if (rc != 0)
            throw new InvalidOperationException($"XsEngine_Initialize failed: {rc} - {FetchLastError()}");
    }

    public void LoadSettings(String jsonPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(NativeEngine));
        fixed (Char* p = jsonPath)
        {
            Int32 rc = _loadSettings(_handle, p);
            if (rc != 0)
                throw new InvalidOperationException($"XsEngine_LoadSettings failed: {rc} - {FetchLastError()}");
        }
    }

    public void Scan(String path)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(NativeEngine));
        fixed (Char* p = path)
        {
            Int32 rc = _scan(_handle, (IntPtr)p);
            if (rc != 0)
                throw new InvalidOperationException($"XsEngine_Scan failed: {rc} - {FetchLastError()}");
        }
    }

    public Int32 GetResultCount()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(NativeEngine));
        Int32 count = 0;
        Int32 rc = _getResultCount(_handle, &count);
        if (rc != 0)
            throw new InvalidOperationException($"XsEngine_GetResultCount failed: {rc} - {FetchLastError()}");
        return count;
    }

    public NativeScanResult GetResult(Int32 index)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(NativeEngine));
        XsScanResult raw;
        Int32 rc = _getResult(_handle, index, &raw);
        if (rc != 0)
            throw new InvalidOperationException($"XsEngine_GetResult failed: {rc} - {FetchLastError()}");

        String? filePath = Marshal.PtrToStringUni(raw.FilePath);
        _freeString(raw.FilePath);

        return new NativeScanResult
        {
            FilePath = filePath ?? String.Empty,
            IsMalicious = raw.IsMalicious != 0,
            BitremalScore = raw.BitremalScore,
            ZeroflowsScore = raw.ZeroflowsScore,
            IsSigned = raw.IsSigned != 0,
            IsTrusted = raw.IsTrusted != 0,
            ShellDetected = raw.ShellDetected != 0,
            ArchiveSuspicious = raw.ArchiveSuspicious,
            DocumentHasMacro = raw.DocumentHasMacro != 0
        };
    }

    public String? CheckForUpdate()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(NativeEngine));
        IntPtr versionPtr = IntPtr.Zero;
        Int32 rc = _checkForUpdate(_handle, &versionPtr);
        if (rc != 0)
            throw new InvalidOperationException($"XsEngine_CheckForUpdate failed: {rc} - {FetchLastError()}");

        if (versionPtr == IntPtr.Zero)
            return null;

        String? version = Marshal.PtrToStringUni(versionPtr);
        _freeString(versionPtr);
        return version;
    }

    public void DownloadUpdate(String? outputPath = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(NativeEngine));
        IntPtr pathPtr = outputPath != null ? Marshal.StringToCoTaskMemUni(outputPath) : IntPtr.Zero;
        try
        {
            Int32 rc = _downloadUpdate(_handle, pathPtr);
            if (rc != 0)
                throw new InvalidOperationException($"XsEngine_DownloadUpdate failed: {rc} - {FetchLastError()}");
        }
        finally
        {
            if (pathPtr != IntPtr.Zero)
                Marshal.FreeCoTaskMem(pathPtr);
        }
    }

    public void ApplyUpdate(String? serviceName = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(NativeEngine));
        IntPtr namePtr = serviceName != null ? Marshal.StringToCoTaskMemUni(serviceName) : IntPtr.Zero;
        try
        {
            Int32 rc = _applyUpdate(_handle, namePtr);
            if (rc != 0)
                throw new InvalidOperationException($"XsEngine_ApplyUpdate failed: {rc} - {FetchLastError()}");
        }
        finally
        {
            if (namePtr != IntPtr.Zero)
                Marshal.FreeCoTaskMem(namePtr);
        }
    }

    public String FetchLastError()
    {
        Char* buffer = stackalloc Char[1024];
        Int32 length = _getLastError(buffer, 1024);
        return length > 0 ? new String(buffer, 0, length) : String.Empty;
    }

    private IntPtr GetExport(String name)
    {
        IntPtr ptr = GetProcAddress(_module, name);
        if (ptr == IntPtr.Zero)
            throw new EntryPointNotFoundException($"Export not found: {name}");
        return ptr;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_handle != IntPtr.Zero)
            {
                _free(_handle);
                _handle = IntPtr.Zero;
            }
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryW(String lpFileName);

    [DllImport("kernel32", SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, String lpProcName);
}

[StructLayout(LayoutKind.Sequential)]
internal struct XsScanResult
{
    public IntPtr FilePath;
    public Byte IsMalicious;
    public Single BitremalScore;
    public Single ZeroflowsScore;
    public Byte IsSigned;
    public Byte IsTrusted;
    public Byte ShellDetected;
    public Int32 ArchiveSuspicious;
    public Byte DocumentHasMacro;
}

internal sealed class NativeScanResult
{
    public String FilePath { get; set; } = String.Empty;
    public Boolean IsMalicious { get; set; }
    public Single BitremalScore { get; set; }
    public Single ZeroflowsScore { get; set; }
    public Boolean IsSigned { get; set; }
    public Boolean IsTrusted { get; set; }
    public Boolean ShellDetected { get; set; }
    public Int32 ArchiveSuspicious { get; set; }
    public Boolean DocumentHasMacro { get; set; }
}
