using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace ZdoRpgAi.Util;

// Changes single values of a YAML document in its text, so a hand-written config keeps its
// comments, order and quoting: a value is replaced where it stands, a missing key is added at the
// end of its section, a removed key takes its lines with it. Keys match case-insensitively, as the
// config parser reads them.
public static class YamlEditor {
    private const int IndentStep = 2;

    public static string Set(string yaml, IReadOnlyList<string> path, JsonValue value) {
        if (path.Count == 0) {
            throw new ArgumentException("Empty path", nameof(path));
        }

        var newline = yaml.Contains("\r\n") ? "\r\n" : "\n";
        if (Parse(yaml) is not MappingNode root) {
            if (!string.IsNullOrWhiteSpace(StripComments(yaml))) {
                throw new InvalidOperationException("The document is not a mapping");
            }
            return AppendAtEnd(yaml, BlockLines(path, 0, 0, value, newline), newline);
        }

        var map = root;
        for (var depth = 0; depth < path.Count; depth++) {
            var entry = map.Find(path[depth]);
            if (entry == null) {
                return InsertInto(yaml, map, map == root, path, depth, value, newline);
            }

            var last = depth == path.Count - 1;
            switch (entry.Value.Value) {
                case ScalarNode scalar when last:
                    return Replace(yaml, scalar.Start, scalar.End, ValueText(yaml, scalar, value));
                case ScalarNode scalar when scalar.IsNull:
                    return ReplaceNullWithMapping(yaml, map, entry.Value, path, depth + 1, value, newline);
                case MappingNode child when !last:
                    map = child;
                    break;
                default:
                    throw new InvalidOperationException($"{Describe(path, depth)} is {(last ? "a section, not a value" : "a value, not a section")}");
            }
        }
        throw new InvalidOperationException("Unreachable");
    }

    // Removing what isn't there is fine; a section left empty goes as well
    public static string Remove(string yaml, IReadOnlyList<string> path) {
        if (Parse(yaml) is not MappingNode root || path.Count == 0) {
            return yaml;
        }

        var chain = new List<(MappingNode Map, Entry Entry)>();
        var map = root;
        for (var depth = 0; depth < path.Count; depth++) {
            var entry = map.Find(path[depth]);
            if (entry == null) {
                return yaml;
            }
            chain.Add((map, entry.Value));
            if (depth < path.Count - 1) {
                if (entry.Value.Value is not MappingNode child) {
                    return yaml;
                }
                map = child;
            }
        }

        // Walk up while the section would be left with nothing in it
        var level = chain.Count - 1;
        while (level > 0 && chain[level].Map.Entries.Count == 1 && !chain[level].Map.Flow) {
            level--;
        }
        var (owner, target) = chain[level];
        return owner.Flow ? RemoveFlowEntry(yaml, owner, target) : RemoveBlockEntry(yaml, target);
    }

    private static string InsertInto(string yaml, MappingNode map, bool isRoot, IReadOnlyList<string> path, int depth, JsonValue value, string newline) {
        if (map.Flow) {
            var text = $"{path[depth]}: {FlowValue(path, depth + 1, value)}";
            if (map.Entries.Count == 0) {
                return Replace(yaml, map.End, map.End, $" {text} ");
            }
            var lastEnd = map.Entries[^1].Value.End;
            return Replace(yaml, lastEnd, lastEnd, $", {text}");
        }

        var indent = map.Entries[0].Key.Column - 1;
        var lines = BlockLines(path, depth, indent, value, newline);
        if (isRoot) {
            return AppendAtEnd(yaml, lines, newline);
        }
        var at = LineEnd(yaml, map.LastIndex);
        return Replace(yaml, at, at, newline + lines);
    }

    // "voiceMapping:" with nothing (or null) after it becomes a section with the new key
    private static string ReplaceNullWithMapping(string yaml, MappingNode map, Entry entry, IReadOnlyList<string> path, int depth, JsonValue value, string newline) {
        var scalar = (ScalarNode)entry.Value;
        if (map.Flow) {
            var flow = (scalar.Start > 0 && yaml[scalar.Start - 1] == ':' ? " " : "") + FlowValue(path, depth, value);
            return Replace(yaml, scalar.Start, scalar.End, flow);
        }

        var indent = entry.Key.Column - 1 + IndentStep;
        var lines = BlockLines(path, depth, indent, value, newline);
        // An explicit "null" or "~" goes; an empty value has nothing to remove
        var cleared = Replace(yaml, scalar.Start, scalar.End, "");
        var at = LineEnd(cleared, entry.Key.End);
        return Replace(cleared, at, at, newline + lines);
    }

