using System;
using System.Runtime.InteropServices;

namespace XaocSirck_Core.Native;

internal static unsafe partial class InferenceService
{
    private const String DllName = "XaocSirck Inference Service.dll";

    [LibraryImport(DllName)]
    internal static partial IntPtr XaocSirckSessionManagementCreate();

    [LibraryImport(DllName)]
    internal static partial void XaocSirckSessionManagementDestroy(IntPtr handle);

    [LibraryImport(DllName)]
    internal static partial void XaocSirckSessionManagementLoad(IntPtr handle);

    [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial IntPtr XaocSirckSessionManagementGet(IntPtr handle, String modelName);

    [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial void XaocSirckSessionManagementSwitchDevice(IntPtr handle, String deviceName);

    [LibraryImport(DllName)]
    internal static partial IntPtr XaocSirckSessionInferenceCreate();

    [LibraryImport(DllName)]
    internal static partial void XaocSirckSessionInferenceDestroy(IntPtr handle);

    [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial IntPtr XaocSirckSessionInferencePacking(IntPtr handle, Single* data, Int64* shape, Int64 shapeLength, String deviceName, Int32 deviceId);

    [LibraryImport(DllName)]
    internal static partial IntPtr XaocSirckSessionInferenceInference(IntPtr handle, IntPtr session, IntPtr tensor);

    [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void XaocSirckSessionInferenceSetInput(IntPtr handle, String name);

    [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void XaocSirckSessionInferenceSetOutput(IntPtr handle, String name);

    [LibraryImport(DllName)]
    internal static partial void XaocSirckAlgorithmSoftmax(Single* input, Int64 length, Single* output);
}
