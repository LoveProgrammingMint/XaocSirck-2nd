using System.Diagnostics;
using System.ServiceProcess;

static void Log(string message)
{
    File.AppendAllText("updater.log", $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
}

try
{
    if (args.Length < 2)
    {
        Log("Insufficient arguments");
        return 1;
    }

    string extractPath = args[0];
    string serviceName = args[1];

    Directory.SetCurrentDirectory(extractPath);

    Log($"Updater started. extractPath={extractPath} serviceName={serviceName}");
    Log($"WorkingDirectory={Environment.CurrentDirectory}");

    string listFile = Path.Combine(extractPath, "update_list.updatelist");
    Log($"Reading list: {listFile}");

    if (!File.Exists(listFile))
    {
        Log("List file not found");
        return 1;
    }

    string[] lines = File.ReadAllLines(listFile);
    Log($"List entries: {lines.Length}");

    bool anyMoveFailed = false;
    foreach (string line in lines)
    {
        if (string.IsNullOrWhiteSpace(line)) continue;

        string[] parts = line.Split(';', 2);
        if (parts.Length != 2)
        {
            Log($"Invalid line: {line}");
            continue;
        }

        string src = parts[0].Trim();
        string dst = parts[1].Trim();

        Log($"Entry: src=[{src}] dst=[{dst}]");

        try
        {
            string srcFull = Path.GetFullPath(src);
            string dstFull = Path.GetFullPath(dst);

            Log($"srcFull={srcFull} exists={File.Exists(srcFull)}");
            Log($"dstFull={dstFull}");

            if (File.Exists(dstFull))
            {
                File.Delete(dstFull);
                Log("Deleted existing dst");
            }

            File.Move(srcFull, dstFull);
            Log("Move succeeded");
        }
        catch (Exception ex)
        {
            anyMoveFailed = true;
            Log($"Move failed: {ex.Message}");
        }
    }

    if (anyMoveFailed)
    {
        Log("One or more file moves failed; aborting service restart");
        return 1;
    }

    try
    {
        using var controller = new ServiceController(serviceName);
        if (controller.Status != ServiceControllerStatus.Running)
        {
            controller.Start();
            controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            Log("Service started");
        }
        else
        {
            Log("Service already running");
        }
    }
    catch (Exception ex)
    {
        Log($"Service start failed: {ex.Message}");
        return 1;
    }

    Log("Updater finished");
    return 0;
}
catch (Exception ex)
{
    Log($"Updater crashed: {ex}");
    return 1;
}
