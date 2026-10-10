using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ZdoRpgAi.Core;
using ZdoRpgAi.Repository;
using ZdoRpgAi.Server.Bootstrap;
using ZdoRpgAi.Server.Http;
using ZdoRpgAi.Server.Settings;

namespace ZdoRpgAi.Server.Panel;

// The web panel, a handy editor for the server's YAML config: its page at "/", built into the
// server, and its API under /api. It answers only on this computer, even when the server listens on
// the local network for a client elsewhere (host 0.0.0.0): the config holds the API keys.
public class PanelEndpoints {
    private static readonly ILog Log = Logger.Get<PanelEndpoints>();

    private static readonly Dictionary<string, string> ContentTypes = new() {
        [".html"] = "text/html; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".svg"] = "image/svg+xml",
    };

    private readonly SettingsStore _settings;
    private readonly HttpServer _http;
    private readonly ISaveGameRepository _saveGameRepo;
    private readonly string _version;
    private readonly Action? _restart;
    // Tells this run from the next: the panel waits for it to change after a restart
    private readonly string _started = DateTime.UtcNow.ToString("O");

    public PanelEndpoints(SettingsStore settings, HttpServer http, ISaveGameRepository saveGameRepo, string version, Action? restart) {
        _settings = settings;
        _http = http;
        _saveGameRepo = saveGameRepo;
        _version = version;
        _restart = restart;
    }

    // Changes go through PUT, which a page from another site can't send to localhost; the test takes
    // JSON only, which such a page can't post either
    public void Map(WebApplication app) {
        app.MapGet("/", Local(ctx => ServeAssetAsync(ctx, "index.html")));
        app.MapGet("/panel/{file}", Local(ctx => ServeAssetAsync(ctx, ctx.Request.RouteValues["file"] as string ?? "")));
        app.MapGet("/api/status", Local(GetStatusAsync));
        app.MapGet("/api/settings", Local(GetSettingsAsync));
        app.MapPut("/api/settings", Local(PutSettingsAsync));
        app.MapPost("/api/test", Local(TestConnectionAsync));
        app.MapPost("/api/catalog", Local(GetCatalogAsync));
        app.MapPost("/api/restart", Local(RestartAsync));
    }

    private static RequestDelegate Local(RequestDelegate handler) => async ctx => {
        if (IsLocal(ctx)) {
            await handler(ctx);
            return;
        }
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        await ctx.Response.WriteAsync("The panel opens on the computer where the server runs: http://localhost");
    };

    // A loopback connection that asked for a loopback name; a site whose domain points at 127.0.0.1
    // arrives under its own name
    private static bool IsLocal(HttpContext ctx) {
        var ip = ctx.Connection.RemoteIpAddress;
        if (ip == null) {
            return false;
        }
        if (ip.IsIPv4MappedToIPv6) {
            ip = ip.MapToIPv4();
        }
        return IPAddress.IsLoopback(ip) && ctx.Request.Host.Host is "localhost" or "127.0.0.1" or "::1" or "[::1]";
    }

    private Task GetStatusAsync(HttpContext ctx) =>
        ctx.Response.WriteAsJsonAsync(
            new PanelStatus(_version, _settings.FilePath, _http.ConnectedClients, _saveGameRepo.SaveId, _started),
            PanelJsonContext.Default.PanelStatus);

    private Task GetSettingsAsync(HttpContext ctx) =>
        ctx.Response.WriteAsJsonAsync(
            new SettingsInfo(_settings.GetValues(), _settings.FilePath, LiveSettings.RestartPaths, _settings.PendingRestart()),
            PanelJsonContext.Default.SettingsInfo);