    private static string RemoveBlockEntry(string yaml, Entry entry) {
        var start = LineStart(yaml, entry.Key.Start);
        var end = LineEnd(yaml, LastIndexOf(entry.Value));
        // Take the line break after the entry too, or before it at the end of the file
        if (end < yaml.Length) {
            end = yaml[end] == '\r' ? end + 2 : end + 1;
        }
        else if (start > 0) {
            start = yaml[start - 1] == '\n' && start > 1 && yaml[start - 2] == '\r' ? start - 2 : start - 1;
        }
        return Replace(yaml, start, Math.Min(end, yaml.Length), "");
    }

    private static string RemoveFlowEntry(string yaml, MappingNode map, Entry entry) {
        var index = map.Entries.IndexOf(entry);
        int start, end;
        if (index < map.Entries.Count - 1) {
            start = entry.Key.Start;
            end = map.Entries[index + 1].Key.Start;
        }
        else if (index > 0) {
            start = map.Entries[index - 1].Value.End;
            end = entry.Value.End;
        }
        else {
            start = entry.Key.Start;
            end = entry.Value.End;
        }
        return Replace(yaml, start, end, "");
    }

    // "key: value" lines for path[depth..], nested one level deeper each
    private static string BlockLines(IReadOnlyList<string> path, int depth, int indent, JsonValue value, string newline) {
        var lines = new List<string>();
        for (var i = depth; i < path.Count; i++) {
            var pad = new string(' ', indent + (i - depth) * IndentStep);
            lines.Add(i == path.Count - 1 ? $"{pad}{path[i]}: {FormatScalar(value, null)}" : $"{pad}{path[i]}:");
        }
        return string.Join(newline, lines);
    }

    // { a: { b: value } } for path[depth..]
    private static string FlowValue(IReadOnlyList<string> path, int depth, JsonValue value) {
        var text = FormatScalar(value, null);
        for (var i = path.Count - 1; i >= depth; i--) {
            text = $"{{ {path[i]}: {text} }}";
        }
        return text;
    }

    private static string AppendAtEnd(string yaml, string lines, string newline) {
        var sb = new StringBuilder(yaml);
        if (sb.Length > 0 && !yaml.EndsWith('\n')) {
            sb.Append(newline);
        }
        sb.Append(lines).Append(newline);
        return sb.ToString();
    }

    // Keeps the quoting the value had; an empty value right after the colon gets a space first
    private static string ValueText(string yaml, ScalarNode scalar, JsonValue value) {
        var text = FormatScalar(value, scalar.Style);
        var index = scalar.Start;
        return scalar.Start == scalar.End && index > 0 && yaml[index - 1] == ':' ? " " + text : text;
    }

    internal static string FormatScalar(JsonValue value, ScalarStyle? style) {
        switch (value.GetValueKind()) {
            case JsonValueKind.True:
                return "true";
            case JsonValueKind.False:
                return "false";
            case JsonValueKind.Number:
                return value.ToJsonString();
            case JsonValueKind.String:
                var s = value.GetValue<string>();
                return style switch {
                    ScalarStyle.DoubleQuoted => DoubleQuoted(s),
                    ScalarStyle.SingleQuoted => $"'{s.Replace("'", "''")}'",
                    _ => IsPlainSafe(s) ? s : DoubleQuoted(s),
                };
            default:
                throw new ArgumentException($"Only strings, numbers and booleans, not {value.GetValueKind()}");
        }
    }

    // Plain only when the parser reads it back as the same string
    private static bool IsPlainSafe(string s) {
        if (s.Length == 0 || char.IsWhiteSpace(s[0]) || char.IsWhiteSpace(s[^1])) {
            return false;
        }
        if (s is "true" or "True" or "TRUE" or "false" or "False" or "FALSE" or "null" or "Null" or "NULL" or "~"
            || double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) {
            return false;
        }
        if ("-?:,[]{}#&*!|>'\"%@`".Contains(s[0]) || s.EndsWith(':') || s.Contains(": ") || s.Contains(" #")) {
            return false;
        }
        return !s.Any(c => char.IsControl(c) || c is ',' or '[' or ']' or '{' or '}');
    }

