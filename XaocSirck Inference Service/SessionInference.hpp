#pragma once
#include "Base.hpp"
#include <onnxruntime/onnxruntime_c_api.h>
#include <onnxruntime/onnxruntime_cxx_api.h>
#include <memory>
#include <string>
#include <vector>

class SessionInference
{
public:
    SessionInference();
    ~SessionInference();

    Ort::Value* Packing(Single* data, const std::vector<Int64>& shape, const String& deviceName, Int32 deviceId);
    Ort::Value* Inference(Ort::Session* session, Ort::Value* tensor);
    void SetInput(const std::string& name);
    void SetOutput(const std::string& name);

    static const Single* GetTensorData(Ort::Value* tensor, Int64& outLength);
    static std::vector<Int64> GetTensorShape(Ort::Value* tensor);

private:
    std::unique_ptr<Ort::Value> _inputTensor;
    std::unique_ptr<Ort::Value> _outputTensor;
    Ort::MemoryInfo _cpuMemoryInfo;
    std::string _inputName;
    std::string _outputName;

    void ValidateDevice(const String& deviceName);
    static Int64 ShapeProduct(const std::vector<Int64>& shape);
};
