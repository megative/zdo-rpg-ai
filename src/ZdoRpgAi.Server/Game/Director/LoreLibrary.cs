using System.Text.RegularExpressions;
using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Messages;
using ZdoRpgAi.Protocol.Rpc;

namespace ZdoRpgAi.Server.Game.Director;

// The game's own dialogue as NPC knowledge: when the player mentions a dialogue topic ("Balmora",
// "latest rumors"), the lines this NPC would have for it in vanilla Morrowind go into its prompt,
// so answers stay true to the lore and directions are right.
public partial class LoreLibrary {
    private static readonly ILog Log = Logger.Get<LoreLibrary>();

    private const int MaxTopics = 3;
    private const int LinesPerTopic = 3;
    private const int MinStemLength = 4;
    private const int TopicListTimeoutMs = 15000;

    private readonly IRpcChannel _rpc;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    // Topic ids with their words, loaded once from the mod
    private List<(string Id, string[] Words)>? _topics;

    public LoreLibrary(IRpcChannel rpc) {
        _rpc = rpc;
    }

    public async Task<List<TopicLines>> RecallAsync(string npcId, string playerText) {
        try {
            var topics = await GetTopicsAsync();
            var mentioned = FindTopics(topics, playerText);
            if (mentioned.Count == 0) {
                return [];
            }

            var response = await _rpc.CallAsync(
                nameof(ServerToModMessageType.GetTopicLines),
                JsonExtensions.SerializeToObject(
                    new GetTopicLinesRequestPayload(npcId, [.. mentioned], LinesPerTopic),
                    PayloadJsonContext.Default.GetTopicLinesRequestPayload));
            var lines = response.Json?.DeserializeSafe(PayloadJsonContext.Default.GetTopicLinesResponsePayload)?.Topics ?? [];
            Log.Debug("Recalled {Count} topics for {NpcId}: {Topics}", lines.Length, npcId, string.Join(", ", mentioned));
            return [.. lines];
        }
        catch (Exception ex) {
            Log.Warn("Failed to recall lore: {Error}", ex.Message);
            return [];
        }
    }

    private async Task<List<(string Id, string[] Words)>> GetTopicsAsync() {
        if (_topics != null) {
            return _topics;
        }

        await _loadLock.WaitAsync();
        try {
            if (_topics == null) {
                var response = await _rpc.CallAsync(nameof(ServerToModMessageType.GetTopics), null, TopicListTimeoutMs);
                var ids = response.Json?.DeserializeSafe(PayloadJsonContext.Default.GetTopicsResponsePayload)?.Topics ?? [];
                // An empty list most likely means the mod doesn't support it yet: ask again next time
                if (ids.Length > 0) {
                    _topics = ids.Select(id => (id, Words(id))).Where(t => t.Item2.Length > 0).ToList();
                    Log.Info("Loaded {Count} dialogue topics", _topics.Count);
                }
            }
            return _topics ?? [];
        }
        finally {
            _loadLock.Release();
        }
    }

    // Topics all of whose words occur in the text, word endings aside, longest first
    internal static List<string> FindTopics(IEnumerable<(string Id, string[] Words)> topics, string text) {
        var words = Words(text);
        return topics
            .Where(t => t.Words.All(tw => words.Any(w => SameStem(tw, w))))
            .OrderByDescending(t => t.Id.Length)
            .Take(MaxTopics)
            .Select(t => t.Id)
            .ToList();
    }

    // Russian declension changes endings: "Балмора" vs "в Балморе", "секрет" vs "секретик"
    private static bool SameStem(string a, string b) {
        if (a == b) {
            return true;
        }

        var shorter = Math.Min(a.Length, b.Length);
        if (shorter < MinStemLength) {
            return false;
        }

        var common = 0;
        while (common < shorter && a[common] == b[common]) {
            common++;
        }
        return common >= Math.Max(MinStemLength, Math.Max(a.Length, b.Length) - 3);
    }

    private static string[] Words(string text) =>
        WordPattern().Matches(text).Select(m => m.Value.ToLowerInvariant()).ToArray();

    [GeneratedRegex(@"\p{L}+")]
    private static partial Regex WordPattern();
}
