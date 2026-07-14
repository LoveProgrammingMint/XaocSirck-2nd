#pragma once

#include <windows.h>
#include <cstdint>
#include <span>
#include <string>
#include <vector>

#ifdef XAOCSIRCKONLINESERVICE_EXPORTS
#define XAOCSIRCKONLINE_API __declspec(dllexport)
#else
#define XAOCSIRCKONLINE_API __declspec(dllimport)
#endif

using Boolean = bool;
using Int32 = int32_t;
using Int64 = int64_t;
using Single = float;
using Byte = uint8_t;
using String = std::wstring;

#ifdef __cplusplus
extern "C" {
#endif

typedef struct
{
    const wchar_t* Router;
    Int32 Method;
    const wchar_t* Query;
    const char* Body;
} XsApiPack;

#ifdef __cplusplus
}
#endif
