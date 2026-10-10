using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using ZdoRpgAi.Core;
using ZdoRpgAi.Server.Bootstrap;
using ZdoRpgAi.Util;

namespace ZdoRpgAi.Server.Settings;

// A change to one setting; a null value removes it from the file, so it takes its default
public record SettingsEdit(string Path, JsonNode? Value);

public record SettingsUpdate(bool Ok, string? Error, string[] Applied, string[] RestartRequired) {
    public static SettingsUpdate Failed(string error) => new(false, error, [], []);
}

// The server config as its YAML file holds it. The web panel changes single values in the file, so
// it stays the place to edit by hand, comments and all; edits made by hand while the server runs
// are picked up too.
public class SettingsStore : IDisposable {
    private static readonly ILog Log = Logger.Get<SettingsStore>();
    // Editors save in several steps: wait for the last one
    private static readonly TimeSpan ReloadDelay = TimeSpan.FromMilliseconds(500);

    private readonly string _path;
    private readonly JsonTypeInfo<ServerConfig> _typeInfo;
    private readonly LiveSettings _live;
    private readonly object _lock = new();
    private ServerConfig _current;
    // What the server started with: settings read only at start differ from it until a restart
    private readonly JsonObject _startup;
    // The file as last read or written, to tell our own writes from edits by hand
    private string _text;
    private FileSystemWatcher? _watcher;
    private Timer? _reloadTimer;

    public SettingsStore(string path, JsonTypeInfo<ServerConfig> typeInfo, ServerConfig current, LiveSettings live) {
        _path = Path.GetFullPath(path);
        _typeInfo = typeInfo;
        _current = current;
        _live = live;
        _startup = ToJson(current);
        _text = File.ReadAllText(_path);
    }

    public string FilePath => _path;

    // The settings in effect, defaults included, named as in the file
    public JsonObject GetValues() {
        lock (_lock) {
            return ToJson(_current);
        }
    }

    // Settings changed since the start that only a restart puts into effect
    public string[] PendingRestart() {
        lock (_lock) {
            var current = ToJson(_current);
            return [.. LiveSettings.RestartPaths.Where(p => Differs(_startup, current, p))];
        }
    }

    // The settings as they would be with the edits, nothing written: for testing a connection
    // with values not saved yet. Error says what's wrong with the edits.
    public ServerConfig? Preview(IReadOnlyList<SettingsEdit> edits, out string? error) {
        lock (_lock) {
            return TryApply(edits, out _, out var next, out error) ? next : null;
        }
    }

    // Applies the edits to the file and to the running server, or to neither if anything is wrong
    public SettingsUpdate Update(IReadOnlyList<SettingsEdit> edits) {
        lock (_lock) {
            if (!TryApply(edits, out var text, out var next, out var error)) {
                return SettingsUpdate.Failed(error);
            }
            var nextJson = ToJson(next);
            var currentJson = ToJson(_current);

            LiveSettings.Change change;
            try {
                change = _live.Prepare(currentJson, nextJson, next);
            }
            catch (Exception ex) {
                return SettingsUpdate.Failed(Explain(ex.Message));
            }

            try {
                Write(text);
            }
            catch (Exception ex) {
                change.Discard();
                return SettingsUpdate.Failed($"cannot write {_path}: {ex.Message}");
            }

            _text = text;
            _current = next;
            Log.Info("Settings saved to {Path}: {Paths}", _path, string.Join(", ", edits.Select(e => e.Path)));
            return new SettingsUpdate(true, null, _live.Apply(change), RestartNeeded(currentJson, nextJson));
        }
    }

