using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using XaocSirck_Core.Engine;
using XaocSirck_Core.Interface.Engine;
using Mode_Engines = XaocSirck_Core.Interface.Engine._Mode_Engines;
using Mode_Bitremal = XaocSirck_Core.Interface.Engine._Mode_Bitremal;
using Mode_Zeroflows = XaocSirck_Core.Interface.Engine._Mode_Zeroflows;
using Mode_Signature = XaocSirck_Core.Interface.Engine._Mode_Signature;
using Mode_Archive = XaocSirck_Core.Interface.Engine._Mode_Archive;
using Mode_Documentation = XaocSirck_Core.Interface.Engine._Mode_Documentation;
using Mode_Shell = XaocSirck_Core.Interface.Engine._Mode_Shell;
using Mode_Charwolf = XaocSirck_Core.Interface.Engine._Mode_Charwolf;

namespace XaocSirck_Export;

internal sealed class EngineHandle : IDisposable
{
    private static Int64 _nextId = 1;
    private readonly Int64 _id;
    private readonly Engine _engine;
    private readonly List<ScanResult> _results = new();
    private readonly Lock _lock = new();
    private Boolean _disposed;

    public EngineHandle()
    {
        _id = Interlocked.Increment(ref _nextId);
        _engine = new Engine();
    }

    public IntPtr Id => (IntPtr)_id;
    public Engine Engine => _engine;

    public IReadOnlyList<ScanResult> Results
    {
        get
        {
            lock (_lock)
                return _results.ToList();
        }
    }

    public void SetResults(IEnumerable<ScanResult> results)
    {
        lock (_lock)
        {
            _results.Clear();
            _results.AddRange(results);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _engine.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

public static class EngineExport
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(EngineExport))]
    static EngineExport() { }

    private static readonly ConcurrentDictionary<IntPtr, EngineHandle> _handles = new();
    private static readonly Lock _errorLock = new();
    private static String _lastError = String.Empty;

