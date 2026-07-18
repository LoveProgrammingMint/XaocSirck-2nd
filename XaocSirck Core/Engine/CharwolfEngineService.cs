using Cw = Charwolf.XSRule;
using XaocSirck_Core.Interface.Engine;

namespace XaocSirck_Core.Engine;

public sealed class CharwolfEngineService : ICharwolfEngine
{
    private readonly List<Cw.XsRuleDefinition> _rules = [];
    private Cw.CompiledXsRuleDocument? _compiled;
    private Cw.CharwolfEngine? _engine;
    private Boolean _disposed;

    public Boolean IsLoaded => _engine != null;

    public void LoadRules(String rulesDirectory)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(CharwolfEngineService));
        if (!Directory.Exists(rulesDirectory))
        {
            App.Logger.Error($"Charwolf rules directory not found: {rulesDirectory}");
            throw new DirectoryNotFoundException($"Rules directory not found: {rulesDirectory}");
        }

        _rules.Clear();
        _compiled?.Dispose();
        _compiled = null;
        _engine = null;

        String[] files = Directory.EnumerateFiles(rulesDirectory, "*.xsr", SearchOption.AllDirectories).ToArray();
        if (files.Length == 0)
        {
            App.Logger.Warning($"No Charwolf rules found in {rulesDirectory}");
            return;
        }

        Cw.XsRuleCompiler compiler = new();
        foreach (String file in files)
        {
            try
            {
                String source = File.ReadAllText(file);
                Cw.XsRuleDocument document = new Cw.XsRuleParser(source).ParseDocument();
                _rules.AddRange(document.Rules);
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Charwolf rule parse failed: {file}", ex);
            }
        }

        if (_rules.Count == 0)
        {
            App.Logger.Warning("No valid Charwolf rules loaded");
            return;
        }

        try
        {
            Cw.XsRuleDocument combined = new() { Rules = _rules };
            _compiled = compiler.Compile(combined);
            _engine = new Cw.CharwolfEngine(_compiled);
            App.Logger.Info($"Charwolf rules loaded: {files.Length} files, {_rules.Count} rules");
        }
        catch (Exception ex)
        {
            App.Logger.Error("Charwolf rule compilation failed", ex);
            throw;
        }
    }

    public CharwolfScanResult ScanFile(String filePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(CharwolfEngineService));
        CharwolfScanResult result = new() { FilePath = filePath };

        if (_engine == null)
            return result;

        IReadOnlyList<Cw.CharwolfResult> results = _engine.ScanFile(filePath);
        foreach (Cw.CharwolfResult ruleResult in results)
        {
            if (ruleResult.Matched)
            {
                result.Matched = true;
                foreach (Cw.CharwolfMatch match in ruleResult.Matches)
                {
                    result.Matches.Add(new CharwolfMatch
                    {
                        RuleName = match.RuleName,
                        StringId = match.StringId,
                        Part = match.Part.ToString()
                    });
                }
            }
        }

        return result;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _compiled?.Dispose();
            _engine = null;
            _disposed = true;
            App.Logger.Info("CharwolfEngineService disposed");
        }
    }
}
