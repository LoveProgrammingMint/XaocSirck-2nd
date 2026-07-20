#include "SessionInference.hpp"
#include <cwchar>
#include <stdexcept>

SessionInference::SessionInference()
    : _inputTensor(),
      _outputTensor(),
      _cpuMemoryInfo(Ort::MemoryInfo::CreateCpu(OrtArenaAllocator, OrtMemTypeDefault))
{
}

SessionInference::~SessionInference()
{
}

Ort::Value* SessionInference::Packing(Single* data, const std::vector<Int64>& shape, const String& deviceName, Int32 deviceId)
{
    ValidateDevice(deviceName);

    if (data == nullptr)
    {
        throw std::invalid_argument("Input data pointer is null");
    }

    static_cast<void>(deviceId);

    Int64 totalSize = 1;
    for (Int64 dimension : shape)
    {
        totalSize *= dimension;
    }

    _inputTensor = std::make_unique<Ort::Value>(
        Ort::Value::CreateTensor<Single>(
            _cpuMemoryInfo,
            data,
            static_cast<size_t>(totalSize),
            shape.data(),
            shape.size()));

    return _inputTensor.get();
}

Ort::Value* SessionInference::Inference(Ort::Session* session, Ort::Value* tensor)
{
    if (session == nullptr || tensor == nullptr)
    {
        return nullptr;
    }

    const char* inputNames[] = { _inputName.c_str() };
    const char* outputNames[] = { _outputName.c_str() };

    Ort::RunOptions runOptions;
    std::vector<Ort::Value> outputs = session->Run(runOptions, inputNames, tensor, 1, outputNames, 1);

    _outputTensor = std::make_unique<Ort::Value>(std::move(outputs[0]));

    return _outputTensor.get();
}

void SessionInference::FreeTensor()
{
    _outputTensor.reset();
}

void SessionInference::SetInput(const std::string& name)
{
    _inputName = name;
}

void SessionInference::SetOutput(const std::string& name)
{
    _outputName = name;
}

void SessionInference::ValidateDevice(const String& deviceName)
{
    if (_wcsicmp(deviceName.c_str(), L"Cpu") != 0 && _wcsicmp(deviceName.c_str(), L"Gpu") != 0)
    {
        throw std::invalid_argument("Unsupported device name");
    }
}

const Single* SessionInference::GetTensorData(Ort::Value* tensor, Int64& outLength)
{
    if (tensor == nullptr)
    {
        outLength = 0;
        return nullptr;
    }

    std::vector<Int64> shape = GetTensorShape(tensor);
    outLength = ShapeProduct(shape);

    try
    {
        return tensor->GetTensorMutableData<Single>();
    }
    catch (...)
    {
        outLength = 0;
        return nullptr;
    }
}

std::vector<Int64> SessionInference::GetTensorShape(Ort::Value* tensor)
{
    if (tensor == nullptr)
    {
        return {};
    }

    try
    {
        Ort::TensorTypeAndShapeInfo info = tensor->GetTensorTypeAndShapeInfo();
        return info.GetShape();
    }
    catch (...)
    {
        return {};
    }
}

Int64 SessionInference::ShapeProduct(const std::vector<Int64>& shape)
{
    Int64 product = 1;
    for (Int64 dimension : shape)
    {
        product *= dimension;
    }
    return product;
}
