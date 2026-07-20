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

    __declspec(dllexport) void XaocSirckSessionManagementLoadDirectory(void* handle, const wchar_t* directory)
    {
        if (handle == nullptr || directory == nullptr)
        {
            return;
        }
        try
        {
            static_cast<SessionManagement*>(handle)->Load(directory);
        }
        catch (...)
        {
        }
    }

    __declspec(dllexport) void XaocSirckSessionManagementLoadModel(void* handle, const wchar_t* name, const wchar_t* path)
    {
        if (handle == nullptr || name == nullptr || path == nullptr)
        {
            return;
        }
        try
        {
            static_cast<SessionManagement*>(handle)->LoadModel(name, path);
        }
        catch (...)
        {
        }
    }

    __declspec(dllexport) int XaocSirckSessionManagementHasModel(void* handle, const wchar_t* name)
    {
        if (handle == nullptr || name == nullptr)
        {
            return 0;
        }
        try
        {
            return static_cast<SessionManagement*>(handle)->HasModel(name) ? 1 : 0;
        }
        catch (...)
        {
            return 0;
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

    __declspec(dllexport) int XaocSirckSessionManagementGetInputName(void* session, char** outName)
    {
        if (session == nullptr || outName == nullptr)
        {
            return 0;
        }
        try
        {
            std::string inputName;
            std::string outputName;
            SessionManagement::GetModelInfo(static_cast<Ort::Session*>(session), inputName, outputName);
            if (inputName.empty())
            {
                return 0;
            }
            char* result = new char[inputName.length() + 1];
            std::copy(inputName.begin(), inputName.end(), result);
            result[inputName.length()] = '\0';
            *outName = result;
            return 1;
        }
        catch (...)
        {
            return 0;
        }
    }

    __declspec(dllexport) int XaocSirckSessionManagementGetOutputName(void* session, char** outName)
    {
        if (session == nullptr || outName == nullptr)
        {
            return 0;
        }
        try
        {
            std::string inputName;
            std::string outputName;
            SessionManagement::GetModelInfo(static_cast<Ort::Session*>(session), inputName, outputName);
            if (outputName.empty())
            {
                return 0;
            }
            char* result = new char[outputName.length() + 1];
            std::copy(outputName.begin(), outputName.end(), result);
            result[outputName.length()] = '\0';
            *outName = result;
            return 1;
        }
        catch (...)
        {
            return 0;
        }
    }

    __declspec(dllexport) void XaocSirckSessionManagementFreeName(char* name)
    {
        delete[] name;
    }

    __declspec(dllexport) const Int64* XaocSirckSessionManagementGetInputShape(void* session, Int64* outRank)
    {
        if (session == nullptr || outRank == nullptr)
        {
            return nullptr;
        }
        try
        {
            std::vector<Int64> shape = SessionManagement::GetInputShape(static_cast<Ort::Session*>(session));
            *outRank = static_cast<Int64>(shape.size());
            if (shape.empty())
            {
                return nullptr;
            }
            Int64* result = new Int64[shape.size()];
            std::copy(shape.begin(), shape.end(), result);
            return result;
        }
        catch (...)
        {
            *outRank = 0;
            return nullptr;
        }
    }

    __declspec(dllexport) const Int64* XaocSirckSessionManagementGetOutputShape(void* session, Int64* outRank)
    {
        if (session == nullptr || outRank == nullptr)
        {
            return nullptr;
        }
        try
        {
            std::vector<Int64> shape = SessionManagement::GetOutputShape(static_cast<Ort::Session*>(session));
            *outRank = static_cast<Int64>(shape.size());
            if (shape.empty())
            {
                return nullptr;
            }
            Int64* result = new Int64[shape.size()];
            std::copy(shape.begin(), shape.end(), result);
            return result;
        }
        catch (...)
        {
            *outRank = 0;
            return nullptr;
        }
    }

    __declspec(dllexport) void XaocSirckSessionManagementFreeShape(const Int64* shape)
    {
        delete[] shape;
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

    __declspec(dllexport) void XaocSirckSessionInferenceFreeTensor(void* handle)
    {
        if (handle == nullptr)
        {
            return;
        }
        try
        {
            static_cast<SessionInference*>(handle)->FreeTensor();
        }
        catch (...)
        {
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

    __declspec(dllexport) const Single* XaocSirckSessionInferenceGetOutputData(void* tensor, Int64* outLength)
    {
        if (tensor == nullptr || outLength == nullptr)
        {
            return nullptr;
        }
        try
        {
            return SessionInference::GetTensorData(static_cast<Ort::Value*>(tensor), *outLength);
        }
        catch (...)
        {
            *outLength = 0;
            return nullptr;
        }
    }

    __declspec(dllexport) const Int64* XaocSirckSessionInferenceGetOutputShape(void* tensor, Int64* outRank)
    {
        if (tensor == nullptr || outRank == nullptr)
        {
            return nullptr;
        }
        try
        {
            std::vector<Int64> shape = SessionInference::GetTensorShape(static_cast<Ort::Value*>(tensor));
            *outRank = static_cast<Int64>(shape.size());
            if (shape.empty())
            {
                return nullptr;
            }
            Int64* result = new Int64[shape.size()];
            std::copy(shape.begin(), shape.end(), result);
            return result;
        }
        catch (...)
        {
            *outRank = 0;
            return nullptr;
        }
    }

    __declspec(dllexport) void XaocSirckSessionInferenceFreeShape(const Int64* shape)
    {
        delete[] shape;
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