    private async Task PutSettingsAsync(HttpContext ctx) {
        var request = await ReadAsync(ctx, PanelJsonContext.Default.SettingsUpdateRequest);
        if (request == null || request.Edits.Length == 0) {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var result = _settings.Update(request.Edits);
        if (!result.Ok) {
            Log.Warn("Settings from the panel not saved: {Error}", result.Error ?? "?");
        }
        await ctx.Response.WriteAsJsonAsync(result, PanelJsonContext.Default.SettingsUpdate);
    }

    // A connection test with the form's values, the unsaved edits included
    private async Task TestConnectionAsync(HttpContext ctx) {
        if (!ctx.Request.HasJsonContentType()) {
            ctx.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
            return;
        }
        var request = await ReadAsync(ctx, PanelJsonContext.Default.TestRequest);
        if (request == null) {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var config = _settings.Preview(request.Edits ?? [], out var error);
        var result = config == null
            ? new ConnectionTestResult(false, 0, Error: error)
            : await ConnectionTest.RunAsync(config, request.Target);
        await ctx.Response.WriteAsJsonAsync(result, PanelJsonContext.Default.ConnectionTestResult);
    }

    // The models or voices the service offers, asked with the form's values
    private async Task GetCatalogAsync(HttpContext ctx) {
        if (!ctx.Request.HasJsonContentType()) {
            ctx.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
            return;
        }
        var request = await ReadAsync(ctx, PanelJsonContext.Default.CatalogRequest);
        if (request == null) {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var config = _settings.Preview(request.Edits ?? [], out var error);
        var result = config == null
            ? new CatalogResult([], error)
            : await CatalogLookup.RunAsync(config, request.Target, request.Kind);
        await ctx.Response.WriteAsJsonAsync(result, PanelJsonContext.Default.CatalogResult);
    }

    // Restarts the server with the config as the file holds it, so the settings read only at start
    // take effect. Refused when the server couldn't come back up: a broken config, a busy port.
    private async Task RestartAsync(HttpContext ctx) {
        if (!ctx.Request.HasJsonContentType()) {
            ctx.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
            return;
        }

        var next = _settings.Preview([], out var error);
        int? busyPort = null;
        if (next != null && next.HttpServer.Port != _http.Port && PortBusy(next.HttpServer) is { } busy) {
            error = busy;
            busyPort = next.HttpServer.Port;
        }
        if (_restart == null) {
            error = "this server can't restart itself";
        }
        if (next == null || error != null) {
            await ctx.Response.WriteAsJsonAsync(new RestartResult(false, error, busyPort), PanelJsonContext.Default.RestartResult);
            return;
        }

        await ctx.Response.WriteAsJsonAsync(new RestartResult(true, null, next.HttpServer.Port), PanelJsonContext.Default.RestartResult);
        Log.Info("Restart asked for in the panel");
        // Once the reply is out
        _ = Task.Run(async () => {
            await Task.Delay(300);
            _restart!();
        });
    }

    // The port of the next run must be free; this run's port is let go of before the next one starts
    private static string? PortBusy(HttpServerSection next) {
        var address = next.Host == "localhost" ? IPAddress.Loopback : IPAddress.TryParse(next.Host, out var ip) ? ip : IPAddress.Any;
        try {
            var probe = new TcpListener(address, next.Port);
            probe.Start();
            probe.Stop();
            return null;
        }
        catch (SocketException ex) {
            return $"port {next.Port} is busy: {ex.Message}";
        }
    }

    private static async Task<T?> ReadAsync<T>(HttpContext ctx, JsonTypeInfo<T> typeInfo) where T : class {
        try {
            return await JsonSerializer.DeserializeAsync(ctx.Request.Body, typeInfo);
        }
        catch (JsonException) {
            return null;
        }
    }

    private static async Task ServeAssetAsync(HttpContext ctx, string file) {
        var stream = ContentTypes.TryGetValue(Path.GetExtension(file), out var contentType)
            ? typeof(PanelEndpoints).Assembly.GetManifestResourceStream("panel/" + file)
            : null;
        if (stream == null) {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await using (stream) {
            ctx.Response.ContentType = contentType;
            ctx.Response.Headers.CacheControl = "no-cache";
            await stream.CopyToAsync(ctx.Response.Body);
        }
    }
}
