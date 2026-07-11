#pragma once
#include "Base.hpp"
#include <onnxruntime/onnxruntime_c_api.h>
#include <onnxruntime/onnxruntime_cxx_api.h>
#include <onnxruntime/dml_provider_factory.h>
#include <memory>
#include <unordered_map>

class SessionManagement
{
public:
    SessionManagement();
    ~SessionManagement();

    void Load();
    Ort::Session* Get(const String& modelName);
    void SwitchDevice(const String& deviceName);

private:
    Ort::Env _env;
    Ort::SessionOptions _sessionOptions;
    String _currentDevice;
    Int32 _currentDeviceId;
    std::unordered_map<String, std::unique_ptr<Ort::Session>> _sessions;

    void BuildSessionOptions(const String& deviceName, Int32 deviceId);
    void LoadSessions();
    void ReleaseSessions();
};
