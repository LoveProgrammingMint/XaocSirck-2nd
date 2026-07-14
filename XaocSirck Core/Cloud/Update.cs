using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace XaocSirck_Core.Cloud;

internal class Update
{
    private readonly ProcessStartInfo info = new() { UseShellExecute = true, Arguments = "./update_temp/decompressed", FileName = "./Update.exe" };

    public void Run()
    {
        ZipFile.ExtractToDirectory("./update_temp/update_pkg.izxs", @"./update_temp/decompressed", overwriteFiles: true);
        Process.Start(info);
    }
}
