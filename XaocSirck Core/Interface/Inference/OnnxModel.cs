using System.Runtime.InteropServices;
using XaocSirck_Core;
using XaocSirck_Core.Native;

namespace XaocSirck_Core.Interface.Inference;

internal sealed unsafe class OnnxModel : ModelBase
{
    private readonly IntPtr _sessionManagement;
    private readonly String _name;
    private IntPtr _inference;
    private String _inputName = String.Empty;
    private String _outputName = String.Empty;
    private Boolean _loaded;
    private Boolean _disposed;

    public override Boolean EnableGpu { get; set; }

    public String Name => _name;
    public String InputName => _inputName;
    public String OutputName => _outputName;
    public Boolean IsLoaded => _loaded;
    public Int64[] InputShape { get; private set; } = [];
    public Int64[] OutputShape { get; private set; } = [];

    public OnnxModel(IntPtr sessionManagement, String name)
    {
        ArgumentNullException.ThrowIfNull(name);
        _sessionManagement = sessionManagement;
        _name = name;
    }

    public override void Load(String modelPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(OnnxModel));
        ArgumentNullException.ThrowIfNull(modelPath);

        if (_sessionManagement == IntPtr.Zero)
        {
            App.Logger.Error($"ONNX model session management unavailable: {_name}");
            throw new InvalidOperationException("Session management is not available.");
        }

        InferenceService.XaocSirckSessionManagementLoadModel(_sessionManagement, _name, modelPath);
        IntPtr session = InferenceService.XaocSirckSessionManagementGet(_sessionManagement, _name);
        if (session == IntPtr.Zero)
            throw new InvalidOperationException($"Failed to load model '{_name}' from '{modelPath}'.");

        _inputName = FetchName(session, input: true);
        _outputName = FetchName(session, input: false);
        InputShape = FetchShape(session, input: true);
        OutputShape = FetchShape(session, input: false);

        _inference = InferenceService.XaocSirckSessionInferenceCreate();
        if (_inference == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create inference session.");

        InferenceService.XaocSirckSessionInferenceSetInput(_inference, _inputName);
        InferenceService.XaocSirckSessionInferenceSetOutput(_inference, _outputName);
        _loaded = true;
    }

    public Single[] Run(Single[] data, Int64[] shape)
    {
        ArgumentNullException.ThrowIfNull(data);
        fixed (Single* ptr = data)
            return Run((IntPtr)ptr, shape);
    }

    public override Single[] Run(IntPtr data, Int64[] shape)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(OnnxModel));
        ObjectDisposedException.ThrowIf(!_loaded, nameof(OnnxModel));
        ArgumentNullException.ThrowIfNull(shape);
        if (_inference == IntPtr.Zero || _sessionManagement == IntPtr.Zero)
            throw new InvalidOperationException("Model is not ready for inference.");

        IntPtr session = InferenceService.XaocSirckSessionManagementGet(_sessionManagement, _name);
        if (session == IntPtr.Zero)
            throw new InvalidOperationException($"Model session '{_name}' is not available.");

        fixed (Int64* shapePtr = shape)
        {
            IntPtr tensor = InferenceService.XaocSirckSessionInferencePacking(_inference, (Single*)data, shapePtr, shape.Length, "Cpu", 0);
            if (tensor == IntPtr.Zero)
                throw new InvalidOperationException("Failed to pack input tensor.");

            IntPtr outputTensor = InferenceService.XaocSirckSessionInferenceInference(_inference, session, tensor);
            if (outputTensor == IntPtr.Zero)
                throw new InvalidOperationException("Inference returned no output.");

            Single* outputData = InferenceService.XaocSirckSessionInferenceGetOutputData(outputTensor, out Int64 length);
            if (outputData == null || length <= 0)
                return [];

            Single[] result = GC.AllocateUninitializedArray<Single>((Int32)length);
            fixed (Single* dst = result)
            {
                Buffer.MemoryCopy(outputData, dst, (UInt64)(length * sizeof(Single)), (UInt64)(length * sizeof(Single)));
            }
            return result;
        }
    }

    public override void Dispose()
    {
        if (!_disposed)
        {
            if (_inference != IntPtr.Zero)
            {
                InferenceService.XaocSirckSessionInferenceDestroy(_inference);
                _inference = IntPtr.Zero;
            }
            _loaded = false;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    private static String FetchName(IntPtr session, Boolean input)
    {
        Byte* namePtr = null;
        Int32 success = input
            ? InferenceService.XaocSirckSessionManagementGetInputName(session, out namePtr)
            : InferenceService.XaocSirckSessionManagementGetOutputName(session, out namePtr);

        if (success == 0 || namePtr == null)
            return String.Empty;

        try
        {
            return Marshal.PtrToStringUTF8((IntPtr)namePtr) ?? String.Empty;
        }
        finally
        {
            InferenceService.XaocSirckSessionManagementFreeName(namePtr);
        }
    }

    private static Int64[] FetchShape(IntPtr session, Boolean input)
    {
        Int64* shapePtr = input
            ? InferenceService.XaocSirckSessionManagementGetInputShape(session, out Int64 rank)
            : InferenceService.XaocSirckSessionManagementGetOutputShape(session, out rank);

        if (shapePtr == null || rank <= 0)
            return [];

        try
        {
            Int64[] shape = new Int64[rank];
            fixed (Int64* dst = shape)
                Buffer.MemoryCopy(shapePtr, dst, (UInt64)(rank * sizeof(Int64)), (UInt64)(rank * sizeof(Int64)));
            return shape;
        }
        finally
        {
            InferenceService.XaocSirckSessionManagementFreeShape(shapePtr);
        }
    }
}
