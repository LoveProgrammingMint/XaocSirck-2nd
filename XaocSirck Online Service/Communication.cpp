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

String Communication::UpdateVersion(const Api::Pack& pack)
{
    return Utf8ToWide(ReadResponseBody(pack));
}

std::vector<Byte> Communication::UpdateDownload(const Api::Pack& pack)
{
    return ReadResponseBody(pack);
}

Byte Communication::SendRequest(const Api::Pack& pack)
{
    std::vector<Byte> response = ReadResponseBody(pack);
    return response.empty() ? static_cast<Byte>(2) : response[0];
}

Boolean Communication::Report(const Api::Pack& pack)
{
    std::vector<Byte> response;
    return ReadResponseBody(pack, response);
}

std::vector<CommunityReportEntry> Communication::CommunityGetAll(const Api::Pack& pack)
{
    std::vector<CommunityReportEntry> entries;
    std::vector<Byte> response;
    if (!ReadResponseBody(pack, response))
    {
        return entries;
    }
    try
    {
        nlohmann::json json = nlohmann::json::parse(response);
        for (const auto& item : json)
        {
            CommunityReportEntry entry;
            entry.Sha256 = Utf8ToWide(item.value("sha256", std::string()));
            entry.Path = Utf8ToWide(item.value("path", std::string()));
            entry.CreatedAt = Utf8ToWide(item.value("created_at", std::string()));
            entries.push_back(std::move(entry));
        }
    }
    catch (...)
    {
    }
    return entries;
}

Boolean Communication::Annotation(const Api::Pack& pack)
{
    std::vector<Byte> response;
    return ReadResponseBody(pack, response);
}

String Communication::CommunityGetAllJson(const Api::Pack& pack)
{
    std::vector<Byte> response;
    if (!ReadResponseBody(pack, response))
    {
        return {};
    }
    return Utf8ToWide(response);
}

std::vector<Byte> Communication::ReadResponseBody(const Api::Pack& pack)
{
    std::vector<Byte> body;
    ReadResponseBody(pack, body);
    return body;
}

Boolean Communication::ReadResponseBody(const Api::Pack& pack, std::vector<Byte>& outBody)
{
    outBody.clear();
    if (_serverHost.empty())
    {
        return false;
    }

    EnsureConnect();
    if (!_connect)
    {
        return false;
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
        return false;
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
        return false;
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
        return false;
    }

    std::vector<Byte> response;
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
        response.insert(response.end(), reinterpret_cast<Byte*>(buffer.data()), reinterpret_cast<Byte*>(buffer.data() + read));
    }

    outBody = std::move(response);
    return true;
}

String Communication::Utf8ToWide(const std::vector<Byte>& value)
{
    if (value.empty())
    {
        return {};
    }
    Int32 size = MultiByteToWideChar(CP_UTF8, 0, reinterpret_cast<const char*>(value.data()), static_cast<Int32>(value.size()), nullptr, 0);
    if (size <= 0)
    {
        return {};
    }
    String result(static_cast<size_t>(size), L'\0');
    MultiByteToWideChar(CP_UTF8, 0, reinterpret_cast<const char*>(value.data()), static_cast<Int32>(value.size()), result.data(), size);
    return result;
}

struct XsCommunication
{
    Communication Instance;
};

static Api::Pack ToCppPack(const XsApiPack* pack)
{
    Api::Pack result;
    result.Router = pack && pack->Router ? pack->Router : L"";
    result.Method = pack && pack->Method == 1 ? Api::HttpMethod::Post : Api::HttpMethod::Get;
    result.Query = pack && pack->Query ? pack->Query : L"";
    result.Body = pack && pack->Body ? pack->Body : "";
    return result;
}

extern "C" XsCommunication* XsCommunication_Create()
{
    try
    {
        return new XsCommunication{Communication()};
    }
    catch (...)
    {
        return nullptr;
    }
}

