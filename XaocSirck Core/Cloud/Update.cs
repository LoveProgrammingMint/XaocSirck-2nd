using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace XaocSirck_Core.Cloud;

public sealed class UpdateClient : IDisposable
{
    private readonly HttpClient _client = new();
    private readonly string _packagePath;
    private readonly string _extractPath;
    private readonly string _updaterPath;

    public UpdateClient(string? packagePath = null, string? extractPath = null, string? updaterPath = null)
    {
        _packagePath = packagePath ?? "./update_temp/update_pkg.izxs";
        _extractPath = extractPath ?? "./update_temp/decompressed";
        _updaterPath = updaterPath ?? "./Updater.exe";
    }

    public async Task<string?> CheckAsync(Uri server, string currentVersion, CancellationToken cancellationToken = default)
    {
        var url = new Uri(server, "version");
        try
        {
            var response = await _client.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            var latest = response.Trim();
            if (string.IsNullOrEmpty(latest)) return null;

            if (!Version.TryParse(currentVersion, out var current)) return latest;
            if (!Version.TryParse(latest, out var remote)) return latest;

            return remote > current ? latest : null;
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"Failed to check version from {url}", ex);
        }
    }

    public async Task DownloadAsync(Uri server, CancellationToken cancellationToken = default)
    {
        var url = new Uri(server, "download");
        try
        {
            var packageDir = Path.GetDirectoryName(_packagePath);
            if (!string.IsNullOrEmpty(packageDir)) Directory.CreateDirectory(packageDir);

            await using var stream = await _client.GetStreamAsync(url, cancellationToken).ConfigureAwait(false);
            await using var file = File.Create(_packagePath);
            await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"Failed to download package from {url}", ex);
        }
    }

    public void Apply(string? serviceName = null)
    {
        if (Directory.Exists(_extractPath)) Directory.Delete(_extractPath, true);
        Directory.CreateDirectory(_extractPath);

        ZipFile.ExtractToDirectory(_packagePath, _extractPath, overwriteFiles: true);

        var updaterFullPath = Path.GetFullPath(_updaterPath);
        if (!File.Exists(updaterFullPath))
            throw new FileNotFoundException($"Updater not found at {updaterFullPath}", updaterFullPath);

        var arguments = serviceName is null ? _extractPath : $"{_extractPath} {serviceName}";
        Process.Start(new ProcessStartInfo
        {
            UseShellExecute = true,
            FileName = updaterFullPath,
            Arguments = arguments,
        });
    }

    public void Dispose() => _client.Dispose();
}
