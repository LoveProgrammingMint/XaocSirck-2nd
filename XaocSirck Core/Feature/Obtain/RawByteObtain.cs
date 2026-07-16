using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

using XaocSirck_Core.Interface.Feature;

namespace XaocSirck_Core.Feature.Obtain;

internal sealed unsafe class RawByteObtain : IFeatureObtain
{
    private const Int32 PayloadSize = 16384;
    private const Int32 BufferSize = PayloadSize + sizeof(Int32);
    private Byte* _resultPtr = (Byte*)NativeMemory.AlignedAlloc((UIntPtr)BufferSize, (UIntPtr)32);
    private SharePool? _sharePool;
    private Boolean _disposed;

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RawByteObtain));
        Span<Byte> result = new(_resultPtr, BufferSize);
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
        Int64 remaining = stream.Length - stream.Position;
        Int32 bytesToRead = (Int32)Math.Min(PayloadSize, remaining);
        if (bytesToRead == 0)
        {
            *(Int32*)_resultPtr = sizeof(Int32);
            return;
        }
        *(Int32*)_resultPtr = sizeof(Int32) + bytesToRead;
        Span<Byte> span = new(_resultPtr + sizeof(Int32), bytesToRead);
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
