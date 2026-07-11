using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

using XaocSirck_Core.Interface.Feature;

namespace XaocSirck_Core.Feature.Extraction;

internal sealed unsafe class AssemblyListExtraction : IFeatureExtraction
{
    private readonly Int32 _embeddingDim = 192;
    private readonly Dictionary<String, Int32> _tokenToId;
    private readonly Single[] _embeddingTable;
    private readonly Int32 _vocabSize;
    private Int32* _inputData = null;
    private IntPtr _resultPtr = IntPtr.Zero;
    private Boolean _disposed;

    public AssemblyListExtraction()
    {
        String vocabPath = Path.Combine(AppContext.BaseDirectory, "XaocSirck", "AssemblyList", "platforms_mnemonics_vocab.bin");
        (_tokenToId, _vocabSize) = LoadBpeVocab(vocabPath);

        String embedPath = Path.Combine(AppContext.BaseDirectory, "XaocSirck", "Embeddings", "AssemblyList", "embedding_static.bin");
        if (!File.Exists(embedPath))
            throw new FileNotFoundException($"Embedding file not found: {embedPath}");
        Byte[] embedData = File.ReadAllBytes(embedPath);
        Int32 expectedSize = _vocabSize * _embeddingDim * sizeof(Single);
        if (embedData.Length != expectedSize)
            throw new InvalidDataException(
                $"Embedding file size mismatch: expected {expectedSize}, got {embedData.Length}");
        _embeddingTable = new Single[_vocabSize * _embeddingDim];
        Buffer.BlockCopy(embedData, 0, _embeddingTable, 0, embedData.Length);
    }

    private static (Dictionary<String, Int32> TokenToId, Int32 VocabSize) LoadBpeVocab(String path)
    {
        using FileStream fs = new(path, FileMode.Open, FileAccess.Read);
        using BinaryReader br = new(fs);
        Int32 count = br.ReadInt32();
        Dictionary<String, Int32> tokenToId = new(count, StringComparer.OrdinalIgnoreCase);
        for (Int32 i = 0; i < count; i++)
        {
            Int32 len = br.ReadInt32();
            String name = Encoding.UTF8.GetString(br.ReadBytes(len));
            Int32 leftId = br.ReadInt32();
            Int32 rightId = br.ReadInt32();
            if (leftId == -1)
                tokenToId[name] = i;
        }
        return (tokenToId, count);
    }

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(AssemblyListExtraction));
        if (_resultPtr != IntPtr.Zero)
        {
            NativeMemory.AlignedFree((void*)_resultPtr);
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

    public void Extract()
    {
        Int32 totalBytes = *_inputData;
        Int32 payloadBytes = totalBytes - sizeof(Int32);
        if (payloadBytes <= 0)
            return;

        Int32 tokenCount = payloadBytes / sizeof(Int32);
        if (tokenCount == 0)
            return;

        Int32* idPtr = _inputData + 1;

        Int32 outputLength = _embeddingDim * _embeddingDim;
        IntPtr newPtr = (IntPtr)NativeMemory.AlignedAlloc((UIntPtr)(sizeof(Single) * outputLength), 64);
        try
        {
            Span<Single> outputSpan = new(newPtr.ToPointer(), outputLength);
            outputSpan.Clear();

            Int32 limit = Math.Min(tokenCount, _embeddingDim);
            for (Int32 t = 0; t < limit; t++)
            {
                Int32 tokenId = idPtr[t];
                if (tokenId < 0 || tokenId >= _vocabSize)
                    continue;

                Int32 embedBase = tokenId * _embeddingDim;
                Int32 outBase = t * _embeddingDim;
                for (Int32 d = 0; d < _embeddingDim; d++)
                    outputSpan[outBase + d] = _embeddingTable[embedBase + d];
            }

            _resultPtr = newPtr;
        }
        catch
        {
            NativeMemory.AlignedFree((void*)newPtr);
            throw;
        }
    }

    public IntPtr GetResult()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(AssemblyListExtraction));
        return _resultPtr;
    }

    public void Set(Object inputData)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(AssemblyListExtraction));
        if (inputData is not IntPtr ptr)
            throw new ArgumentException("Input data must be of type IntPtr.", nameof(inputData));
        _inputData = (Int32*)ptr;
    }
}
