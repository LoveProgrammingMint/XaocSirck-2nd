using System.Diagnostics;
using System.Linq;
using Gee.External.Capstone;
using Gee.External.Capstone.X86;
using PeNet;
using XaocSirck_Core;
using XaocSirck_Core.Cloud;
using XaocSirck_Core.Core.Queues;
using XaocSirck_Core.Engine;
using XaocSirck_Core.Feature;
using XaocSirck_Core.Feature.Obtain;
using XaocSirck_Core.Inference;
using XaocSirck_Core.Interface.Cloud;
using XaocSirck_Core.Interface.Engine;
using XaocSirck_Core.Interface.Inference;
using XaocSirck_Core.Interface.Settings;
using Charwolf.XSRule;

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

            TestSettings();
            WriteLine();

            TestCloud(serverAddress);
            WriteLine();

            TestInference(modelsDirectory, "C:\\Windows\\System32\\notepad.exe");
            WriteLine();

            TestZeroflows(modelsDirectory, "C:\\Windows\\System32\\notepad.exe");
            WriteLine();

            TestZeroflowsOnly(scanPath, modelsDirectory, maxFiles);
            WriteLine();

            TestScan(scanPath, modelsDirectory, maxFiles);
            WriteLine();

            TestEngine(scanPath, maxFiles);
            WriteLine();

            TestCharwolf("C:\\Windows\\System32\\notepad.exe");
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
            String candidate = Path.Combine(dir.FullName, "XaocSirck", "Models");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return Path.Combine(baseDir, "XaocSirck", "Models");
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

    static void TestSettings()
    {
        WriteLine("[Settings] Testing configuration");
        String configPath = Path.Combine(AppContext.BaseDirectory, "dev_console_config.json");
        App.Settings.Load(configPath);
        WriteLine($"[Settings] Loaded from {configPath}");
        WriteLine($"[Settings] Version: {App.Settings.Config.Version}");
        WriteLine($"[Settings] ParticipateInCoConstruction: {App.Settings.Config.ParticipateInCoConstruction}");
        WriteLine($"[Settings] ModelsDirectory: {App.Settings.Config.ModelsDirectory}");
        WriteLine($"[Settings] EnableGpu: {App.Settings.Config.EnableGpu}");
        WriteLine($"[Settings] FilterByExtension: {App.Settings.Config.FilterByExtension}");
        WriteLine($"[Settings] TargetExtensions: [{String.Join(", ", App.Settings.Config.TargetExtensions)}]");
        WriteLine($"[Settings] BitremalMode: {App.Settings.Config.BitremalMode}");
        WriteLine($"[Settings] ZeroflowMode: {App.Settings.Config.ZeroflowMode}");
        WriteLine($"[Settings] SignatureMode: {App.Settings.Config.SignatureMode}");
        WriteLine($"[Settings] ArchiveMode: {App.Settings.Config.ArchiveMode}");
        WriteLine($"[Settings] DocumentationMode: {App.Settings.Config.DocumentationMode}");
        WriteLine($"[Settings] ShellMode: {App.Settings.Config.ShellMode}");
        WriteLine($"[Settings] CharwolfMode: {App.Settings.Config.CharwolfMode}");

        App.Settings.Update(s =>
        {
            s.Version = "1.0.1";
            s.ParticipateInCoConstruction = true;
            s.EnableGpu = false;
            s.BitremalMode = _Mode_Bitremal.Ot;
            s.ZeroflowMode = _Mode_Zeroflows.Zf;
            s.SignatureMode = _Mode_Signature.Strict;
            s.ArchiveMode = _Mode_Archive.Check;
            s.DocumentationMode = _Mode_Documentation.DocVBA;
            s.ShellMode = _Mode_Shell.Suspicious;
            s.CharwolfMode = _Mode_Charwolf.Core;
            s.FilterByExtension = true;
            s.MaxFiles = 8;
            s.QueueCapacity = 64;
            s.EnableTiming = true;
        });
        WriteLine("[Settings] Updated and saved");

        App.Settings.ReLoad();
        WriteLine($"[Settings] After reload Version: {App.Settings.Config.Version}, ParticipateInCoConstruction: {App.Settings.Config.ParticipateInCoConstruction}");
        WriteLine($"[Settings] After reload BitremalMode: {App.Settings.Config.BitremalMode}, ZeroflowMode: {App.Settings.Config.ZeroflowMode}, SignatureMode: {App.Settings.Config.SignatureMode}");
        WriteLine($"[Settings] After reload ArchiveMode: {App.Settings.Config.ArchiveMode}, DocumentationMode: {App.Settings.Config.DocumentationMode}, ShellMode: {App.Settings.Config.ShellMode}, CharwolfMode: {App.Settings.Config.CharwolfMode}");
        WriteLine($"[Settings] After reload MaxFiles: {App.Settings.Config.MaxFiles}, QueueCapacity: {App.Settings.Config.QueueCapacity}");
        WriteLine($"[Settings] EnableTiming: {App.Settings.Config.EnableTiming}, EnableFeatureCache: {App.Settings.Config.EnableFeatureCache}, EnableLogging: {App.Settings.Config.EnableLogging}");
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

            var textSection = Array.Find(pe.ImageSectionHeaders!, s => s.Name == ".text");
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

    static void TestZeroflowsOnly(String scanPath, String modelsDirectory, Int32 maxFiles)
    {
        if (!Directory.Exists(scanPath))
        {
            WriteLine($"[ZeroflowsOnly] Directory not found: {scanPath}");
            return;
        }

        EngineSettings settings = App.Settings.Config;
        using BitremalInferenceService inference = new(settings.EnableGpu);
        using ZeroflowsInferenceService zeroflows = new(settings.EnableGpu);
        try
        {
            zeroflows.Load(modelsDirectory);
            WriteLine($"[ZeroflowsOnly] Zeroflows models loaded: {zeroflows.IsLoaded}");
        }
        catch (Exception ex)
        {
            WriteLine($"[ZeroflowsOnly] Zeroflows load warning: {ex.Message}");
        }

        using CloudClient cloud = new();
        EngineSettings zeroflowSettings = new()
        {
            EnableTiming = true,
            FilterByExtension = settings.FilterByExtension,
            TargetExtensions = settings.TargetExtensions,
            QueueCapacity = settings.QueueCapacity,
            MaxFiles = settings.MaxFiles,
            Recursive = settings.Recursive
        };
        using MainQueue queue = new(cloud, inference, zeroflows, zeroflowSettings, capacity: zeroflowSettings.QueueCapacity);

        EngineMode mode = new() { Bitremal = _Mode_Bitremal.Disabled, Zeroflow = _Mode_Zeroflows.Zf };
        queue.Start(scanPath, mode, zeroflowSettings.Recursive, maxFiles);

        Stopwatch sw = Stopwatch.StartNew();
        queue.Wait();
        sw.Stop();

        WriteLine($"[ZeroflowsOnly] Completed in {sw.Elapsed.TotalMilliseconds:F3} ms");
        WriteLine($"[ZeroflowsOnly] Results: {queue.Results.Count}");
        if (queue.Timer.Enabled && queue.Timer.Results.Count > 0)
        {
            WriteLine("[ZeroflowsOnly] Phase timing:");
            foreach (KeyValuePair<String, TimeSpan> kv in queue.Timer.Results.OrderByDescending(x => x.Value.Ticks))
                WriteLine($"[ZeroflowsOnly]   {kv.Key}: {kv.Value.TotalMilliseconds:F3} ms");
        }
        Int32 malicious = 0;
        foreach (ScanResult result in queue.Results)
        {
            if (result.IsMalicious)
                malicious++;
        }
        WriteLine($"[ZeroflowsOnly] Malicious: {malicious}");
        foreach (ScanResult result in queue.Results.Take(10))
        {
            String zf = result.ZeroflowsProbabilities != null ? $"[{String.Join(", ", result.ZeroflowsProbabilities)}]" : "null";
            WriteLine($"[ZeroflowsOnly]   {result.FilePath} -> malicious={result.IsMalicious}, zeroflows={zf}");
        }
    }

    static void TestScan(String scanPath, String modelsDirectory, Int32 maxFiles)
    {
        if (!Directory.Exists(scanPath))
        {
            WriteLine($"[Scan] Directory not found: {scanPath}");
            return;
        }

        EngineSettings settings = App.Settings.Config;
        using BitremalInferenceService inference = new(settings.EnableGpu);
        using ZeroflowsInferenceService zeroflows = new(settings.EnableGpu);
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
        using MainQueue queue = new(cloud, inference, zeroflows, settings, capacity: settings.QueueCapacity);

        EngineMode mode = new() { Bitremal = settings.BitremalMode, Zeroflow = settings.ZeroflowMode };
        queue.Start(scanPath, mode, settings.Recursive, maxFiles);

        Stopwatch sw = Stopwatch.StartNew();
        queue.Wait();
        sw.Stop();

        WriteLine($"[Scan] Completed in {sw.Elapsed.TotalMilliseconds:F3} ms");
        WriteLine($"[Scan] Results: {queue.Results.Count}");
        if (queue.Timer.Enabled && queue.Timer.Results.Count > 0)
        {
            WriteLine("[Scan] Phase timing:");
            foreach (KeyValuePair<String, TimeSpan> kv in queue.Timer.Results.OrderByDescending(x => x.Value.Ticks))
                WriteLine($"[Scan]   {kv.Key}: {kv.Value.TotalMilliseconds:F3} ms");
        }
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
            String sig = result.SignatureResult != null ? $"signed={result.SignatureResult.IsSigned}, trusted={result.SignatureResult.IsTrusted}, cloud={result.SignatureResult.CloudResult}" : "null";
            String shell = result.ShellResult != null ? result.ShellResult.Hit.ToString() : "null";
            String arch = result.ArchiveResult != null ? $"archive={result.ArchiveResult.IsArchive}, suspicious={result.ArchiveResult.SuspiciousEntryCount}" : "null";
            String doc = result.DocumentationResult != null ? $"macro={result.DocumentationResult.HasMacro}" : "null";
            WriteLine($"[Scan]   {result.FilePath} -> cache={result.CacheResult}, malicious={result.IsMalicious}, bitremal={bitremal}, zeroflows={zf}, signature={sig}, shell={shell}, archive={arch}, doc={doc}");
        }
    }

    static void TestEngine(String scanPath, Int32 maxFiles)
    {
        if (!Directory.Exists(scanPath))
        {
            WriteLine($"[Engine] Directory not found: {scanPath}");
            return;
        }

        WriteLine("[Engine] Testing Engine pipeline");
        using Engine engine = new();

        engine.Initialize();
        WriteLine($"[Engine] Initialized, Bitremal loaded: {engine.IsBitremalLoaded}, Zeroflows loaded: {engine.IsZeroflowsLoaded}, Charwolf loaded: {engine.IsCharwolfLoaded}");

        Stopwatch sw = Stopwatch.StartNew();
        ScanResult[] results = engine.Scan(scanPath, maxFiles: maxFiles);
        sw.Stop();

        WriteLine($"[Engine] Scan completed in {sw.Elapsed.TotalMilliseconds:F3} ms");
        WriteLine($"[Engine] Results: {results.Length}");
        if (engine.Timer?.Enabled == true && engine.Timer.Results.Count > 0)
        {
            WriteLine("[Engine] Phase timing:");
            foreach (KeyValuePair<String, TimeSpan> kv in engine.Timer.Results.OrderByDescending(x => x.Value.Ticks))
                WriteLine($"[Engine]   {kv.Key}: {kv.Value.TotalMilliseconds:F3} ms");
        }
        Int32 malicious = 0;
        foreach (ScanResult result in results)
        {
            if (result.IsMalicious)
                malicious++;
        }
        WriteLine($"[Engine] Malicious: {malicious}");
        foreach (ScanResult result in results.Take(10))
        {
            String bitremal = result.BitremalProbabilities != null ? $"[{String.Join(", ", result.BitremalProbabilities)}]" : "null";
            String zf = result.ZeroflowsProbabilities != null ? $"[{String.Join(", ", result.ZeroflowsProbabilities)}]" : "null";
            String cw = result.CharwolfResult != null ? $"matched={result.CharwolfResult.Matched}, rules={String.Join(", ", result.CharwolfResult.Matches.Select(m => m.RuleName).Distinct())}" : "null";
            String sig = result.SignatureResult != null ? $"signed={result.SignatureResult.IsSigned}, trusted={result.SignatureResult.IsTrusted}, cloud={result.SignatureResult.CloudResult}" : "null";
            String shell = result.ShellResult != null ? result.ShellResult.Hit.ToString() : "null";
            String arch = result.ArchiveResult != null ? $"archive={result.ArchiveResult.IsArchive}, suspicious={result.ArchiveResult.SuspiciousEntryCount}" : "null";
            String doc = result.DocumentationResult != null ? $"macro={result.DocumentationResult.HasMacro}" : "null";
            WriteLine($"[Engine]   {result.FilePath} -> cache={result.CacheResult}, malicious={result.IsMalicious}, bitremal={bitremal}, zeroflows={zf}, charwolf={cw}, signature={sig}, shell={shell}, archive={arch}, doc={doc}");
        }
    }

    static void TestCharwolf(String filePath)
    {
        WriteLine($"[Charwolf] Testing XSRule engine on {filePath}");
        if (!File.Exists(filePath))
        {
            WriteLine($"[Charwolf] File not found: {filePath}");
            return;
        }

        String rulePath = Path.Combine(AppContext.BaseDirectory, "XSRule_Test.xsr");
        if (!File.Exists(rulePath))
        {
            String repoPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "XSRule_Test.xsr");
            if (File.Exists(repoPath))
                rulePath = Path.GetFullPath(repoPath);
        }

        if (!File.Exists(rulePath))
        {
            WriteLine($"[Charwolf] Rule file not found: {rulePath}");
            return;
        }

        String source = File.ReadAllText(rulePath);
        XsRuleDocument document = new XsRuleParser(source).ParseDocument();
        WriteLine($"[Charwolf] Parsed {document.Rules.Count} rules from reference file");

        String testRule = """
            define Notepad_Test {
                string {
                    "notepad"(head) Wildcard;
                    "MZ"(head) Wildcard;
                    "[4D 5A]"(head) Hex;
                }
                conditions {
                    1 and 2 and 3
                }
            }
            """;

        XsRuleDocument testDocument = new XsRuleParser(testRule).ParseDocument();
        WriteLine($"[Charwolf] Parsed {testDocument.Rules.Count} test rules, strings: {String.Join(", ", testDocument.Rules.Select(r => $"{r.Name}={r.Strings.Count}"))}");

        using CompiledXsRuleDocument compiled = new XsRuleCompiler().Compile(testDocument);
        using CharwolfEngine engine = new(compiled);

        Stopwatch sw = Stopwatch.StartNew();
        IReadOnlyList<CharwolfResult> results = engine.ScanFile(filePath);
        sw.Stop();

        WriteLine($"[Charwolf] Scan completed in {sw.Elapsed.TotalMilliseconds:F3} ms");
        foreach (CharwolfResult result in results)
        {
            WriteLine($"[Charwolf]   {result.RuleName}: matched={result.Matched}, hits={result.Matches.Count}");
            foreach (Charwolf.XSRule.CharwolfMatch match in result.Matches.Take(5))
                WriteLine($"[Charwolf]     string#{match.StringId}");
        }
    }
}
