#include "SessionManagement.hpp"
#include <cwchar>
#include <stdexcept>

SessionManagement::SessionManagement()
    : _env(ORT_LOGGING_LEVEL_WARNING, "XaocSirck"),
      _sessionOptions(),
      _currentDevice(L"Cpu"),
      _currentDeviceId(-1)
{
    BuildSessionOptions(L"Cpu", -1);
}

SessionManagement::~SessionManagement()
{
    ReleaseSessions();
}

void SessionManagement::Load()
{
    ReleaseSessions();
    LoadSessions(DefaultDirectory);
}

void SessionManagement::Load(const String& directory)
{
    ReleaseSessions();
    LoadSessions(directory);
}

void SessionManagement::LoadModel(const String& name, const String& path)
{
    auto session = std::make_unique<Ort::Session>(_env, path.c_str(), _sessionOptions);
    _sessions[name] = std::move(session);
}

Boolean SessionManagement::HasModel(const String& name) const
{
    return _sessions.find(name) != _sessions.end();
}

Ort::Session* SessionManagement::Get(const String& modelName)
{
    auto iterator = _sessions.find(modelName);
    if (iterator == _sessions.end())
    {
        return nullptr;
    }
    return iterator->second.get();
}

void SessionManagement::SwitchDevice(const String& deviceName)
{
    if (_wcsicmp(deviceName.c_str(), _currentDevice.c_str()) == 0)
    {
        return;
    }

    if (_wcsicmp(deviceName.c_str(), L"Cpu") != 0 && _wcsicmp(deviceName.c_str(), L"Gpu") != 0)
    {
        throw std::invalid_argument("Unsupported device name");
    }

    Int32 deviceId = -1;
    if (_wcsicmp(deviceName.c_str(), L"Gpu") == 0)
    {
        deviceId = 0;
    }

    ReleaseSessions();
    BuildSessionOptions(deviceName, deviceId);
    _currentDevice = deviceName;
    _currentDeviceId = deviceId;
}

void SessionManagement::BuildSessionOptions(const String& deviceName, Int32 deviceId)
{
    _sessionOptions = Ort::SessionOptions();
    if (_wcsicmp(deviceName.c_str(), L"Gpu") == 0)
    {
        Ort::ThrowOnError(OrtSessionOptionsAppendExecutionProvider_DML(_sessionOptions, deviceId));
    }
}

void SessionManagement::LoadSessions(const String& directory)
{
    String modelDir = directory;
    if (!modelDir.empty() && modelDir.back() != L'/' && modelDir.back() != L'\\')
    {
        modelDir += L'/';
    }
    const String searchPattern = modelDir + L"*.onnx";

    WIN32_FIND_DATAW findData;
    HANDLE findHandle = FindFirstFileW(searchPattern.c_str(), &findData);

    if (findHandle == INVALID_HANDLE_VALUE)
    {
        return;
    }

    do
    {
        if ((findData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
        {
            continue;
        }

        String fileName = findData.cFileName;
        size_t dotPosition = fileName.find_last_of(L'.');
        if (dotPosition == String::npos)
        {
            continue;
        }

        String modelName = fileName.substr(0, dotPosition);
        String filePath = modelDir + fileName;

        auto session = std::make_unique<Ort::Session>(_env, filePath.c_str(), _sessionOptions);
        _sessions[modelName] = std::move(session);
    } while (FindNextFileW(findHandle, &findData) != 0);

    FindClose(findHandle);
}

void SessionManagement::ReleaseSessions()
{
    _sessions.clear();
}

void SessionManagement::GetModelInfo(Ort::Session* session, std::string& inputName, std::string& outputName)
{
    if (session == nullptr)
    {
        inputName.clear();
        outputName.clear();
        return;
    }

    Ort::AllocatorWithDefaultOptions allocator;
    const size_t inputCount = session->GetInputCount();
    const size_t outputCount = session->GetOutputCount();

    if (inputCount > 0)
    {
        Ort::AllocatedStringPtr name = session->GetInputNameAllocated(0, allocator);
        inputName = name.get() != nullptr ? name.get() : "";
    }
    else
    {
        inputName.clear();
    }

    if (outputCount > 0)
    {
        Ort::AllocatedStringPtr name = session->GetOutputNameAllocated(0, allocator);
        outputName = name.get() != nullptr ? name.get() : "";
    }
    else
    {
        outputName.clear();
    }
}

std::vector<Int64> SessionManagement::GetInputShape(Ort::Session* session)
{
    std::vector<Int64> result;
    if (session == nullptr || session->GetInputCount() == 0)
        return result;

    Ort::TypeInfo typeInfo = session->GetInputTypeInfo(0);
    auto tensorInfo = typeInfo.GetTensorTypeAndShapeInfo();
    result = tensorInfo.GetShape();
    return result;
}

std::vector<Int64> SessionManagement::GetOutputShape(Ort::Session* session)
{
    std::vector<Int64> result;
    if (session == nullptr || session->GetOutputCount() == 0)
        return result;

    Ort::TypeInfo typeInfo = session->GetOutputTypeInfo(0);
    auto tensorInfo = typeInfo.GetTensorTypeAndShapeInfo();
    result = tensorInfo.GetShape();
    return result;
}
