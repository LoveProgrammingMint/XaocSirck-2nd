#include "Api.hpp"
#include <stdexcept>

std::string Api::ToHexString(std::span<const Byte> data)
{
    static constexpr char hexDigits[] = "0123456789abcdef";
    std::string result;
    result.reserve(data.size() * 2);
    for (Byte value : data)
    {
        result.push_back(hexDigits[value >> 4]);
        result.push_back(hexDigits[value & 0x0F]);
    }
    return result;
}

Api::Pack Api::BuildGetPack(const String& router, const String& queryKey, const std::string& value)
{
    Pack pack;
    pack.Router = router;
    pack.Method = HttpMethod::Get;
    pack.Query = queryKey + L"=";
    for (char c : value)
    {
        pack.Query += static_cast<wchar_t>(static_cast<unsigned char>(c));
    }
    return pack;
}

Api::Pack Api::BuildPostPack(const String& router, const std::string& key, const std::string& value)
{
    Pack pack;
    pack.Router = router;
    pack.Method = HttpMethod::Post;
    nlohmann::json json;
    json[key] = value;
    pack.Body = json.dump();
    return pack;
}

Api::Pack Api::CacheQueryPack(std::span<const Byte> sha256)
{
    if (sha256.size() != 32)
    {
        throw std::invalid_argument("SHA256 must be exactly 32 bytes");
    }
    return BuildGetPack(L"/api/cache/query", L"sha256", ToHexString(sha256));
}

Api::Pack Api::SignatureQueryPack(std::span<const Byte> signature)
{
    if (signature.empty())
    {
        throw std::invalid_argument("Signature must not be empty");
    }
    return BuildPostPack(L"/api/signature/query", "signature", ToHexString(signature));
}

Api::Pack Api::UpdateVersionPack()
{
    Pack pack;
    pack.Router = L"/api/update/version";
    pack.Method = HttpMethod::Get;
    return pack;
}

Api::Pack Api::UpdateDownloadPack()
{
    Pack pack;
    pack.Router = L"/api/update/download";
    pack.Method = HttpMethod::Get;
    return pack;
}
