#pragma once

#include "Api.hpp"
#include "Base.hpp"
#include <winhttp.h>

class XAOCSIRCKONLINE_API Communication
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
