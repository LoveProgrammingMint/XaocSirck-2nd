using System.Diagnostics;

namespace FeatureExtractor;

internal static class Program
{
    private const Int32 ShardCapacity = 10000;

    private static Int32 Main(String[] args)
    {
        Console.Title = "OverThink ICeZeRoX Feature Extractor";

        if (args.Length > 0 && args[0] == "--worker")
            return Worker.Run(args.AsSpan(1));

        if (args.Length > 0 && args[0] == "--worker-filelist")
            return Worker.RunFileList(args.AsSpan(1));

        String dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
        Directory.CreateDirectory(dataDir);

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== OverThink ICeZeRoX Feature Extractor ===");
            Console.WriteLine("[1] Ingest from folder (multi-process)");
            Console.WriteLine("[2] List database records");
            Console.WriteLine("[3] Exit");
            Console.Write("Select: ");
            String? choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    RunIngest(dataDir);
                    break;
                case "2":
                    RunList(dataDir);
                    break;
                case "3":
                    return 0;
                default:
                    Console.WriteLine("Invalid option.");
                    break;
            }
        }
    }

    private static void RunIngest(String dataDir)
    {
        Console.Write("Enter folder path: ");
        String? folder = Console.ReadLine()?.Trim().Trim('"');
        if (String.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            Console.WriteLine("Folder not found.");
            return;
        }

        Console.Write($"Worker count (default {Environment.ProcessorCount}): ");
        String? wcInput = Console.ReadLine()?.Trim();
        Int32 workerCount;
        if (String.IsNullOrWhiteSpace(wcInput))
            workerCount = Environment.ProcessorCount;
        else if (!Int32.TryParse(wcInput, out workerCount) || workerCount < 1)
            workerCount = Environment.ProcessorCount;
        workerCount = Math.Min(workerCount, 32);

        String[] files = EnumeratePeFilesRecursive(folder);
        if (files.Length == 0)
        {
            Console.WriteLine("No PE files found.");
            return;
        }
        Console.WriteLine($"Found {files.Length} PE files, using {workerCount} workers.");

        String tempDir = Path.Combine(Path.GetTempPath(), "otx_workers", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        String[] workerDbs = DispatchWorkers(files, workerCount, tempDir);
        Console.WriteLine($"All workers finished. Merging into shards (capacity {ShardCapacity}/db)...");

        Int32 merged = DbShard.MergeIntoShards(workerDbs, dataDir, ShardCapacity);
        Console.WriteLine($"Merged {merged} records into {dataDir}");

        foreach (String db in workerDbs)
        {
            try { File.Delete(db); } catch { }
        }
        try { Directory.Delete(tempDir, recursive: true); } catch { }
    }

    private static String[] DispatchWorkers(String[] files, Int32 workerCount, String tempDir)
    {
        String[] workerDbs = new String[workerCount];
        Int32[] chunkSizes = new Int32[workerCount];
        List<Task<Int32>> tasks = new(workerCount);
        Int32 chunkSize = (files.Length + workerCount - 1) / workerCount;

        using ProgressRenderer progress = new(workerCount);

        for (Int32 i = 0; i < workerCount; i++)
        {
            Int32 start = i * chunkSize;
            if (start >= files.Length)
            {
                workerDbs[i] = String.Empty;
                chunkSizes[i] = 0;
                continue;
            }
            Int32 end = Math.Min(start + chunkSize, files.Length);
            String dbPath = Path.Combine(tempDir, $"worker_{i:D3}.db");
            workerDbs[i] = dbPath;
            String[] chunk = files[start..end];
            chunkSizes[i] = chunk.Length;
            progress.Update(i, 0, chunk.Length, "starting");

            String listPath = Path.Combine(tempDir, $"list_{i:D3}.txt");
            File.WriteAllLines(listPath, chunk);

            Int32 wid = i;
            tasks.Add(Task.Run(() => LaunchWorker(dbPath, listPath, chunk.Length, wid, progress)));
        }

        Int32[] results = Task.WhenAll(tasks).GetAwaiter().GetResult();
        Int32 totalOk = results.Sum();
        progress.Finish(totalOk, files.Length);
        Console.WriteLine($"Workers processed {totalOk}/{files.Length} files successfully.");
        return workerDbs.Where(s => !String.IsNullOrEmpty(s) && File.Exists(s)).ToArray();
    }

    private static Int32 LaunchWorker(String dbPath, String listPath, Int32 fileCount, Int32 workerId, ProgressRenderer progress)
    {
        String exe = Environment.ProcessPath!;
        ProcessStartInfo psi = new(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add("--worker-filelist");
        psi.ArgumentList.Add(dbPath);
        psi.ArgumentList.Add(listPath);

        using Process p = Process.Start(psi)!;
        p.OutputDataReceived += (_, e) =>
        {
            if (String.IsNullOrEmpty(e.Data)) return;
            if (e.Data.StartsWith("PROGRESS:", StringComparison.Ordinal))
            {
                ParseProgress(e.Data[9..], out Int32 done, out Int32 total, out String fname);
                progress.Update(workerId, done, total, fname);
            }
        };
        p.ErrorDataReceived += (_, e) =>
        {
            if (!String.IsNullOrEmpty(e.Data))
                Console.Error.WriteLine($"  [W{workerId}!] {e.Data}");
        };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        p.WaitForExit();
        return p.ExitCode == 0 ? fileCount : 0;
    }

    private static void ParseProgress(String s, out Int32 done, out Int32 total, out String fname)
    {
        done = 0; total = 0; fname = "";
        Int32 colon = s.IndexOf(':');
        String fraction = colon > 0 ? s[..colon] : s;
        if (colon > 0) fname = s[(colon + 1)..];
        Int32 slash = fraction.IndexOf('/');
        if (slash > 0 && Int32.TryParse(fraction[..slash], out done) && Int32.TryParse(fraction[(slash + 1)..], out total)) { }
    }

    private static String[] EnumeratePeFilesRecursive(String root)
    {
        List<String> result = new();
        Stack<String> dirs = new();
        dirs.Push(root);
        while (dirs.Count > 0)
        {
            String dir = dirs.Pop();
            String[] subDirs = Array.Empty<String>();
            String[] files = Array.Empty<String>();
            try { subDirs = Directory.GetDirectories(dir); }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException) { }
            catch (IOException) { }
            try { files = Directory.GetFiles(dir); }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException) { }
            catch (IOException) { }

            foreach (String f in files)
            {
                if (IsPeFile(f))
                    result.Add(f);
            }
            foreach (String d in subDirs)
                dirs.Push(d);
        }
        return result.ToArray();
    }

    private static Boolean IsPeFile(String path)
    {
        try
        {
            using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            Span<Byte> hdr = stackalloc Byte[64];
            if (fs.Read(hdr) < 64) return false;
            if (hdr[0] != 0x4D || hdr[1] != 0x5A) return false;
            Int32 peOffset = BitConverter.ToInt32(hdr[0x3C..0x40]);
            if (peOffset <= 0 || peOffset + 4 > 4096) return false;
            fs.Position = peOffset;
            Span<Byte> sig = stackalloc Byte[4];
            return fs.Read(sig) == 4 && sig[0] == 0x50 && sig[1] == 0x45 && sig[2] == 0 && sig[3] == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void RunList(String dataDir)
    {
        String[] dbs = Directory.Exists(dataDir)
            ? Directory.EnumerateFiles(dataDir, "*DATA.db", SearchOption.TopDirectoryOnly).OrderBy(s => s).ToArray()
            : [];
        if (dbs.Length == 0)
        {
            Console.WriteLine("No data shards found.");
            return;
        }
        Console.WriteLine($"{"shard",-16} {"records",8}  {"size",10}");
        Console.WriteLine(new String('-', 40));
        Int32 total = 0;
        foreach (String db in dbs)
        {
            Int32 cnt = DbShard.CountRecords(db);
            Int64 sz = new FileInfo(db).Length;
            Console.WriteLine($"{Path.GetFileName(db),-16} {cnt,8}  {sz,10:N0}");
            total += cnt;
        }
        Console.WriteLine($"Total records: {total}");
    }
}
