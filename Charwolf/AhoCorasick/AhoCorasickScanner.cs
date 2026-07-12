using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Charwolf.AhoCorasick;


internal readonly struct AcMatch(Int32 patternId, Int32 start, Int32 end)
{
    public readonly Int32 PatternId = patternId;
    public readonly Int32 StartOffset = start;
    public readonly Int32 EndOffset = end;
}

internal sealed unsafe class AcScanner : IDisposable
{
    private readonly Int16* _base;
    private readonly Int16* _check;
    private readonly Int16* _fail;
    private readonly Int32* _outputHead;
    private readonly Int32* _outputNext;
    private readonly Int32* _outputPattern;
    private readonly Int32* _patternLength;
    private readonly Int32 _arraySize;
    private readonly Int32 _patternCount;

    private IntPtr _nativeBlock;
    private Boolean _disposed;

    private const Int32 StackAllocThreshold = 4096;

    public AcScanner(DoubleArrayResult result)
    {
        _arraySize = result.Base.Length;
        _patternCount = result.PatternLength.Length;

        IntPtr totalBytes =
            sizeof(Int16) * _arraySize * 3 +
            sizeof(Int32) * _arraySize +
            sizeof(Int32) * result.OutputNext.Length +
            sizeof(Int32) * result.OutputPattern.Length +
            sizeof(Int32) * _patternCount;

        _nativeBlock = (IntPtr)NativeMemory.AlignedAlloc((UIntPtr)totalBytes, (UIntPtr)64);
        if (_nativeBlock == IntPtr.Zero)
            throw new OutOfMemoryException("Failed to allocate AC tables");

        Byte* ptr = (Byte*)_nativeBlock;

        _base = (Int16*)ptr; ptr += sizeof(Int16) * _arraySize;
        _check = (Int16*)ptr; ptr += sizeof(Int16) * _arraySize;
        _fail = (Int16*)ptr; ptr += sizeof(Int16) * _arraySize;
        _outputHead = (Int32*)ptr; ptr += sizeof(Int32) * _arraySize;
        _outputNext = (Int32*)ptr; ptr += sizeof(Int32) * result.OutputNext.Length;
        _outputPattern = (Int32*)ptr; ptr += sizeof(Int32) * result.OutputPattern.Length;
        _patternLength = (Int32*)ptr;

        fixed (Int16* b = result.Base, c = result.Check, f = result.Fail)
        {
            Buffer.MemoryCopy(b, _base, sizeof(Int16) * _arraySize, sizeof(Int16) * _arraySize);
            Buffer.MemoryCopy(c, _check, sizeof(Int16) * _arraySize, sizeof(Int16) * _arraySize);
            Buffer.MemoryCopy(f, _fail, sizeof(Int16) * _arraySize, sizeof(Int16) * _arraySize);
        }

        fixed (Int32* h = result.OutputHead)
            Buffer.MemoryCopy(h, _outputHead, sizeof(Int32) * _arraySize, sizeof(Int32) * _arraySize);

        fixed (Int32* n = result.OutputNext)
            Buffer.MemoryCopy(n, _outputNext, sizeof(Int32) * result.OutputNext.Length, sizeof(Int32) * result.OutputNext.Length);

        fixed (Int32* p = result.OutputPattern)
            Buffer.MemoryCopy(p, _outputPattern, sizeof(Int32) * result.OutputPattern.Length, sizeof(Int32) * result.OutputPattern.Length);

        fixed (Int32* l = result.PatternLength)
            Buffer.MemoryCopy(l, _patternLength, sizeof(Int32) * _patternCount, sizeof(Int32) * _patternCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Int32 Transition(Int32 state, Byte b)
    {
        Int32 t = _base[state] + b;

        return (UInt32)t < (UInt32)_arraySize && _check[t] == state ? t : -1;
    }

    public Int32 Scan(ReadOnlySpan<Byte> data, Span<AcMatch> outputBuffer)
    {
        Int32 matchCount = 0;
        Int32 state = 0;
        Int32 bufLen = outputBuffer.Length;

        fixed (Byte* ptr = data)
        {
            for (Int32 i = 0; i < data.Length; i++)
            {
                Byte b = ptr[i];
                Int32 next = Transition(state, b);

                while (next == -1 && state != 0)
                {
                    state = _fail[state];
                    next = Transition(state, b);
                }

                state = next == -1 ? 0 : next;

                Int32 head = _outputHead[state];
                while (head != -1)
                {
                    if (matchCount >= bufLen) return matchCount;

                    Int32 pid = _outputPattern[head];
                    Int32 len = _patternLength[pid];
                    outputBuffer[matchCount++] = new AcMatch(pid, i - len + 1, i + 1);

                    head = _outputNext[head];
                }
            }
        }

        return matchCount;
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (_nativeBlock != IntPtr.Zero)
        {
            NativeMemory.AlignedFree(_nativeBlock.ToPointer());
            _nativeBlock = IntPtr.Zero;
        }
        _disposed = true;
    }
}
