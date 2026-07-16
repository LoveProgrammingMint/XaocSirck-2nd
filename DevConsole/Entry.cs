using System.Diagnostics;
using Gee.External.Capstone;
using Gee.External.Capstone.X86;
using PeNet;
using XaocSirck_Core.Cloud;
using XaocSirck_Core.Core.Queues;
using XaocSirck_Core.Feature;
using XaocSirck_Core.Feature.Obtain;
using XaocSirck_Core.Inference;
using XaocSirck_Core.Interface.Engine;

namespace DevConsole;

internal class Entry
{
    private static StreamWriter? _log;

    static void Main(String[] args)
    {
        String scanPath = args.Length > 0 ? args[0] : "C:\\Windows\\System32\\drivers\\etc";
        String? logPath = args.Length > 1 ? args[1] : null;
        Int32 maxFiles = args.Length > 2 && Int32.TryParse(args[2], out Int32 parsed) ? parsed : 16;
        String? serverAddress = args.Length > 3 ? args[3] : null;

        if (logPath != null)
        {
            _log = new StreamWriter(logPath, false, System.Text.Encoding.UTF8);
            _log.AutoFlush = true;
            Console.SetError(_log);
        }

        try
        {
            WriteLine("DevConsole integration test start");
            WriteLine($"Scan path: {scanPath}");
            WriteLine();

            TestDispose();
            WriteLine();

            String modelsDirectory = FindModelsDirectory();
            WriteLine($"Models directory: {modelsDirectory}");

            TestCloud(serverAddress);
            WriteLine();

            TestInference(modelsDirectory, "C:\\Windows\\System32\\notepad.exe");
            WriteLine();

            TestZeroflows(modelsDirectory, "C:\\Windows\\System32\\notepad.exe");
            WriteLine();

            TestScan(scanPath, modelsDirectory, maxFiles);
        WriteLine();

            WriteLine("DevConsole integration test completed successfully");
        }
        catch (Exception ex)
        {
            WriteLine($"[Error] {ex}");
        }
        finally
        {
            _log?.Dispose();
        }
    }

    static void WriteLine(String message = "")
    {
        Console.WriteLine(message);
        _log?.WriteLine(message);
    }

