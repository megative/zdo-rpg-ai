using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Channel;
using ZdoRpgAi.Protocol.Rpc;
using ZdoRpgAi.Server.Bootstrap;

namespace ZdoRpgAi.Server.Http;

public class HttpServer {
    private static readonly ILog Log = Logger.Get<HttpServer>();

    private readonly WebApplication _app;
    private readonly int _maxMessageSize;
    private readonly int _rpcTimeoutMs;
    // Read on each connection: the token can change in the settings while the server runs
    private readonly HttpServerSection _config;
    private readonly List<IChannel> _activeChannels = new();

    public event Action<IChannel>? ClientConnected;

    // Where this run listens; a changed address takes effect with the next run
    public string Host => _config.Host;
    public int Port => _config.Port;

    public int ConnectedClients {
        get {
            lock (_activeChannels) {
                return _activeChannels.Count;
            }
        }
    }

    public HttpServer(HttpServerSection config) {
        _maxMessageSize = config.MaxMessageSize;
        _rpcTimeoutMs = config.RpcTimeoutMs;
        _config = config;

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://{config.Host}:{config.Port}");
        _app = builder.Build();

        _app.UseWebSockets();

        _app.Map("/ping", context => {
            context.Response.ContentType = "text/plain";
            return context.Response.WriteAsync("pong");
        });

        _app.Map("/ws", async context => {
            if (!context.WebSockets.IsWebSocketRequest) {
                context.Response.StatusCode = 400;
                return;
            }

            var clientToken = _config.ClientToken;
            if (clientToken.Length > 0 && context.Request.Headers["X-ZdoRpgAi-Client"] != clientToken) {
                context.Response.StatusCode = 403;
                return;
            }

            var socket = await context.WebSockets.AcceptWebSocketAsync();
            var channel = new WebSocketChannel(socket, _maxMessageSize);

            lock (_activeChannels) {
                _activeChannels.Add(channel);
            }

            Log.Info("Client connected");
            ClientConnected?.Invoke(channel);

            await channel.RunWebSocketAsync();

            lock (_activeChannels) {
                _activeChannels.Remove(channel);
            }

            Log.Info("Client disconnected");
        });
    }

    // More routes, like the web panel's, before the server starts
    public void AddEndpoints(Action<WebApplication> map) => map(_app);

    public async Task StartAsync(CancellationToken ct = default) {
        ct.Register(() => {
            lock (_activeChannels) {
                foreach (var channel in _activeChannels) {
                    channel.Close();
                }
            }
        });
        await _app.RunAsync(ct);
    }
}
