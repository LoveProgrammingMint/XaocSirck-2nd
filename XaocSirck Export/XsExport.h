#pragma once

#ifdef __cplusplus
extern "C" {
#endif

#include <stdint.h>
#include <windows.h>

#define XS_MODE_BITREMAL_DISABLED       0x00000000
#define XS_MODE_BITREMAL_RB             0x00000001
#define XS_MODE_BITREMAL_AL             0x00000002
#define XS_MODE_BITREMAL_OT             0x00000003
#define XS_MODE_BITREMAL_OT_EXIST_RB    0x00000004

#define XS_MODE_ZEROFLOWS_DISABLED      0x00000000
#define XS_MODE_ZEROFLOWS_ZF            0x00000010

#define XS_MODE_SIGNATURE_DISABLED      0x00000000
#define XS_MODE_SIGNATURE_LOOSE         0x00000100
#define XS_MODE_SIGNATURE_STRICT        0x00000200

#define XS_MODE_ARCHIVE_DISABLED        0x00000000
#define XS_MODE_ARCHIVE_CHECK           0x00001000

#define XS_MODE_DOCUMENTATION_DISABLED  0x00000000
#define XS_MODE_DOCUMENTATION_DOCVBA    0x00010000

#define XS_MODE_SHELL_DISABLED          0x00000000
#define XS_MODE_SHELL_BLOCK             0x00100000
#define XS_MODE_SHELL_SUSPICIOUS        0x00200000

#define XS_MODE_CHARWOLF_DISABLED       0x00000000
#define XS_MODE_CHARWOLF_CORE           0x01000000
#define XS_MODE_CHARWOLF_EXTENDED       0x02000000
#define XS_MODE_CHARWOLF_FULLED         0x03000000

typedef struct _XsScanResult
{
    const wchar_t* FilePath;
    uint8_t        IsMalicious;
    float          BitremalScore;
    float          ZeroflowsScore;
    uint8_t        IsSigned;
    uint8_t        IsTrusted;
    uint8_t        ShellDetected;
    int32_t        ArchiveSuspicious;
    uint8_t        DocumentHasMacro;
} XsScanResult;

__declspec(dllimport) int32_t __cdecl XsExport_GetLastError(wchar_t* buffer, int32_t bufferSize);

__declspec(dllimport) int32_t __cdecl XsEngine_Create(intptr_t* handle);
__declspec(dllimport) int32_t __cdecl XsEngine_Free(intptr_t handle);
__declspec(dllimport) int32_t __cdecl XsEngine_Initialize(intptr_t handle);
__declspec(dllimport) int32_t __cdecl XsEngine_LoadSettings(intptr_t handle, const wchar_t* jsonPath);

__declspec(dllimport) int32_t __cdecl XsEngine_Scan(intptr_t handle, const wchar_t* path);
__declspec(dllimport) int32_t __cdecl XsEngine_ScanWithMode(intptr_t handle, const wchar_t* path, uint32_t modeFlags);

__declspec(dllimport) int32_t __cdecl XsEngine_GetResultCount(intptr_t handle, int32_t* count);
__declspec(dllimport) int32_t __cdecl XsEngine_GetResult(intptr_t handle, int32_t index, XsScanResult* result);
__declspec(dllimport) void    __cdecl XsEngine_FreeString(void* ptr);

#ifdef __cplusplus
}
#endif
