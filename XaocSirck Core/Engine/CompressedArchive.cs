using SharpCompress;
using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace XaocSirck_Core.Engine;

public sealed unsafe class CompressedArchive : IDisposable
{
    private IArchive? _archive;
    private IEnumerator<IArchiveEntry>? _entries;
    private IntPtr _buffer;
    private Int64 _length;
    private Boolean _disposed;

    public IntPtr Buffer => _buffer;
    public Int64 Length => _length;

    public void Load(String path)
    {
        Dispose();
        _archive = ArchiveFactory.OpenArchive(path);
        _entries = _archive.Entries.GetEnumerator();
    }

    public Boolean Next()
    {
        if (_disposed || _entries is null) return false;
        FreeBuffer();
        while (_entries.MoveNext())
        {
            IArchiveEntry entry = _entries.Current;
            if (entry.IsDirectory) continue;
            Int64 size = entry.Size;
            if (size > 200L * 1024L * 1024L || size <= 0) continue;
            _length = size;
            _buffer = Marshal.AllocHGlobal((nint)size);   // Use IntPtr will get a info ( - . -)
            using Stream stream = entry.OpenEntryStream();
            Span<Byte> span = new(_buffer.ToPointer(), (Int32)size);
            Int32 total = 0;
            while (total < size)
            {
                Int32 read = stream.Read(span[total..]);
                if (read == 0) break;
                total += read;
            }
            return true;
        }
        return false;
    }

    private void FreeBuffer()
    {
        if (_buffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_buffer);
            _buffer = IntPtr.Zero;
        }
        _length = 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        FreeBuffer();
        _entries?.Dispose();
        _archive?.Dispose();
        _disposed = true;
    }
}
