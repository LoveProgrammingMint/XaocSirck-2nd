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
    LoadSessions();
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
    LoadSessions();
}

void SessionManagement::BuildSessionOptions(const String& deviceName, Int32 deviceId)
{
    _sessionOptions = Ort::SessionOptions();
    if (_wcsicmp(deviceName.c_str(), L"Gpu") == 0)
    {
        Ort::ThrowOnError(OrtSessionOptionsAppendExecutionProvider_DML(_sessionOptions, deviceId));
    }
}

void SessionManagement::LoadSessions()
{
    const String modelDir = L"./XaocSirck/Models/";
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
