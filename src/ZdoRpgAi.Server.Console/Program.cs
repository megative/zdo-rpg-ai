using ZdoRpgAi.Core;
using ZdoRpgAi.Server.App;
using ZdoRpgAi.Server.Bootstrap;
using ZdoRpgAi.Server.Console;
using ZdoRpgAi.Util;

var parser = new CommandLineArgsParser("Zdo RPG AI Server", BuildInfo.Version);
parser.Add("-c", "--config", "Path to YAML config file", defaultValue: "server-config.yaml");
parser.AddFlag("--no-panel", "Don't serve the web settings panel");

var parsed = parser.Parse(args);
var configPath = parsed.Get("--config")!;
var panel = parsed.Get("--no-panel") is not ("true" or "1" or "yes");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => {
    e.Cancel = true;
    cts.Cancel();
};

// The panel's restart button ends a run and the next one reads the config afresh, so the settings
// read only at start (port, databases, logs) take effect without leaving the program
while (true) {
    var config = ConfigParser.ParseYamlFile(configPath, ServerConfigJsonContext.Default.ServerConfig);
    ServerBootstrap.ResolvePaths(config, configPath);
    Logger.Configure(config.Log);
    Logger.Get<ServerApplication>().Info("Server {Version}", BuildInfo.Version);

    using var run = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
    var restart = false;
    try {
        using var app = ServerBootstrap.Create(config, configPath, ServerConfigJsonContext.Default.ServerConfig, BuildInfo.Version, panel,
            restart: () => {
                restart = true;
                run.Cancel();
            });
        await app.RunAsync(run.Token);
    }
    catch (OperationCanceledException) {
        // Normal shutdown
    }
    catch (Exception ex) {
        Logger.Get<ServerApplication>().Error(ex, "Fatal error");
        Logger.Flush();
        throw;
    }

    if (!restart || cts.IsCancellationRequested) {
        break;
    }
    Logger.Get<ServerApplication>().Info("Restarting with the new settings");
    // The log file is reopened, and rotated, by the next run
    Logger.Flush();
}
