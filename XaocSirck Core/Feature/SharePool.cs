using PeNet;
using System;
using System.Collections.Generic;
using System.Text;

namespace XaocSirck_Core.Feature;

internal sealed unsafe class SharePool
{
    public PeFile? Pe { get; set; }
    public IntPtr RawBytes { get; set; }
    public String? FilePath { get; set; }
}
