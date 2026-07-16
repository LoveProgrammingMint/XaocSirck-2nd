#pragma once

#include "Base.hpp"
#include <nlohmann/json.hpp>
#include <span>
#include <string>

class Api
{
public:
    enum class HttpMethod
    {
        Get,
        Post
    };

    struct Pack
    {
        String Router;
        HttpMethod Method;
        String Query;
        std::string Body;
    };

    static Pack CacheQueryPack(std::span<const Byte> sha256);
    static Pack SignatureQueryPack(std::span<const Byte> signature);
    static Pack UpdateVersionPack();
    static Pack UpdateDownloadPack();
    static Pack ReportPack(std::span<const Byte> sha256, const String& path);
    static Pack CommunityGetAllPack();
    static Pack AnnotationPack(std::span<const Byte> sha256, Int32 label);

private:
    static std::string ToHexString(std::span<const Byte> data);
    static Pack BuildGetPack(const String& router, const String& queryKey, const std::string& value);
    static Pack BuildPostPack(const String& router, const std::string& key, const std::string& value);
};

extern "C"
{
    XAOCSIRCKONLINE_API XsApiPack* XsApi_CacheQueryPack(const uint8_t* data, uint64_t length);
    XAOCSIRCKONLINE_API XsApiPack* XsApi_SignatureQueryPack(const uint8_t* data, uint64_t length);
    XAOCSIRCKONLINE_API XsApiPack* XsApi_UpdateVersionPack();
    XAOCSIRCKONLINE_API XsApiPack* XsApi_UpdateDownloadPack();
    XAOCSIRCKONLINE_API XsApiPack* XsApi_ReportPack(const uint8_t* sha256, uint64_t length, const wchar_t* path);
    XAOCSIRCKONLINE_API XsApiPack* XsApi_CommunityGetAllPack();
    XAOCSIRCKONLINE_API XsApiPack* XsApi_AnnotationPack(const uint8_t* sha256, uint64_t length, Int32 label);
    XAOCSIRCKONLINE_API void XsApi_FreePack(XsApiPack* pack);
}
