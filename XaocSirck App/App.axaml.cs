using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using XaocSirck_App.Services;

namespace XaocSirck_App;

public partial class App : Application
{
    private TrayIcon? _trayIcon;
    private WebApplication? _notifyApi;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        InitializeTrayIcon();
        StartNotificationApi();

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "avalonia-logo.ico");
        using var stream = File.OpenRead(iconPath);
        var icon = new WindowIcon(stream);

        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) => Shutdown();

        _trayIcon = new TrayIcon
        {
            Icon = icon,
            ToolTipText = "XaocSirck",
            Menu = new NativeMenu { Items = { exitItem } },
            IsVisible = true,
        };
    }

    private void StartNotificationApi()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:51235");
        _notifyApi = builder.Build();

        _notifyApi.MapPost("/notify/updating", () =>
        {
            NotificationService.ShowUpdatingNotification();
            return Results.Ok();
        });

        Task.Run(() => _notifyApi.Run());
    }

    private void Shutdown()
    {
        _notifyApi?.StopAsync().GetAwaiter().GetResult();
        _notifyApi?.DisposeAsync().GetAwaiter().GetResult();
        _trayIcon?.Dispose();
        _trayIcon = null;
        Environment.Exit(0);
    }
}