extern "C" void XsCommunication_Destroy(XsCommunication* instance)
{
    delete instance;
}

extern "C" void XsCommunication_SetServerAddress(XsCommunication* instance, const wchar_t* address)
{
    try
    {
        if (instance != nullptr && address != nullptr)
        {
            instance->Instance.SetServerAddress(address);
        }
    }
    catch (...)
    {
    }
}

extern "C" uint8_t XsCommunication_SignatureQuery(XsCommunication* instance, const XsApiPack* pack)
{
    try
    {
        if (instance == nullptr)
        {
            return 2;
        }
        return instance->Instance.SignatureQuery(ToCppPack(pack));
    }
    catch (...)
    {
        return 2;
    }
}

extern "C" uint8_t XsCommunication_CacheQuery(XsCommunication* instance, const XsApiPack* pack)
{
    try
    {
        if (instance == nullptr)
        {
            return 2;
        }
        return instance->Instance.CacheQuery(ToCppPack(pack));
    }
    catch (...)
    {
        return 2;
    }
}

extern "C" wchar_t* XsCommunication_UpdateVersion(XsCommunication* instance, const XsApiPack* pack)
{
    try
    {
        if (instance == nullptr)
        {
            return nullptr;
        }
        String version = instance->Instance.UpdateVersion(ToCppPack(pack));
        if (version.empty())
        {
            return nullptr;
        }
        wchar_t* result = new wchar_t[version.length() + 1];
        std::copy(version.begin(), version.end(), result);
        result[version.length()] = L'\0';
        return result;
    }
    catch (...)
    {
        return nullptr;
    }
}

extern "C" void XsCommunication_FreeString(wchar_t* str)
{
    delete[] str;
}

extern "C" uint8_t* XsCommunication_UpdateDownload(XsCommunication* instance, const XsApiPack* pack, uint64_t* outLength)
{
    try
    {
        if (instance == nullptr)
        {
            if (outLength != nullptr)
            {
                *outLength = 0;
            }
            return nullptr;
        }
        std::vector<Byte> data = instance->Instance.UpdateDownload(ToCppPack(pack));
        if (outLength != nullptr)
        {
            *outLength = static_cast<uint64_t>(data.size());
        }
        if (data.empty())
        {
            return nullptr;
        }
        uint8_t* result = new uint8_t[data.size()];
        std::copy(data.begin(), data.end(), result);
        return result;
    }
    catch (...)
    {
        if (outLength != nullptr)
        {
            *outLength = 0;
        }
        return nullptr;
    }
}

extern "C" void XsCommunication_FreeBuffer(uint8_t* buffer)
{
    delete[] buffer;
}

extern "C" uint8_t XsCommunication_Report(XsCommunication* instance, const XsApiPack* pack)
{
    try
    {
        if (instance == nullptr)
        {
            return 0;
        }
        return instance->Instance.Report(ToCppPack(pack)) ? 1 : 0;
    }
    catch (...)
    {
        return 0;
    }
}

extern "C" wchar_t* XsCommunication_CommunityGetAll(XsCommunication* instance, const XsApiPack* pack)
{
    try
    {
        if (instance == nullptr)
        {
            return nullptr;
        }
        String json = instance->Instance.CommunityGetAllJson(ToCppPack(pack));
        if (json.empty())
        {
            return nullptr;
        }
        wchar_t* result = new wchar_t[json.length() + 1];
        std::copy(json.begin(), json.end(), result);
        result[json.length()] = L'\0';
        return result;
    }
    catch (...)
    {
        return nullptr;
    }
}

extern "C" uint8_t XsCommunication_Annotation(XsCommunication* instance, const XsApiPack* pack)
{
    try
    {
        if (instance == nullptr)
        {
            return 0;
        }
        return instance->Instance.Annotation(ToCppPack(pack)) ? 1 : 0;
    }
    catch (...)
    {
        return 0;
    }
}
