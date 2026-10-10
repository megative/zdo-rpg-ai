using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ZdoRpgAi.Server.Settings;

namespace ZdoRpgAi.Server.Panel;

// SaveId: the playthrough whose memory is active; Started: when this run of the server began
public record PanelStatus(string Version, string ConfigPath, int Clients, string SaveId, string Started);
// Port: where the restarted server listens, or, when refused, the busy port that stopped it
public record RestartResult(bool Ok, string? Error, int? Port = null);
// Values: the settings in effect, named as in the file. RestartRequired: settings read only at
// start; PendingRestart: those of them changed since the server started.
public record SettingsInfo(JsonObject Values, string Path, string[] RestartRequired, string[] PendingRestart);
public record SettingsUpdateRequest(SettingsEdit[] Edits);
// Target: llm.main, llm.simple, stt or tts; Edits: the form's unsaved changes, tested as if saved
public record TestRequest(string Target, SettingsEdit[]? Edits);
// Kind: models or voices
public record CatalogRequest(string Target, string Kind, SettingsEdit[]? Edits);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PanelStatus))]
[JsonSerializable(typeof(SettingsInfo))]
[JsonSerializable(typeof(SettingsUpdateRequest))]
[JsonSerializable(typeof(SettingsUpdate))]
[JsonSerializable(typeof(TestRequest))]
[JsonSerializable(typeof(ConnectionTestResult))]
[JsonSerializable(typeof(CatalogRequest))]
[JsonSerializable(typeof(CatalogResult))]
[JsonSerializable(typeof(RestartResult))]
internal partial class PanelJsonContext : JsonSerializerContext;
