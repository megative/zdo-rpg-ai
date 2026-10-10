using System.Globalization;
using System.Text;

namespace ZdoRpgAi.ModEmulator.Scenarios;

// The results as a Markdown page: a table up top, then each scenario step by step. Numbers come
// out the same whatever the system's locale.
public static class ScenarioReport {
    public static string ToMarkdown(IReadOnlyList<ScenarioResult> results, string modelNote) {
        var md = new StringBuilder();
        md.AppendLine($"# Scenarios: {results.Count(r => r.Passed)} of {results.Count} passed");
        md.AppendLine();
        md.AppendLine(modelNote);
        md.AppendLine();
        md.AppendLine("| Scenario | Result | Model calls | Model time |");
        md.AppendLine("|---|---|---|---|");
        foreach (var r in results) {
            md.AppendLine(Invariant($"| {Escape(r.Name)} | {(r.Passed ? "✓" : "✗")} | {r.LlmCalls} | {r.LlmTime.TotalSeconds:0.0} s |"));
        }

        foreach (var r in results) {
            md.AppendLine();
            md.AppendLine($"## {(r.Passed ? "✓" : "✗")} {r.Name}");
            md.AppendLine();
            md.AppendLine($"`{r.File}`");
            if (r.Error != null) {
                md.AppendLine();
                md.AppendLine($"Stopped: {r.Error}");
            }
            for (var i = 0; i < r.Steps.Count; i++) {
                var step = r.Steps[i];
                md.AppendLine();
                md.AppendLine(Invariant($"{i + 1}. {step.Description} ({step.Took.TotalSeconds:0.0} s{(step.TimedOut ? ", timed out" : "")})"));
                foreach (var evt in step.Events) {
                    md.AppendLine($"   - {Escape(Line(evt))}");
                }
                foreach (var check in step.Checks) {
                    md.AppendLine($"   - {(check.Passed ? "✓" : "✗")} {Escape(check.Check)}{(check.Detail == null ? "" : $": {Escape(check.Detail)}")}");
                }
            }
        }
        return md.ToString();
    }

    private static string Line(GameEvent e) => e.Kind switch {
        GameEventKind.PlayerSaid => $"**Player:** {e.Text}",
        GameEventKind.NpcSaid => $"**{e.NpcId}{(e.TargetId is null or EmulatedWorld.PlayerId ? "" : $" → {e.TargetId}")}:** {e.Text}",
        _ => $"_{e.Kind} {e.NpcId} {e.Text}{(e.Value == 0 ? "" : $" {e.Value:+0;-0}")}{(e.Success ? "" : " (refused)")}_",
    };

    private static string Escape(string text) => text.Replace("|", "\\|").Replace("\n", " ");

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
