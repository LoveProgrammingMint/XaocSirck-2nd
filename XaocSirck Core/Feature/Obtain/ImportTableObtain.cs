using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

using PeNet;
using PeNet.Header.Pe;

using XaocSirck_Core.Interface.Feature;

namespace XaocSirck_Core.Feature.Obtain;

internal sealed unsafe class ImportTableObtain : IFeatureObtain
{
    private readonly Int32 _vocabSize = 417;
    private readonly Int32 _embeddingDim = 1;
    private readonly HashSet<String> _vocabSet;
    private readonly String[] _vocabTokens;
    private IntPtr _resultPtr = IntPtr.Zero;
    private SharePool? _sharePool;
    private Boolean _disposed;

    public ImportTableObtain()
    {
        String vocabPath = Path.Combine(App.RuntimeDirectory, "Resources", "winapi_vocab_417.bin");
        if (!File.Exists(vocabPath))
            throw new FileNotFoundException($"Vocabulary file not found: {vocabPath}");
        _vocabTokens = File.ReadAllText(vocabPath)
            .ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (_vocabTokens.Length != _vocabSize)
            throw new InvalidDataException($"Vocab size mismatch: expected {_vocabSize}, got {_vocabTokens.Length}");
        _vocabSet = new HashSet<String>(_vocabSize, StringComparer.OrdinalIgnoreCase);
        foreach (String token in _vocabTokens)
            _vocabSet.Add(token.Trim().ToLowerInvariant());
    }

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(ImportTableObtain));
        if (_resultPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_resultPtr);
            _resultPtr = IntPtr.Zero;
        }
        _sharePool = null;
    }

    public void Dispose()
    {
        _disposed = true;
        if (_resultPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_resultPtr);
            _resultPtr = IntPtr.Zero;
        }
        GC.SuppressFinalize(this);
    }

    public IntPtr GetResult()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(ImportTableObtain));
        return _resultPtr;
    }

    public void Obtain()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(ImportTableObtain));
        ImportFunction[]? importedFunctions = _sharePool?.Pe?.ImportedFunctions;
        HashSet<String> importedApis = new(StringComparer.OrdinalIgnoreCase);
        if (importedFunctions != null)
        {
            foreach (ImportFunction func in importedFunctions)
            {
                String? apiName = func.Name;
                if (!String.IsNullOrEmpty(apiName))
                    importedApis.Add(apiName.ToLowerInvariant());
            }
        }
        Int32 outputLength = _vocabSize * _embeddingDim;
        Int32 totalBytes = sizeof(Int32) + sizeof(Single) * outputLength;
        IntPtr newPtr = Marshal.AllocHGlobal(totalBytes);
        try
        {
            Byte* basePtr = (Byte*)newPtr.ToPointer();
            *(Int32*)basePtr = totalBytes;
            Span<Single> outputSpan = new(basePtr + sizeof(Int32), outputLength);
            for (Int32 i = 0; i < _vocabSize; i++)
            {
                String token = _vocabTokens[i].Trim().ToLowerInvariant();
                outputSpan[i] = importedApis.Contains(token) ? 1.0f : -1.0f;
            }
            _resultPtr = newPtr;
        }
        catch
        {
            Marshal.FreeHGlobal(newPtr);
            throw;
        }
    }

    public void Set(Object inputData)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(ImportTableObtain));
        if (inputData is not SharePool pool)
        {
            throw new ArgumentException("Input data must be a SharePool instance.", nameof(inputData));
        }
        _sharePool = pool;
    }
}
