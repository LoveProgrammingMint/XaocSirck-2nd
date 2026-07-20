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
    internal static partial void XaocSirckSessionManagementLoadDirectory(IntPtr handle, String directory);

    [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial void XaocSirckSessionManagementLoadModel(IntPtr handle, String name, String path);

    [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial Int32 XaocSirckSessionManagementHasModel(IntPtr handle, String name);

    [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial IntPtr XaocSirckSessionManagementGet(IntPtr handle, String modelName);

    [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial void XaocSirckSessionManagementSwitchDevice(IntPtr handle, String deviceName);

    [LibraryImport(DllName)]
    internal static partial Int32 XaocSirckSessionManagementGetInputName(IntPtr session, out Byte* outName);

    [LibraryImport(DllName)]
    internal static partial Int32 XaocSirckSessionManagementGetOutputName(IntPtr session, out Byte* outName);

    [LibraryImport(DllName)]
    internal static partial void XaocSirckSessionManagementFreeName(Byte* name);

    [LibraryImport(DllName)]
    internal static partial Int64* XaocSirckSessionManagementGetInputShape(IntPtr session, out Int64 outRank);

    [LibraryImport(DllName)]
    internal static partial Int64* XaocSirckSessionManagementGetOutputShape(IntPtr session, out Int64 outRank);

    [LibraryImport(DllName)]
    internal static partial void XaocSirckSessionManagementFreeShape(Int64* shape);

    [LibraryImport(DllName)]
    internal static partial IntPtr XaocSirckSessionInferenceCreate();

    [LibraryImport(DllName)]
    internal static partial void XaocSirckSessionInferenceDestroy(IntPtr handle);

    [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial IntPtr XaocSirckSessionInferencePacking(IntPtr handle, Single* data, Int64* shape, Int64 shapeLength, String deviceName, Int32 deviceId);

    [LibraryImport(DllName)]
    internal static partial IntPtr XaocSirckSessionInferenceInference(IntPtr handle, IntPtr session, IntPtr tensor);

    [LibraryImport(DllName)]
    internal static partial void XaocSirckSessionInferenceFreeTensor(IntPtr handle);

    [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void XaocSirckSessionInferenceSetInput(IntPtr handle, String name);

    [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void XaocSirckSessionInferenceSetOutput(IntPtr handle, String name);

    [LibraryImport(DllName)]
    internal static partial Single* XaocSirckSessionInferenceGetOutputData(IntPtr tensor, out Int64 outLength);

    [LibraryImport(DllName)]
    internal static partial Int64* XaocSirckSessionInferenceGetOutputShape(IntPtr tensor, out Int64 outRank);

    [LibraryImport(DllName)]
    internal static partial void XaocSirckSessionInferenceFreeShape(Int64* shape);

    [LibraryImport(DllName)]
    internal static partial void XaocSirckAlgorithmSoftmax(Single* input, Int64 length, Single* output);
}
