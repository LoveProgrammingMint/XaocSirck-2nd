using System;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using XaocSirck_Core.Cloud;

namespace XaocSirck2Service;

public class Worker(ILogger<Worker> logger, IConfiguration configuration, IUpdateTrigger trigger, IHostApplicationLifetime lifetime) : BackgroundService
{
    private readonly UpdateClient _updateClient = new();
    private readonly HttpClient _notifyClient = new();
    private readonly string _currentVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Update checker started. CurrentVersion={Version}", _currentVersion);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (TryGetUpdateServer(out var server))
                {
                    await CheckAndUpdateAsync(server, stoppingToken);
                }

                logger.LogInformation("Waiting for next update check cycle.");
                await Task.WhenAny(
                    Task.Delay(TimeSpan.FromMinutes(1), stoppingToken),
                    trigger.WaitAsync(stoppingToken));
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Update checker stopping.");
        }
    }

    private bool TryGetUpdateServer(out Uri server)
    {
        server = null!;
        var serverUrl = configuration["UpdateServer"] ?? "http://101.132.25.27:5100/api/update";
        if (!serverUrl.EndsWith('/')) serverUrl += "/";
        logger.LogInformation("Configured UpdateServer={Url}", serverUrl);

        if (Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri))
        {
            server = uri;
            return true;
        }

        logger.LogError("Invalid UpdateServer: {Url}", serverUrl);
        return false;
    }

    private async Task CheckAndUpdateAsync(Uri server, CancellationToken cancellationToken)
    {
        try
        {
            var versionUrl = new Uri(server, "version");
            logger.LogInformation("Checking update. CurrentVersion={Current} VersionUrl={Url}", _currentVersion, versionUrl);

            var latest = await _updateClient.CheckAsync(server, _currentVersion, cancellationToken);
            if (latest is null)
            {
                logger.LogInformation("No update available. CurrentVersion={Current}", _currentVersion);
                return;
            }

            logger.LogInformation("Update available. CurrentVersion={Current} LatestVersion={Latest}", _currentVersion, latest);
            await NotifyAppAsync();

            var downloadUrl = new Uri(server, "download");
            logger.LogInformation("Downloading update package from {Url} to {Path}", downloadUrl, "./update_temp/update_pkg.izxs");
            await _updateClient.DownloadAsync(server, cancellationToken);

            logger.LogInformation("Applying update. UpdaterPath={Updater} Arguments={Args}", "./Updater.exe", "./update_temp/decompressed XaocSirck2Service");
            _updateClient.Apply("XaocSirck2Service");

            logger.LogInformation("Update applied. Shutting down service for replacement.");
            lifetime.StopApplication();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Update check failed");
        }
    }

    private async Task NotifyAppAsync()
    {
        const string notifyUrl = "http://127.0.0.1:51235/notify/updating";
        try
        {
            logger.LogInformation("Notifying desktop app: {Url}", notifyUrl);
            using var response = await _notifyClient.PostAsync(notifyUrl, null, CancellationToken.None);
            response.EnsureSuccessStatusCode();
            logger.LogInformation("Desktop app notified successfully.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to notify desktop app at {Url}", notifyUrl);
        }
    }
}
