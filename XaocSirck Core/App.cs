using System;
using System.IO;
using XaocSirck_Core.Engine;

namespace XaocSirck_Core;

internal static class App
{
    public static Settings Settings { get; } = new();

    public static String RuntimeDirectory
    {
        get
        {
            DirectoryInfo? dir = new(AppContext.BaseDirectory);
            for (Int32 i = 0; i < 5 && dir != null; i++)
            {
                String candidate = Path.Combine(dir.FullName, "XaocSirck_Runtimes");
                if (Directory.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }
            return Path.Combine(AppContext.BaseDirectory, "XaocSirck_Runtimes");
        }
    }
}
