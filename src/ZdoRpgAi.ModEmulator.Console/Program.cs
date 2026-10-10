using ZdoRpgAi.Core;
using ZdoRpgAi.ModEmulator;
using ZdoRpgAi.ModEmulator.Console;
using ZdoRpgAi.ModEmulator.Scenarios;
using ZdoRpgAi.Util;

var parser = new CommandLineArgsParser("Zdo RPG AI Mod Emulator", BuildInfo.Version);
parser.Add("--host", "Host to listen on", defaultValue: "localhost");
parser.Add("-p", "--port", "Port to listen on", defaultValue: "8081");
parser.Add("-s", "--scenarios", "Play the scenarios in this folder or file with the models of --config, then exit", defaultValue: null);
parser.Add("-c", "--config", "Server config with the models that play the scenarios", defaultValue: "server-config.yaml");
parser.Add("--only", "Play only the scenarios whose file or name contains this");
parser.Add("--report", "Also write the scenario results to this Markdown file");

var parsed = parser.Parse(args);
var scenarioPath = parsed.Get("--scenarios");
if (scenarioPath != null) {
    Environment.Exit(await RunScenariosAsync(scenarioPath, parsed.Get("--config")!, parsed.Get("--only"), parsed.Get("--report")));
}

var host = parsed.Get("--host")!;
var port = int.Parse(parsed.Get("--port")!);

Logger.Configure(new LogConfig { ConsoleLevel = LogLevel.Debug });
var log = Logger.Get<EmulatorServer>();
log.Info("Mod Emulator {Version}", BuildInfo.Version);

var world = new EmulatedWorld(SeydaNeen.Create());
log.Info("World: {Cell}", world.PlayerCell);
PrintNpcs();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => {
    e.Cancel = true;
    cts.Cancel();
};

var server = new EmulatorServer(host, port, world);
log.Info("Listening on {Host}:{Port}", host, port);

var serverTask = server.RunAsync(cts.Token);

// Interactive command loop
var input = new ConsoleInput();
_ = Task.Run(async () => {
    await Task.Delay(500, cts.Token).ConfigureAwait(false);
    PrintHelp();

    while (!cts.Token.IsCancellationRequested) {
        var line = await Task.Run(() => input.ReadLine(), cts.Token).ConfigureAwait(false);
        if (line == null) break;

        var parts = line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) continue;

        var session = server.Session;
        if (session == null && parts[0] is not ("help" or "npcs" or "q")) {
            log.Warn("No client connected");
            continue;
        }

        switch (parts[0].ToLowerInvariant()) {
            case "target" or "t":
                if (parts.Length < 2) {
                    log.Info("Usage: target <npc_id | clear>");
                } else {
                    session!.SetTarget(parts[1] == "clear" ? null : parts[1]);
                }
                break;
            case "say" or "s":
                if (parts.Length < 2) {
                    log.Info("Usage: say <text>");
                } else {
                    session!.Say(parts[1]);
                }
                break;
            case "go" or "g":
                if (parts.Length < 2) {
                    log.Info("Usage: go <cell name>");
                } else {
                    session!.MoveTo(parts[1]);
                }
                break;
            case "npcs":
                PrintNpcs();
                break;
            case "help":
                PrintHelp();
                break;
            case "q":
                cts.Cancel();
                break;
            default:
                log.Warn("Unknown command: {Cmd}. Type 'help' for commands.", parts[0]);
                break;
        }
    }
}, cts.Token);

try {
    await serverTask;
}
catch (OperationCanceledException) {
    // Normal shutdown
}

void PrintNpcs() {
    log.Info("NPCs:");
    foreach (var npc in world.Npcs) {
        log.Info("  {ObjectId}: {Name} ({Race}, {Sex}) in {Cell}", npc.ObjectId, npc.Name, npc.Race, npc.Sex, world.CellOf(npc.ObjectId) ?? "?");
    }
}

void PrintHelp() {
    log.Info("Commands:");
    log.Info("  say <text>       - Send player speech text to server (alias: s)");
    log.Info("  target <npc_id>  - Set player target NPC (alias: t)");
    log.Info("  target clear     - Clear player target");
    log.Info("  go <cell>        - Walk into another cell; followers come along (alias: g)");
    log.Info("  npcs             - List available NPCs");
    log.Info("  help             - Show this help");
    log.Info("  q                - Quit");
}

// Plays the scenarios against the configured models; 0 when all pass, 1 when some fail, 2 when
// they can't run
static async Task<int> RunScenariosAsync(string path, string configPath, string? only, string? reportPath) {
    Logger.Configure(new LogConfig { ConsoleLevel = LogLevel.Error });
    var files = Directory.Exists(path)
        ? Directory.GetFiles(path, "*.yaml").Order().ToArray()
        : File.Exists(path) ? [path] : [];
    if (files.Length == 0) {
        Console.Error.WriteLine($"No scenarios in {path}");
        return 2;
    }

    var runner = ScenarioRunner.FromServerConfig(configPath, out var provider);
    if (provider == "dummy") {
        Console.Error.WriteLine("The main model in the config is 'dummy': scenarios need a real one to mean anything.");
    }
    runner.Progress += Console.WriteLine;

    var results = new List<ScenarioResult>();
    foreach (var file in files) {
        if (!ScenarioRunner.TryLoad(File.ReadAllText(file), out var scenario, out var error)) {
            Console.Error.WriteLine($"Skipping {file}: {error}");
            continue;
        }
        if (only != null && !file.Contains(only, StringComparison.OrdinalIgnoreCase)
            && !scenario!.Name.Contains(only, StringComparison.OrdinalIgnoreCase)) {
            continue;
        }
        var problems = runner.Check(scenario!);
        if (problems.Count > 0) {
            Console.Error.WriteLine($"Skipping {file}: {string.Join("; ", problems)}");
            continue;
        }
        results.Add(await runner.RunAsync(scenario!, file));
    }

    var passed = results.Count(r => r.Passed);
    Console.WriteLine();
    Console.WriteLine($"{passed} of {results.Count} scenarios passed");
    if (reportPath != null) {
        await File.WriteAllTextAsync(reportPath, ScenarioReport.ToMarkdown(results, $"Main model provider: {provider}"));
        Console.WriteLine($"Report: {Path.GetFullPath(reportPath)}");
    }
    return results.Count > 0 && passed == results.Count ? 0 : 1;
}
