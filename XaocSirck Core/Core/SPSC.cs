using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace XaocSirck_Core.Core;

internal class SPSC<T> where T : class
{
    private readonly T?[] _buffer;
    private readonly Int32 _capacity;
    private readonly Int32 _mask;
    private Int32 _head;
    private Int32 _tail;

    public SPSC(Int32 capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        Int32 size = RoundUpToPowerOfTwo(capacity);
        _capacity = size;
        _mask = size - 1;
        _buffer = new T[size];
    }

    public Int32 Capacity => _capacity;

    public Boolean TryEnqueue(T item)
    {
        Int32 head = Volatile.Read(ref _head);
        Int32 next = (head + 1) & _mask;
        if (next == Volatile.Read(ref _tail))
            return false;

        _buffer[head] = item;
        Volatile.Write(ref _head, next);
        return true;
    }

    public Boolean TryDequeue([MaybeNullWhen(false)] out T item)
    {
        Int32 tail = Volatile.Read(ref _tail);
        if (tail == Volatile.Read(ref _head))
        {
            item = null;
            return false;
        }

        item = _buffer[tail]!;
        _buffer[tail] = null;
        Volatile.Write(ref _tail, (tail + 1) & _mask);
        return true;
    }

    public Boolean IsEmpty => Volatile.Read(ref _head) == Volatile.Read(ref _tail);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Int32 RoundUpToPowerOfTwo(Int32 value)
    {
        if (value <= 2)
            return 2;

        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }
}