    private static string DoubleQuoted(string s) {
        var sb = new StringBuilder("\"");
        foreach (var c in s) {
            switch (c) {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (char.IsControl(c)) {
                        sb.Append(CultureInfo.InvariantCulture, $"\\u{(int)c:x4}");
                    }
                    else {
                        sb.Append(c);
                    }
                    break;
            }
        }
        return sb.Append('"').ToString();
    }

    private static string Replace(string text, int start, int end, string replacement) =>
        string.Concat(text.AsSpan(0, start), replacement, text.AsSpan(end));

    private static int LineStart(string text, int index) {
        var i = text.LastIndexOf('\n', Math.Max(0, Math.Min(index, text.Length) - 1));
        return i < 0 || index == 0 ? 0 : i + 1;
    }

    // Where the line holding index ends, before its line break
    private static int LineEnd(string text, int index) {
        var i = text.IndexOf('\n', Math.Min(index, text.Length));
        if (i < 0) {
            return text.Length;
        }
        return i > 0 && text[i - 1] == '\r' ? i - 1 : i;
    }

    private static int LastIndexOf(Node node) => node switch {
        MappingNode map => map.LastIndex,
        SequenceNode seq => seq.Items.Count > 0 ? LastIndexOf(seq.Items[^1]) : seq.End,
        _ => node.End,
    };

    private static string Describe(IReadOnlyList<string> path, int depth) => string.Join('.', path.Take(depth + 1));

    private static string StripComments(string yaml) =>
        string.Join('\n', yaml.Split('\n').Select(line => line.TrimStart().StartsWith('#') ? "" : line));

    // A small tree of the document. Start and End are character offsets in the text, Column is the
    // 1-based column of Start.
    private abstract class Node {
        public int Start { get; init; }
        public int End { get; set; }
        public int Column { get; init; }
    }

    private sealed class ScalarNode : Node {
        public required string Value { get; init; }
        public ScalarStyle Style { get; init; }
        public bool IsNull => Style is ScalarStyle.Plain && Value is "" or "~" or "null" or "Null" or "NULL";
    }

    private record struct Entry(ScalarNode Key, Node Value);

    private sealed class MappingNode : Node {
        public bool Flow { get; init; }
        public List<Entry> Entries { get; } = [];

        // Where the last value in this section ends
        public int LastIndex => Entries.Count > 0 ? LastIndexOf(Entries[^1].Value) : End;

        public Entry? Find(string key) {
            foreach (var entry in Entries) {
                if (string.Equals(entry.Key.Value, key, StringComparison.OrdinalIgnoreCase)) {
                    return entry;
                }
            }
            return null;
        }
    }

    private sealed class SequenceNode : Node {
        public List<Node> Items { get; } = [];
    }

    private sealed class OtherNode : Node;

    private static Node? Parse(string yaml) {
        var parser = new Parser(new StringReader(yaml));
        parser.Consume<StreamStart>();
        if (!parser.TryConsume<DocumentStart>(out _)) {
            return null;
        }
        return ParseNode(parser);
    }

    private static Node ParseNode(IParser parser) {
        if (parser.TryConsume<Scalar>(out var scalar)) {
            return new ScalarNode { Value = scalar.Value, Style = scalar.Style, Start = Index(scalar.Start), End = Index(scalar.End), Column = (int)scalar.Start.Column };
        }

        if (parser.TryConsume<MappingStart>(out var mappingStart)) {
            var map = new MappingNode { Start = Index(mappingStart.Start), Column = (int)mappingStart.Start.Column, Flow = mappingStart.Style == MappingStyle.Flow };
            while (!parser.Accept<MappingEnd>(out _)) {
                if (ParseNode(parser) is not ScalarNode key) {
                    throw new InvalidOperationException("Only plain keys are supported");
                }
                map.Entries.Add(new Entry(key, ParseNode(parser)));
            }
            map.End = Index(parser.Consume<MappingEnd>().Start);
            return map;
        }

        if (parser.TryConsume<SequenceStart>(out var sequenceStart)) {
            var seq = new SequenceNode { Start = Index(sequenceStart.Start), Column = (int)sequenceStart.Start.Column };
            while (!parser.Accept<SequenceEnd>(out _)) {
                seq.Items.Add(ParseNode(parser));
            }
            seq.End = Index(parser.Consume<SequenceEnd>().End);
            return seq;
        }

        var other = parser.Consume<ParsingEvent>();
        return new OtherNode { Start = Index(other.Start), End = Index(other.End), Column = (int)other.Start.Column };
    }

    private static int Index(Mark mark) => (int)mark.Index;
}
