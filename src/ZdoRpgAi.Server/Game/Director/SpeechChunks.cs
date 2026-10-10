using System.Text.RegularExpressions;

namespace ZdoRpgAi.Server.Game.Director;

// Splits a line into groups of sentences that are voiced one after another. Short lines stay
// whole: splitting only pays off when synthesizing the rest takes longer than the start plays.
public static partial class SpeechChunks {
    private const int MinChunkLength = 60;

    // Drops stage directions like "*she puts down her needle*" that models add despite the rules:
    // the voice would read them out. Null when nothing is left to say.
    public static string? WithoutStageDirections(string? text) {
        if (text == null) {
            return null;
        }
        var spoken = Spaces().Replace(StageDirection().Replace(text, " "), " ").Trim();
        return spoken.Length == 0 ? null : spoken;
    }

    [GeneratedRegex(@"\*[^*\n]*\*")]
    private static partial Regex StageDirection();

    [GeneratedRegex(@"[ \t]{2,}")]
    private static partial Regex Spaces();

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