    // The file's text with the edits, and the config it parses into
    private bool TryApply(IReadOnlyList<SettingsEdit> edits,
        [NotNullWhen(true)] out string? text, [NotNullWhen(true)] out ServerConfig? next, [NotNullWhen(false)] out string? error) {
        text = null;
        next = null;
        try {
            text = File.ReadAllText(_path);
        }
        catch (Exception ex) {
            error = $"cannot read {_path}: {ex.Message}";
            return false;
        }

        try {
            foreach (var edit in edits) {
                var path = edit.Path.Split('.');
                text = edit.Value is JsonValue value ? YamlEditor.Set(text, path, value) : YamlEditor.Remove(text, path);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or YamlDotNet.Core.YamlException) {
            error = ex.Message;
            return false;
        }

        if (!ConfigParser.TryParseYaml(text, _typeInfo, out next, out var parseError)) {
            error = Explain(parseError);
            return false;
        }
        ServerBootstrap.ResolvePaths(next, _path);

        // A value that doesn't come back from the parsed config named no real setting
        var nextJson = ToJson(next);
        foreach (var edit in edits) {
            if (edit.Value != null && !SameValue(At(nextJson, edit.Path), edit.Value)) {
                error = $"no such setting: {edit.Path}";
                return false;
            }
        }
        error = null;
        return true;
    }

    public void StartWatching() {
        try {
            _reloadTimer = new Timer(_ => ReloadEditedByHand());
            _watcher = new FileSystemWatcher(Path.GetDirectoryName(_path)!, Path.GetFileName(_path)) {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime,
            };
            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Renamed += OnFileChanged;
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) {
            Log.Warn("Cannot watch {Path} for edits, they take effect after a restart: {Error}", _path, ex.Message);
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e) =>
        _reloadTimer?.Change(ReloadDelay, Timeout.InfiniteTimeSpan);

    private void ReloadEditedByHand() {
        lock (_lock) {
            string text;
            try {
                text = File.ReadAllText(_path);
            }
            catch (IOException ex) {
                Log.Warn("Cannot read the changed config: {Error}", ex.Message);
                return;
            }
            if (text == _text) {
                return;
            }
            _text = text;

            if (!ConfigParser.TryParseYaml(text, _typeInfo, out var next, out var error)) {
                Log.Warn("The config was edited but has an error, the settings in effect stay: {Error}", error);
                return;
            }
            ServerBootstrap.ResolvePaths(next, _path);
            try {
                var currentJson = ToJson(_current);
                var nextJson = ToJson(next);
                var change = _live.Prepare(currentJson, nextJson, next);
                _current = next;
                _live.Apply(change);
                Log.Info("Picked up the config edited by hand");
                RestartNeeded(currentJson, nextJson);
            }
            catch (Exception ex) {
                Log.Warn("Cannot apply the edited config, the settings in effect stay: {Error}", ex.Message);
            }
        }
    }

    // Through a temporary file, so a crash can't leave half a config
    private void Write(string text) {
        var temp = _path + ".tmp";
        File.WriteAllText(temp, text, new UTF8Encoding(false));
        File.Move(temp, _path, overwrite: true);
    }

    // Settings for a restart that this change touched, and that now differ from what the server started with
    private string[] RestartNeeded(JsonObject currentJson, JsonObject nextJson) {
        string[] paths = [.. LiveSettings.RestartPaths.Where(p => Differs(currentJson, nextJson, p) && Differs(_startup, nextJson, p))];
        if (paths.Length > 0) {
            Log.Warn("Restart the server to apply: {Settings}", string.Join(", ", paths));
        }
        return paths;
    }

    private static bool Differs(JsonObject a, JsonObject b, string path) =>
        !JsonNode.DeepEquals(LiveSettings.At(a, path), LiveSettings.At(b, path));

    // "The JSON value could not be converted to System.Int32. Path: $.httpServer.port | ..." reads
    // as "httpServer.port: must be a whole number"
    private static string Explain(string error) {
        var match = Regex.Match(error, @"converted to ([\w.]+)\. Path: \$\.([\w.]+)");
        if (!match.Success) {
            return error;
        }
        var expected = match.Groups[1].Value switch {
            "System.Int32" or "System.Int64" => "must be a whole number",
            "System.Double" or "System.Single" or "System.Decimal" => "must be a number",
            "System.Boolean" => "must be true or false",
            "System.String" => "must be text",
            var type => $"must be a {type.Split('.')[^1]}",
        };
        return $"{match.Groups[2].Value}: {expected}";
    }

    private JsonObject ToJson(ServerConfig config) =>
        JsonSerializer.SerializeToNode(config, _typeInfo)!.AsObject();

    private static JsonNode? At(JsonNode? node, string path) {
        foreach (var key in path.Split('.')) {
            node = node is JsonObject obj ? obj.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)).Value : null;
        }
        return node;
    }

    // 180 and 180.0 are the same setting
    private static bool SameValue(JsonNode? actual, JsonNode expected) {
        if (actual is not JsonValue a || expected is not JsonValue e || a.GetValueKind() != e.GetValueKind()) {
            return false;
        }
        return a.GetValueKind() == JsonValueKind.Number
            ? Number(a) == Number(e)
            : JsonNode.DeepEquals(a, e);

        // Whatever number type is behind the node
        static double Number(JsonValue value) => double.Parse(value.ToJsonString(), CultureInfo.InvariantCulture);
    }

    public void Dispose() {
        _watcher?.Dispose();
        _reloadTimer?.Dispose();
    }
}
