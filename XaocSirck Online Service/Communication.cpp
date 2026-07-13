#include "Communication.hpp"
#include <algorithm>
#include <stdexcept>
#include <vector>

Communication::WinHttpHandle::WinHttpHandle() noexcept
    : _handle(nullptr)
{
}

Communication::WinHttpHandle::WinHttpHandle(HINTERNET handle) noexcept
    : _handle(handle)
{
}

Communication::WinHttpHandle::~WinHttpHandle()
{
    if (_handle != nullptr)
    {
        WinHttpCloseHandle(_handle);
    }
}

Communication::WinHttpHandle::WinHttpHandle(WinHttpHandle&& other) noexcept
    : _handle(other._handle)
{
    other._handle = nullptr;
}

Communication::WinHttpHandle& Communication::WinHttpHandle::operator=(WinHttpHandle&& other) noexcept
{
    if (this != &other)
    {
        if (_handle != nullptr)
        {
            WinHttpCloseHandle(_handle);
        }
        _handle = other._handle;
        other._handle = nullptr;
    }
    return *this;
}

HINTERNET Communication::WinHttpHandle::Get() const noexcept
{
    return _handle;
}

Communication::WinHttpHandle::operator bool() const noexcept
{
    return _handle != nullptr;
}

Communication::Communication()
    : _session(),
      _connect(),
      _serverHost(),
      _serverPort(80),
      _useHttps(false)
{
}

Communication::~Communication()
{
}

Boolean Communication::ParseServerAddress(const String& address)
{
    URL_COMPONENTS components = {};
    components.dwStructSize = sizeof(components);
    components.dwSchemeLength = static_cast<DWORD>(-1);
    components.dwHostNameLength = static_cast<DWORD>(-1);
    components.dwUserNameLength = static_cast<DWORD>(-1);
    components.dwPasswordLength = static_cast<DWORD>(-1);
    components.dwUrlPathLength = static_cast<DWORD>(-1);
    components.dwExtraInfoLength = static_cast<DWORD>(-1);

    if (!WinHttpCrackUrl(address.c_str(), 0, 0, &components))
    {
        String prefixed = L"http://" + address;
        if (!WinHttpCrackUrl(prefixed.c_str(), 0, 0, &components))
        {
            return false;
        }
    }

    _serverHost.assign(components.lpszHostName, components.dwHostNameLength);
    _serverPort = components.nPort;
    _useHttps = components.nScheme == INTERNET_SCHEME_HTTPS;
    return true;
}

void Communication::EnsureSession()
{
    if (!_session)
    {
        _session = WinHttpHandle(WinHttpOpen(
            L"XaocSirck Online Service/1.0",
            WINHTTP_ACCESS_TYPE_DEFAULT_PROXY,
            WINHTTP_NO_PROXY_NAME,
            WINHTTP_NO_PROXY_BYPASS,
            0));
    }
}

void Communication::EnsureConnect()
{
    if (!_connect)
    {
        EnsureSession();
        _connect = WinHttpHandle(WinHttpConnect(_session.Get(), _serverHost.c_str(), _serverPort, 0));
    }
}

void Communication::SetServerAddress(const String& address)
{
    if (!ParseServerAddress(address))
    {
        throw std::invalid_argument("Invalid server address");
    }
    _connect = WinHttpHandle();
    EnsureConnect();
}

Byte Communication::SignatureQuery(const Api::Pack& pack)
{
    Byte result = SendRequest(pack);
    return result == 0 ? static_cast<Byte>(0) : static_cast<Byte>(2);
}

Byte Communication::CacheQuery(const Api::Pack& pack)
{
    Byte result = SendRequest(pack);
    switch (result)
    {
        case 0: return 0;
        case 1: return 1;
        case 2: return 2;
        case 4: return 4;
        default: return 2;
    }
}

Byte Communication::SendRequest(const Api::Pack& pack)
{
    if (_serverHost.empty())
    {
        return 2;
    }

    EnsureConnect();
    if (!_connect)
    {
        return 2;
    }

    std::wstring verb = pack.Method == Api::HttpMethod::Get ? L"GET" : L"POST";
    std::wstring path = pack.Router;
    if (pack.Method == Api::HttpMethod::Get && !pack.Query.empty())
    {
        path += L"?" + pack.Query;
    }

    WinHttpHandle request(WinHttpOpenRequest(
        _connect.Get(),
        verb.c_str(),
        path.c_str(),
        nullptr,
        WINHTTP_NO_REFERER,
        WINHTTP_DEFAULT_ACCEPT_TYPES,
        _useHttps ? WINHTTP_FLAG_SECURE : 0));
    if (!request)
    {
        return 2;
    }

    std::wstring headers;
    if (pack.Method == Api::HttpMethod::Post)
    {
        headers = L"Content-Type: application/json\r\n";
    }

    const std::string& body = pack.Body;
    BOOL sent = WinHttpSendRequest(
        request.Get(),
        headers.empty() ? WINHTTP_NO_ADDITIONAL_HEADERS : headers.c_str(),
        headers.empty() ? 0 : static_cast<DWORD>(headers.length()),
        pack.Method == Api::HttpMethod::Post ? const_cast<char*>(body.data()) : WINHTTP_NO_REQUEST_DATA,
        pack.Method == Api::HttpMethod::Post ? static_cast<DWORD>(body.length()) : 0,
        pack.Method == Api::HttpMethod::Post ? static_cast<DWORD>(body.length()) : 0,
        0);
    if (!sent || !WinHttpReceiveResponse(request.Get(), nullptr))
    {
        return 2;
    }

    DWORD statusCode = 0;
    DWORD statusCodeSize = sizeof(statusCode);
    if (!WinHttpQueryHeaders(
            request.Get(),
            WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
            WINHTTP_HEADER_NAME_BY_INDEX,
            &statusCode,
            &statusCodeSize,
            WINHTTP_NO_HEADER_INDEX)
        || statusCode != HTTP_STATUS_OK)
    {
        return 2;
    }

    std::string response;
    DWORD available = 0;
    DWORD read = 0;
    std::vector<char> buffer(8192);

    while (WinHttpQueryDataAvailable(request.Get(), &available))
    {
        if (available == 0)
        {
            break;
        }
        DWORD toRead = static_cast<DWORD>(std::min<size_t>(static_cast<size_t>(available), buffer.size()));
        if (!WinHttpReadData(request.Get(), buffer.data(), toRead, &read))
        {
            break;
        }
        response.append(buffer.data(), read);
    }

    return response.empty() ? static_cast<Byte>(2) : static_cast<Byte>(response[0]);
}
