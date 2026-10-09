using System.Text.RegularExpressions;

namespace ZdoRpgAi.Server.Game.Director;

// Splits a line into groups of sentences that are voiced one after another. Short lines stay
// whole: splitting only pays off when synthesizing the rest takes longer than the start plays.
public static partial class SpeechChunks {
    private const int MinChunkLength = 60;

    public static List<string> Split(string text) {
        var chunks = new List<string>();
        var current = "";
        foreach (var sentence in SentenceBreak().Split(text.Trim())) {
            current = current.Length == 0 ? sentence : $"{current} {sentence}";
            if (current.Length >= MinChunkLength) {
                chunks.Add(current);
                current = "";
            }
        }

        if (current.Length > 0) {
            // Don't leave a tiny tail on its own
            if (chunks.Count > 0 && current.Length < MinChunkLength / 2) {
                chunks[^1] = $"{chunks[^1]} {current}";
            }
            else {
                chunks.Add(current);
            }
        }

        return chunks.Count > 0 ? chunks : [text];
    }

    [GeneratedRegex(@"(?<=[.!?…])\s+")]
    private static partial Regex SentenceBreak();
}
