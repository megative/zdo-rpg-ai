using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ZdoRpgAi.Util;

public static class ConfigParser {
    public static T ParseYamlFile<T>(string path, JsonTypeInfo<T> jsonTypeInfo) {
        var fullPath = Path.GetFullPath(path);
        string yaml;
        try {
            yaml = File.ReadAllText(fullPath);
        }
        catch (Exception ex) {
            Console.Error.WriteLine($"Failed to read config: {fullPath} ({ex.Message})");
            Environment.Exit(1);
            return default!;
        }

        if (!TryParseYaml(yaml, jsonTypeInfo, out var config, out var error)) {
            Console.Error.WriteLine($"Invalid config: {fullPath} ({error})");
            Environment.Exit(1);
        }
        return config;
    }

    // For a config that changes while the program runs: an error leaves the caller to decide
    public static bool TryParseYaml<T>(string yaml, JsonTypeInfo<T> jsonTypeInfo, [NotNullWhen(true)] out T? config, [NotNullWhen(false)] out string? error) {
        try {
            var json = YamlToJsonTransformer.Parse(yaml).ToJsonString();
            config = JsonSerializer.Deserialize(json, jsonTypeInfo);
            error = config == null ? "the config is empty" : null;
            return config != null;
        }
        catch (Exception ex) {
            config = default;
            error = ex.Message;
            return false;
        }
    }
}
