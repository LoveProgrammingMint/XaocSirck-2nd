using PeNet;
using System;

namespace XaocSirck_Core.Feature;

internal sealed unsafe class SharePool : IDisposable
{
    public PeFile? Pe { get; set; }
    public IntPtr RawBytes { get; set; }
    public String? FilePath { get; set; }
    private Boolean _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            Pe = null;
            RawBytes = IntPtr.Zero;
            FilePath = null;
            _disposed = true;
        }
    }
}
