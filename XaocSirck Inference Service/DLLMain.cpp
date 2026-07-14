#include "Base.hpp"
#include "SessionManagement.hpp"
#include "SessionInference.hpp"
#include "Algorithm.hpp"
#include <algorithm>
#include <vector>

BOOL APIENTRY DllMain(HMODULE hModule,
                      DWORD  ul_reason_for_call,
                      LPVOID lpReserved
                     )
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

extern "C"
{
    __declspec(dllexport) void* XaocSirckSessionManagementCreate()
    {
        try
        {
            return new SessionManagement();
        }
        catch (...)
        {
            return nullptr;
        }
    }

    __declspec(dllexport) void XaocSirckSessionManagementDestroy(void* handle)
    {
        if (handle != nullptr)
        {
            delete static_cast<SessionManagement*>(handle);
        }
    }

    __declspec(dllexport) void XaocSirckSessionManagementLoad(void* handle)
    {
        if (handle == nullptr)
        {
            return;
        }
        try
        {
            static_cast<SessionManagement*>(handle)->Load();
        }
        catch (...)
        {
        }
    }

    __declspec(dllexport) void* XaocSirckSessionManagementGet(void* handle, const wchar_t* modelName)
    {
        if (handle == nullptr || modelName == nullptr)
        {
            return nullptr;
        }
        try
        {
            return static_cast<SessionManagement*>(handle)->Get(modelName);
        }
        catch (...)
        {
            return nullptr;
        }
    }

    __declspec(dllexport) void XaocSirckSessionManagementSwitchDevice(void* handle, const wchar_t* deviceName)
    {
        if (handle == nullptr || deviceName == nullptr)
        {
            return;
        }
        try
        {
            static_cast<SessionManagement*>(handle)->SwitchDevice(deviceName);
        }
        catch (...)
        {
        }
    }

    __declspec(dllexport) void* XaocSirckSessionInferenceCreate()
    {
        try
        {
            return new SessionInference();
        }
        catch (...)
        {
            return nullptr;
        }
    }

    __declspec(dllexport) void XaocSirckSessionInferenceDestroy(void* handle)
    {
        if (handle != nullptr)
        {
            delete static_cast<SessionInference*>(handle);
        }
    }

    __declspec(dllexport) void* XaocSirckSessionInferencePacking(void* handle, Single* data, const Int64* shape, Int64 shapeLength, const wchar_t* deviceName, Int32 deviceId)
    {
        if (handle == nullptr || data == nullptr || shape == nullptr || deviceName == nullptr)
        {
            return nullptr;
        }
        try
        {
            std::vector<Int64> shapeVector(shape, shape + shapeLength);
            return static_cast<SessionInference*>(handle)->Packing(data, shapeVector, deviceName, deviceId);
        }
        catch (...)
        {
            return nullptr;
        }
    }

    __declspec(dllexport) void* XaocSirckSessionInferenceInference(void* handle, void* session, void* tensor)
    {
        if (handle == nullptr || session == nullptr || tensor == nullptr)
        {
            return nullptr;
        }
        try
        {
            return static_cast<SessionInference*>(handle)->Inference(static_cast<Ort::Session*>(session), static_cast<Ort::Value*>(tensor));
        }
        catch (...)
        {
            return nullptr;
        }
    }

    __declspec(dllexport) void XaocSirckSessionInferenceSetInput(void* handle, const char* name)
    {
        if (handle == nullptr || name == nullptr)
        {
            return;
        }
        try
        {
            static_cast<SessionInference*>(handle)->SetInput(name);
        }
        catch (...)
        {
        }
    }

    __declspec(dllexport) void XaocSirckSessionInferenceSetOutput(void* handle, const char* name)
    {
        if (handle == nullptr || name == nullptr)
        {
            return;
        }
        try
        {
            static_cast<SessionInference*>(handle)->SetOutput(name);
        }
        catch (...)
        {
        }
    }

    __declspec(dllexport) void XaocSirckAlgorithmSoftmax(const Single* input, Int64 length, Single* output)
    {
        if (input == nullptr || output == nullptr || length <= 0)
        {
            return;
        }
        try
        {
            std::vector<Single> inputVector(input, input + length);
            std::vector<Single> result = Algorithm::Softmax(inputVector);
            std::copy(result.begin(), result.end(), output);
        }
        catch (...)
        {
        }
    }
}
