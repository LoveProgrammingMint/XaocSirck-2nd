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
    void Load(const String& directory);
    void LoadModel(const String& name, const String& path);
    Boolean HasModel(const String& name) const;
    Ort::Session* Get(const String& modelName);
    void SwitchDevice(const String& deviceName);

    static void GetModelInfo(Ort::Session* session, std::string& inputName, std::string& outputName);
    static std::vector<Int64> GetInputShape(Ort::Session* session);
    static std::vector<Int64> GetOutputShape(Ort::Session* session);

private:
    static constexpr const wchar_t* DefaultDirectory = L"./XaocSirck/Models/";

    Ort::Env _env;
    Ort::SessionOptions _sessionOptions;
    String _currentDevice;
    Int32 _currentDeviceId;
    std::unordered_map<String, std::unique_ptr<Ort::Session>> _sessions;

    void BuildSessionOptions(const String& deviceName, Int32 deviceId);
    void LoadSessions(const String& directory);
    void ReleaseSessions();
};
