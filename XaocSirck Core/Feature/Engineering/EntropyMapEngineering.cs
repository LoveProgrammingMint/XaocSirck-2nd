using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

using XaocSirck_Core.Interface.Feature;

namespace XaocSirck_Core.Feature.Engineering;

internal sealed unsafe class EntropyMapEngineering : IFeatureEngineering
{
    private readonly Int32 _windowSize = 256;
    private readonly Int32 _totalBytes = 256 * 64;
    private Byte* _inputData = null;
    private IntPtr _resultPtr = IntPtr.Zero;
    private Boolean _disposed;

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(EntropyMapEngineering));
        if (_resultPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_resultPtr);
            _resultPtr = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Clear();
            _inputData = null;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public void Engineer()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(EntropyMapEngineering));
        Int32 outputLength = _windowSize;
        IntPtr newPtr = Marshal.AllocHGlobal(sizeof(Single) * outputLength);
        try
        {
            Span<Single> outputSpan = new((void*)newPtr, outputLength);
            outputSpan.Clear();
            Int32 actualWindows = (_totalBytes + _windowSize - 1) / _windowSize;
            for (Int32 w = 0; w < actualWindows; w++)
            {
                Int32 offset = w * _windowSize;
                Int32 currentWindowSize = Math.Min(_windowSize, _totalBytes - offset);
                if (currentWindowSize <= 0)
                    break;
                Single entropy = CalculateShannonEntropy(_inputData + offset, currentWindowSize);
                outputSpan[w] = entropy / 8.0f;
            }
            _resultPtr = newPtr;
        }
        catch
        {
            Marshal.FreeHGlobal(newPtr);
            throw;
        }
    }

    private static Single CalculateShannonEntropy(Byte* data, Int32 length)
    {
        if (length <= 0)
            return 0.0f;
        Int32[] freq = new Int32[256];
        for (Int32 i = 0; i < length; i++)
            freq[data[i]]++;
        Single entropy = 0.0f;
        Single log2 = MathF.Log(2.0f);
        for (Int32 i = 0; i < 256; i++)
        {
            if (freq[i] == 0)
                continue;
            Single p = (Single)freq[i] / length;
            entropy -= p * (MathF.Log(p) / log2);
        }
        return entropy;
    }

    public IntPtr GetResult()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(EntropyMapEngineering));
        return _resultPtr;
    }

    public void Set(Object? inputData)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(EntropyMapEngineering));
        if (inputData is not IntPtr ptr)
            throw new ArgumentException("Input data must be an IntPtr", nameof(inputData));
        _inputData = (Byte*)ptr;
    }
}