    private static void SetError(String message)
    {
        lock (_errorLock)
            _lastError = message;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)], EntryPoint = "XsExport_GetLastError")]
    public static unsafe Int32 XsExport_GetLastError(Char* buffer, Int32 bufferSize)
    {
        if (buffer == null || bufferSize <= 0)
            return -1;
        String error;
        lock (_errorLock)
            error = _lastError;
        Byte[] bytes = Encoding.Unicode.GetBytes(error);
        Int32 copyLength = Math.Min(bytes.Length, (bufferSize - 1) * 2);
        fixed (Byte* src = bytes)
            Buffer.MemoryCopy(src, buffer, copyLength, copyLength);
        buffer[copyLength / 2] = '\0';
        return copyLength / 2;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)], EntryPoint = "XsEngine_Create")]
    public static unsafe Int32 XsEngine_Create(IntPtr* handle)
    {
        SetError(String.Empty);
        if (handle == null)
        {
            SetError("Handle pointer is null.");
            return 1;
        }
        try
        {
            EngineHandle engineHandle = new();
            _handles[engineHandle.Id] = engineHandle;
            *handle = engineHandle.Id;
            return 0;
        }
        catch (Exception ex)
        {
            SetError($"Engine creation failed: {ex.Message}");
            return 2;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)], EntryPoint = "XsEngine_Free")]
    public static Int32 XsEngine_Free(IntPtr handle)
    {
        SetError(String.Empty);
        if (!_handles.TryRemove(handle, out EngineHandle? engineHandle))
        {
            SetError("Invalid engine handle.");
            return 1;
        }
        engineHandle.Dispose();
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)], EntryPoint = "XsEngine_Initialize")]
    public static Int32 XsEngine_Initialize(IntPtr handle)
    {
        SetError(String.Empty);
        if (!_handles.TryGetValue(handle, out EngineHandle? engineHandle))
        {
            SetError("Invalid engine handle.");
            return 1;
        }
        try
        {
            engineHandle.Engine.Initialize();
            return 0;
        }
        catch (Exception ex)
        {
            SetError($"Engine initialization failed: {ex.Message}");
            return 2;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)], EntryPoint = "XsEngine_LoadSettings")]
    public static unsafe Int32 XsEngine_LoadSettings(IntPtr handle, Char* jsonPath)
    {
        SetError(String.Empty);
        if (!_handles.TryGetValue(handle, out EngineHandle? engineHandle))
        {
            SetError("Invalid engine handle.");
            return 1;
        }
        if (jsonPath == null)
        {
            SetError("JSON path is null.");
            return 2;
        }
        try
        {
            String path = new String(jsonPath);
            engineHandle.Engine.Settings.Load(path);
            return 0;
        }
        catch (Exception ex)
        {
            SetError($"Settings load failed: {ex.Message}");
            return 3;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)], EntryPoint = "XsEngine_Scan")]
    public static Int32 XsEngine_Scan(IntPtr handle, IntPtr path)
    {
        SetError(String.Empty);
        if (!_handles.TryGetValue(handle, out EngineHandle? engineHandle))
        {
            SetError("Invalid engine handle.");
            return 1;
        }
        String? scanPath = Marshal.PtrToStringUni(path);
        if (String.IsNullOrEmpty(scanPath))
        {
            SetError("Scan path is null or empty.");
            return 2;
        }
        try
        {
            ScanResult[] results = engineHandle.Engine.Scan(scanPath, null, engineHandle.Engine.Settings.MaxFiles);
            engineHandle.SetResults(results);
            return 0;
        }
        catch (Exception ex)
        {
            SetError($"Scan failed: {ex.Message}");
            return 3;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)], EntryPoint = "XsEngine_ScanWithMode")]
    public static unsafe Int32 XsEngine_ScanWithMode(IntPtr handle, IntPtr path, UInt32 modeFlags)
    {
        SetError(String.Empty);
        if (!_handles.TryGetValue(handle, out EngineHandle? engineHandle))
        {
            SetError("Invalid engine handle.");
            return 1;
        }
        String? scanPath = Marshal.PtrToStringUni(path);
        if (String.IsNullOrEmpty(scanPath))
        {
            SetError("Scan path is null or empty.");
            return 2;
        }
        try
        {
            EngineMode mode = ParseModeFlags(modeFlags);
            ScanResult[] results = engineHandle.Engine.Scan(scanPath, mode, engineHandle.Engine.Settings.MaxFiles);
            engineHandle.SetResults(results);
            return 0;
        }
        catch (Exception ex)
        {
            SetError($"Scan failed: {ex.Message}");
            return 3;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)], EntryPoint = "XsEngine_GetResultCount")]
    public static unsafe Int32 XsEngine_GetResultCount(IntPtr handle, Int32* count)
    {
        SetError(String.Empty);
        if (count == null)
        {
            SetError("Count pointer is null.");
            return 1;
        }
        if (!_handles.TryGetValue(handle, out EngineHandle? engineHandle))
        {
            SetError("Invalid engine handle.");
            return 2;
        }
        *count = engineHandle.Results.Count;
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)], EntryPoint = "XsEngine_GetResult")]
    public static unsafe Int32 XsEngine_GetResult(IntPtr handle, Int32 index, XsScanResult* result)
    {
        SetError(String.Empty);
        if (result == null)
        {
            SetError("Result pointer is null.");
            return 1;
        }
        if (!_handles.TryGetValue(handle, out EngineHandle? engineHandle))
        {
            SetError("Invalid engine handle.");
            return 2;
        }
        IReadOnlyList<ScanResult> results = engineHandle.Results;
        if (index < 0 || index >= results.Count)
        {
            SetError("Result index out of range.");
            return 3;
        }
        ScanResult source = results[index];
        result->FilePath = Marshal.StringToCoTaskMemUni(source.FilePath);
        result->IsMalicious = source.IsMalicious ? (Byte)1 : (Byte)0;
        result->BitremalScore = source.BitremalProbabilities != null && source.BitremalProbabilities.Length > 1 ? source.BitremalProbabilities[1] : 0.0f;
        result->ZeroflowsScore = source.ZeroflowsProbabilities != null && source.ZeroflowsProbabilities.Length > 1 ? source.ZeroflowsProbabilities[1] : 0.0f;
        result->IsSigned = source.SignatureResult?.IsSigned ?? false ? (Byte)1 : (Byte)0;
        result->IsTrusted = source.SignatureResult?.IsTrusted ?? false ? (Byte)1 : (Byte)0;
        result->ShellDetected = source.ShellResult?.Hit != ShellHits.Emtpy ? (Byte)1 : (Byte)0;
        result->ArchiveSuspicious = source.ArchiveResult?.SuspiciousEntryCount ?? 0;
        result->DocumentHasMacro = source.DocumentationResult?.HasMacro ?? false ? (Byte)1 : (Byte)0;
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)], EntryPoint = "XsEngine_FreeString")]
    public static void XsEngine_FreeString(IntPtr ptr)
    {
        if (ptr != IntPtr.Zero)
            Marshal.FreeCoTaskMem(ptr);
    }

    private static EngineMode ParseModeFlags(UInt32 flags)
    {
        EngineMode mode = new();
        mode.Bitremal = (Mode_Bitremal)((flags >> 0) & 0xF);
        mode.Zeroflow = (Mode_Zeroflows)((flags >> 4) & 0xF);
        mode.Signature = (Mode_Signature)((flags >> 8) & 0xF);
        mode.Archive = (Mode_Archive)((flags >> 12) & 0xF);
        mode.Documentation = (Mode_Documentation)((flags >> 16) & 0xF);
        mode.Shell = (Mode_Shell)((flags >> 20) & 0xF);
        mode.Charwolf = (Mode_Charwolf)((flags >> 24) & 0xF);
        mode.Engines = new Mode_Engines
        {
            Signature = mode.Signature,
            Archive = mode.Archive,
            Documentation = mode.Documentation,
            Shell = mode.Shell
        };
        return mode;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct XsScanResult
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
