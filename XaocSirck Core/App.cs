using System;
using System.IO;
using LogSystem;
using XaocSirck_Core.Engine;

namespace XaocSirck_Core;

internal static class App
{
    public static Logger Logger { get; } = new(Path.Combine(RuntimeDirectory, "Logs"));
    public static Settings Settings { get; } = new();

    public static String RuntimeDirectory
    {
        get
        {
            DirectoryInfo? dir = new(AppContext.BaseDirectory);
            for (Int32 i = 0; i < 5 && dir != null; i++)
            {
                String candidate = Path.Combine(dir.FullName, "XaocSirck");
                if (Directory.Exists(candidate) && Directory.Exists(Path.Combine(candidate, "Models")))
                    return candidate;
                dir = dir.Parent;
            }
            return Path.Combine(AppContext.BaseDirectory, "XaocSirck");
        }
    }
}