    static String FindModelsDirectory()
    {
        String baseDir = AppContext.BaseDirectory;
        DirectoryInfo? dir = new(baseDir);
        for (Int32 i = 0; i < 5 && dir != null; i++)
        {
            String candidate = Path.Combine(dir.FullName, "XaocSirck_Runtimes", "Models");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return Path.Combine(baseDir, "XaocSirck_Runtimes", "Models");
    }

    static void TestDispose()
    {
        WriteLine("[Dispose] Testing repeated disposal");

        CloudClient cloud = new();
        cloud.Dispose();
        cloud.Dispose();
        WriteLine("[Dispose] CloudClient disposed twice without error");

        UpdateClient update = new();
        update.Dispose();
        update.Dispose();
        WriteLine("[Dispose] UpdateClient disposed twice without error");

        BitremalInferenceService inference = new();
        inference.Dispose();
        inference.Dispose();
        WriteLine("[Dispose] BitremalInferenceService disposed twice without error");

        ZeroflowsInferenceService zeroflows = new();
        zeroflows.Dispose();
        zeroflows.Dispose();
        WriteLine("[Dispose] ZeroflowsInferenceService disposed twice without error");

        using CloudClient cloud2 = new();
        using BitremalInferenceService inference2 = new();
        using ZeroflowsInferenceService zeroflows2 = new();
        using MainQueue queue = new(cloud2, inference2, zeroflows2, 16);
        queue.Dispose();
        WriteLine("[Dispose] MainQueue disposed without error");
    }

    static void TestCloud(String? serverAddress)
    {
        using CloudClient cloud = new();
        if (String.IsNullOrEmpty(serverAddress))
        {
            WriteLine("[Cloud] No server address provided, using disconnected mode");
            return;
        }

        try
        {
            cloud.Connect(serverAddress);
            WriteLine($"[Cloud] Connected to {serverAddress}: {cloud.IsConnected}");
            Byte[] sha256 = new Byte[32];
            Random.Shared.NextBytes(sha256);
            CloudCacheResult result = cloud.QueryCache(sha256);
            WriteLine($"[Cloud] Cache query result: {result}");
        }
        catch (Exception ex)
        {
            WriteLine($"[Cloud] Connection/query failed: {ex.Message}");
        }
    }

    static void TestALO(String filePath)
    {
        WriteLine($"[ALO] Testing AssemblyListObtain for {filePath}");
        try
        {
            PeFile pe = new(filePath);
            SharePool pool = new() { FilePath = filePath, Pe = pe };
            using AssemblyListObtain alo = new();
            alo.Set(pool);
            alo.Obtain();
            WriteLine($"[ALO] Result pointer: {alo.GetResult()}");
        }
        catch (Exception ex)
        {
            WriteLine($"[ALO] Failed: {ex}");
        }
    }

    static void TestInference(String modelsDirectory, String filePath)
    {
        WriteLine($"[Inference] Direct inference test: {filePath}");
        if (!File.Exists(filePath))
        {
            WriteLine($"[Inference] File not found: {filePath}");
            return;
        }

        try
        {
            PeFile pe = new(filePath);
            WriteLine($"[Inference] PE loaded: machine={pe.ImageNtHeaders?.FileHeader.Machine}, sections={pe.ImageSectionHeaders?.Length}, imageBase={pe.ImageNtHeaders?.OptionalHeader.ImageBase}");
            if (pe.ImageSectionHeaders != null)
                foreach (var s in pe.ImageSectionHeaders)
                    WriteLine($"[Inference]   Section: {s.Name}, rawSize={s.SizeOfRawData}, chars={Convert.ToUInt32(s.Characteristics):X8}");

            var textSection = Array.Find(pe.ImageSectionHeaders, s => s.Name == ".text");
            if (textSection != null)
            {
                Byte[] data = new Byte[textSection.SizeOfRawData];
                using (FileStream fs = new(filePath, FileMode.Open, FileAccess.Read))
                {
                    fs.Seek(textSection.PointerToRawData, SeekOrigin.Begin);
                    fs.ReadExactly(data);
                }
                Int64 baseAddr = (Int64)(pe.ImageNtHeaders!.OptionalHeader.ImageBase + textSection.VirtualAddress);
                using CapstoneX86Disassembler disassembler = CapstoneDisassembler.CreateX86Disassembler(X86DisassembleMode.Bit64);
                X86Instruction[] instructions = disassembler.Disassemble(data, baseAddr);
                WriteLine($"[Inference] Capstone x64 instructions: {instructions.Length}");
            }
        }
        catch (Exception ex)
        {
            WriteLine($"[Inference] PE diagnostic failed: {ex.Message}");
        }

        using BitremalInferenceService inference = new();
        try
        {
            inference.Load(modelsDirectory);
        }
        catch (Exception ex)
        {
            WriteLine($"[Inference] Load failed: {ex}");
            return;
        }

        using Features features = new();
        try
        {
            EngineMode mode = new() { Bitremal = _Mode_Bitremal.Ot };
            features.Set(filePath, mode);
            FeaturesStruct fs = features.Execute(null);
            WriteLine($"[Inference] Feature pointers: RB={fs.RB}, AL={fs.AL}, IT={fs.IT}, EM={fs.EM}");
            Single[] probs = inference.InferOverThink(fs.AL, fs.RB, fs.IT, fs.EM);
            WriteLine($"[Inference] Probabilities: [{String.Join(", ", probs)}]");
        }
        catch (Exception ex)
        {
            WriteLine($"[Inference] Failed: {ex}");
        }
    }

    static void TestZeroflows(String modelsDirectory, String filePath)
    {
        WriteLine($"[Zeroflows] Direct inference test: {filePath}");
        if (!File.Exists(filePath))
        {
            WriteLine($"[Zeroflows] File not found: {filePath}");
            return;
        }

        using ZeroflowsInferenceService inference = new();
        try
        {
            inference.Load(modelsDirectory);
            WriteLine($"[Zeroflows] Models loaded: {inference.IsLoaded}");
        }
        catch (Exception ex)
        {
            WriteLine($"[Zeroflows] Load failed: {ex}");
            return;
        }

        using Features features = new();
        try
        {
            EngineMode mode = new() { Zeroflow = _Mode_Zeroflows.Zf };
            features.Set(filePath, mode);
            FeaturesStruct fs = features.Execute(null);
            WriteLine($"[Zeroflows] Feature pointer: {fs.Zeroflow}");
            Single[] probs = inference.Infer(fs.Zeroflow);
            WriteLine($"[Zeroflows] Probabilities: [{String.Join(", ", probs)}]");
        }
        catch (Exception ex)
        {
            WriteLine($"[Zeroflows] Failed: {ex}");
        }
    }

    static void TestScan(String scanPath, String modelsDirectory, Int32 maxFiles)
    {
        if (!Directory.Exists(scanPath))
        {
            WriteLine($"[Scan] Directory not found: {scanPath}");
            return;
        }

        using BitremalInferenceService inference = new();
        using ZeroflowsInferenceService zeroflows = new();
        try
        {
            inference.Load(modelsDirectory);
            WriteLine($"[Inference] Bitremal models loaded: {inference.IsLoaded}");
            foreach (KeyValuePair<String, OnnxModel> kv in inference.Models.GetAll())
                WriteLine($"[Inference]   {kv.Key}: loaded={kv.Value.IsLoaded}, input={kv.Value.InputName} ({String.Join(",", kv.Value.InputShape)}), output={kv.Value.OutputName} ({String.Join(",", kv.Value.OutputShape)})");
        }
        catch (Exception ex)
        {
            WriteLine($"[Inference] Bitremal load warning: {ex.Message}");
        }

        try
        {
            zeroflows.Load(modelsDirectory);
            WriteLine($"[Inference] Zeroflows models loaded: {zeroflows.IsLoaded}");
            foreach (KeyValuePair<String, OnnxModel> kv in zeroflows.Models.GetAll())
                WriteLine($"[Inference]   {kv.Key}: loaded={kv.Value.IsLoaded}, input={kv.Value.InputName} ({String.Join(",", kv.Value.InputShape)}), output={kv.Value.OutputName} ({String.Join(",", kv.Value.OutputShape)})");
        }
        catch (Exception ex)
        {
            WriteLine($"[Inference] Zeroflows load warning: {ex.Message}");
        }

        using CloudClient cloud = new();
        using MainQueue queue = new(cloud, inference, zeroflows, capacity: 64);

        EngineMode mode = new() { Bitremal = _Mode_Bitremal.Ot, Zeroflow = _Mode_Zeroflows.Zf };
        queue.Start(scanPath, mode, maxFiles: maxFiles);

        Stopwatch sw = Stopwatch.StartNew();
        queue.Wait();
        sw.Stop();

        WriteLine($"[Scan] Completed in {sw.Elapsed.TotalMilliseconds:F3} ms");
        WriteLine($"[Scan] Results: {queue.Results.Count}");
        Int32 malicious = 0;
        foreach (ScanResult result in queue.Results)
        {
            if (result.IsMalicious)
                malicious++;
        }
        WriteLine($"[Scan] Malicious: {malicious}");
        foreach (ScanResult result in queue.Results.Take(10))
        {
            String bitremal = result.BitremalProbabilities != null ? $"[{String.Join(", ", result.BitremalProbabilities)}]" : "null";
            String zf = result.ZeroflowsProbabilities != null ? $"[{String.Join(", ", result.ZeroflowsProbabilities)}]" : "null";
            WriteLine($"[Scan]   {result.FilePath} -> cache={result.CacheResult}, malicious={result.IsMalicious}, bitremal={bitremal}, zeroflows={zf}");
        }
    }
}
