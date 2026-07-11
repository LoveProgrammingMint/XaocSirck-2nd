using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

using XaocSirck_Core.Interface.Feature;

namespace XaocSirck_Core.Feature.Obtain;

internal sealed unsafe class RawByteObtain : IFeatureObtain
{
    private readonly Int32 _bufferSize = 16388;
    private Byte* _resultPtr = (Byte*)NativeMemory.AlignedAlloc((UIntPtr)16388, (UIntPtr)32);
    private SharePool? _sharePool;
    private Boolean _disposed;

    internal RawByteObtain()
    {
        Clear();
    }

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RawByteObtain));
        Span<Byte> result = new(_resultPtr, _bufferSize);
        result.Clear();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_resultPtr != null) { NativeMemory.AlignedFree(_resultPtr); }
            _resultPtr = null;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public IntPtr GetResult()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RawByteObtain));
        return (IntPtr)_resultPtr;
    }

    public void Obtain()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RawByteObtain));
        using FileStream stream = File.OpenRead(_sharePool?.FilePath ?? throw new InvalidOperationException("File path is not set."));
        Int32 needsToRead = (Int32)Math.Min(_bufferSize, stream.Length);
        Span<Byte> span = new(_resultPtr, needsToRead);
        stream.ReadExactly(span);
    }

    public void Set(Object inputData)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RawByteObtain));
        if (inputData is not SharePool pool)
        {
            throw new ArgumentException("Input data must be a SharePool instance.", nameof(inputData));
        }
        _sharePool = pool;
    }
}
