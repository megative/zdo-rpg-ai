using System.Diagnostics;
using ZdoRpgAi.Core;

namespace ZdoRpgAi.Server.Util.Mp3;

public class Mp3SpeedAdjuster {
    private static readonly ILog Log = Logger.Get<Mp3SpeedAdjuster>();

    private readonly string _ffmpegExePath;
    private readonly double _charactersPerSecond;

    public Mp3SpeedAdjuster(Mp3SpeedConfig config) {
        _ffmpegExePath = config.FfmpegExePath;
        _charactersPerSecond = config.CharactersPerSecond;
    }

    public async Task<byte[]> AdjustSpeedAsync(byte[] mp3, string text, double actualDurationSec) {
        var targetDuration = text.Length / _charactersPerSecond;
        if (actualDurationSec <= targetDuration) {
            return mp3;
        }

        var tempoFactor = actualDurationSec / targetDuration;
        var filterArg = BuildAtempoFilter(tempoFactor);

        Log.Info("Speeding up MP3: {Factor:F2}x (actual={Actual:F1}s, target={Target:F1}s, filter={Filter})",
            tempoFactor, actualDurationSec, targetDuration, filterArg);

        var psi = new ProcessStartInfo {
            FileName = _ffmpegExePath,
            Arguments = $"-i pipe:0 -filter:a \"{filterArg}\" -f mp3 pipe:1",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi) ?? throw new Exception("Failed to start ffmpeg");

        // Drain stderr concurrently: ffmpeg blocks once the stderr pipe buffer fills up
        var stderrTask = process.StandardError.ReadToEndAsync();

        var writeTask = Task.Run(async () => {
            await process.StandardInput.BaseStream.WriteAsync(mp3);
            process.StandardInput.BaseStream.Close();
        });

        using var outputStream = new MemoryStream();
        var readTask = process.StandardOutput.BaseStream.CopyToAsync(outputStream);

        await Task.WhenAll(writeTask, readTask);
        await process.WaitForExitAsync();
        var stderr = await stderrTask;

        if (process.ExitCode != 0) {
            Log.Warn("ffmpeg exited with code {Code}: {Stderr}", process.ExitCode, stderr);
            return mp3;
        }

        var result = outputStream.ToArray();
        Log.Debug("ffmpeg output: {InputSize} -> {OutputSize} bytes", mp3.Length, result.Length);
        return result;
    }

    // Invariant culture: with e.g. ru-RU, F4 yields "1,2500", and ffmpeg reads the comma as a filter separator
    private static string BuildAtempoFilter(double factor) {
        if (factor <= 2.0) {
            return FormattableString.Invariant($"atempo={factor:F4}");
        }

        var parts = new List<string>();
        while (factor > 2.0) {
            parts.Add("atempo=2.0000");
            factor /= 2.0;
        }
        parts.Add(FormattableString.Invariant($"atempo={factor:F4}"));
        return string.Join(",", parts);
    }
}

public class Mp3SpeedConfig {
    public string FfmpegExePath { get; init; } = "ffmpeg";
    public double CharactersPerSecond { get; init; } = 15;
}
