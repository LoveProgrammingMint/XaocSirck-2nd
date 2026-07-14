#pragma once

#include "Api.hpp"
#include "Base.hpp"
#include <winhttp.h>

class Communication
{
public:
    Communication();
    ~Communication();

    void SetServerAddress(const String& address);
    Byte SignatureQuery(const Api::Pack& pack);
    Byte CacheQuery(const Api::Pack& pack);
    String UpdateVersion(const Api::Pack& pack);
    std::vector<Byte> UpdateDownload(const Api::Pack& pack);

private:
    class WinHttpHandle
    {
    public:
        WinHttpHandle() noexcept;
        explicit WinHttpHandle(HINTERNET handle) noexcept;
        ~WinHttpHandle();
        WinHttpHandle(WinHttpHandle&& other) noexcept;
        WinHttpHandle& operator=(WinHttpHandle&& other) noexcept;
        WinHttpHandle(const WinHttpHandle&) = delete;
        WinHttpHandle& operator=(const WinHttpHandle&) = delete;
        HINTERNET Get() const noexcept;
        explicit operator bool() const noexcept;

    private:
        HINTERNET _handle;
    };

    WinHttpHandle _session;
    WinHttpHandle _connect;
    String _serverHost;
    INTERNET_PORT _serverPort;
    Boolean _useHttps;

    Boolean ParseServerAddress(const String& address);
    void EnsureSession();
    void EnsureConnect();
    std::vector<Byte> ReadResponseBody(const Api::Pack& pack);
    Byte SendRequest(const Api::Pack& pack);
    static String Utf8ToWide(const std::vector<Byte>& value);
};

extern "C"
{
    typedef struct XsCommunication XsCommunication;

    XAOCSIRCKONLINE_API XsCommunication* XsCommunication_Create();
    XAOCSIRCKONLINE_API void XsCommunication_Destroy(XsCommunication* instance);
    XAOCSIRCKONLINE_API void XsCommunication_SetServerAddress(XsCommunication* instance, const wchar_t* address);
    XAOCSIRCKONLINE_API uint8_t XsCommunication_SignatureQuery(XsCommunication* instance, const XsApiPack* pack);
    XAOCSIRCKONLINE_API uint8_t XsCommunication_CacheQuery(XsCommunication* instance, const XsApiPack* pack);
    XAOCSIRCKONLINE_API wchar_t* XsCommunication_UpdateVersion(XsCommunication* instance, const XsApiPack* pack);
    XAOCSIRCKONLINE_API void XsCommunication_FreeString(wchar_t* str);
    XAOCSIRCKONLINE_API uint8_t* XsCommunication_UpdateDownload(XsCommunication* instance, const XsApiPack* pack, uint64_t* outLength);
    XAOCSIRCKONLINE_API void XsCommunication_FreeBuffer(uint8_t* buffer);
}
