using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;

namespace XaocSirck2Service;

public class Program
{
    private static readonly object SettingsWriteLock = new();

    public static void Main(string[] args)
    {
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls("http://127.0.0.1:51234");
        builder.Services.AddWindowsService();
        builder.Services.AddSingleton<IUpdateTrigger, UpdateTrigger>();
        builder.Services.AddHostedService<Worker>();

        var app = builder.Build();

        app.MapGet("/settings", (IConfiguration config)
            => Results.Ok(new { UpdateServer = config["UpdateServer"] ?? string.Empty }));

        app.MapPost("/settings", (SettingsRequest req, IHostEnvironment env) =>
        {
            var path = Path.Combine(env.ContentRootPath, "appsettings.json");
            lock (SettingsWriteLock)
            {
                var json = File.Exists(path)
                    ? JsonNode.Parse(File.ReadAllText(path)) ?? new JsonObject()
                    : new JsonObject();

                json["UpdateServer"] = req.UpdateServer;
                File.WriteAllText(path, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }
            return Results.Ok();
        });

        app.MapPost("/update/check", (IUpdateTrigger trigger) =>
        {
            trigger.Trigger();
            return Results.Accepted();
        });

        app.Run();
    }
}

public sealed record SettingsRequest(string UpdateServer);
