using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

using XaocSirck_Core.Interface.Feature;

namespace XaocSirck_Core.Feature.Extraction;

internal sealed unsafe class RawByteExtraction : IFeatureExtraction
{
    private Byte* _inputData = null;
    private readonly Single[] _embeddingTable;
    private IntPtr _resultPtr = IntPtr.Zero;
    private Boolean _disposed;

    public RawByteExtraction()
    {
        String EmbeddingPath = Path.Combine(AppContext.BaseDirectory, "XaocSirck", "Embeddings", "RawByte", "embedding_static.bin");
        if (File.Exists(EmbeddingPath))
        {
            Byte[] Data = File.ReadAllBytes(EmbeddingPath);
            _embeddingTable = new Single[2048];
            Buffer.BlockCopy(Data, 0, _embeddingTable, 0, 8192);
        }
        else
        {
            throw new FileNotFoundException($"Embedding file not found: {EmbeddingPath}");
        }
    }

    public void Extract()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RawByteExtraction));
        IntPtr newPtr = Marshal.AllocHGlobal(sizeof(Single) * 131072);
        try
        {
            Span<Single> outputSpan = new(newPtr.ToPointer(), 131072);
            ReadOnlySpan<Single> embeddingSpan = _embeddingTable;
            ReadOnlySpan<Byte> inputSpan = new(_inputData + sizeof(Int32), 16384);
            for (Int32 c = 0; c < 8; c++)
            {
                Int32 dstOffsetBase = c * 16384;
                for (Int32 i = 0; i < 16384; i++)
                {
                    Int32 tokenId = inputSpan[i];
                    Int32 srcOffset = tokenId * 8 + c;
                    outputSpan[dstOffsetBase + i] = embeddingSpan[srcOffset];
                }
            }
            _resultPtr = newPtr;
        }
        catch
        {
            Marshal.FreeHGlobal(newPtr);
            throw;
        }
    }

    public nint GetResult()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RawByteExtraction));
        return _resultPtr;
    }

    public void Set(Object inputData)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RawByteExtraction));
        if (inputData is not IntPtr DataIntPtr)
            throw new ArgumentException("Input data must be a pointer to the data.", nameof(inputData));
        _inputData = (Byte*)DataIntPtr;
    }

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RawByteExtraction));
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
}
