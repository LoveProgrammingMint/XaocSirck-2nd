using System;
using System.Runtime.InteropServices;

namespace XaocSirck_Core.Native;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XsApiPack
{
    public Char* Router;
    public Int32 Method;
    public Char* Query;
    public Byte* Body;
}

internal struct XsCommunication
{
}

internal static unsafe partial class OnlineService
{
    private const String DllName = "XaocSirck Online Service.dll";

    [LibraryImport(DllName)]
    internal static partial XsApiPack* XsApi_CacheQueryPack(Byte* data, UInt64 length);

    [LibraryImport(DllName)]
    internal static partial XsApiPack* XsApi_SignatureQueryPack(Byte* data, UInt64 length);

    [LibraryImport(DllName)]
    internal static partial XsApiPack* XsApi_UpdateVersionPack();

    [LibraryImport(DllName)]
    internal static partial XsApiPack* XsApi_UpdateDownloadPack();

    [LibraryImport(DllName)]
    internal static partial void XsApi_FreePack(XsApiPack* pack);

    [LibraryImport(DllName)]
    internal static partial XsCommunication* XsCommunication_Create();

    [LibraryImport(DllName)]
    internal static partial void XsCommunication_Destroy(XsCommunication* instance);

    [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial void XsCommunication_SetServerAddress(XsCommunication* instance, String address);

    [LibraryImport(DllName)]
    internal static partial Byte XsCommunication_SignatureQuery(XsCommunication* instance, XsApiPack* pack);

    [LibraryImport(DllName)]
    internal static partial Byte XsCommunication_CacheQuery(XsCommunication* instance, XsApiPack* pack);

    [LibraryImport(DllName)]
    internal static partial Char* XsCommunication_UpdateVersion(XsCommunication* instance, XsApiPack* pack);

    [LibraryImport(DllName)]
    internal static partial void XsCommunication_FreeString(Char* str);

    [LibraryImport(DllName)]
    internal static partial Byte* XsCommunication_UpdateDownload(XsCommunication* instance, XsApiPack* pack, UInt64* outLength);

    [LibraryImport(DllName)]
    internal static partial void XsCommunication_FreeBuffer(Byte* buffer);
}
