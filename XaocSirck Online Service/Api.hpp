#pragma once

#include "Base.hpp"
#include <nlohmann/json.hpp>
#include <span>
#include <string>

class XAOCSIRCKONLINE_API Api
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

private:
    static std::string ToHexString(std::span<const Byte> data);
    static Pack BuildGetPack(const String& router, const String& queryKey, const std::string& value);
    static Pack BuildPostPack(const String& router, const std::string& key, const std::string& value);
};
