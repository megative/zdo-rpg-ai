using ZdoRpgAi.Core;
using ZdoRpgAi.Server.Bootstrap;
using ZdoRpgAi.Server.Catalog;

namespace ZdoRpgAi.Server.Settings;

// Error: why the service gave no list; the panel then offers the models it knows itself
public record CatalogResult(CatalogItem[] Items, string? Error = null);

// What the service a part of the config uses offers, with the values in the form, saved or not:
// the models for the settings panel's lists, and ElevenLabs voices
public class CatalogLookup {
    private static readonly ILog Log = Logger.Get<CatalogLookup>();
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    // target: llm.main, llm.simple, stt or tts; kind: models or voices
    public static async Task<CatalogResult> RunAsync(ServerConfig config, string target, string kind) {
        object? provider = null;
        try {
            provider = target switch {
                "llm.main" when config.Llm.Main.Provider != "dummy" => ServerBootstrap.CreateLlm("main", config.Llm.Main, announce: false),
                "llm.simple" when config.Llm.Simple.Provider != "dummy" => ServerBootstrap.CreateLlm("simple", config.Llm.Simple, announce: false),
                "stt" when config.Stt.Provider != "dummy" => ServerBootstrap.CreateStt(config.Stt, announce: false),
                "tts" when config.Tts.Provider != "dummy" => ServerBootstrap.CreateTts(config.Tts, announce: false),
                _ => null,
            };
            var listing = kind == "voices" ? (provider as IVoiceCatalog)?.ListVoicesAsync() : (provider as IModelCatalog)?.ListModelsAsync();
            if (listing == null) {
                return new([]);
            }
            return new([.. await listing.WaitAsync(Timeout)]);
        }
        catch (TimeoutException) {
            return new([], $"no answer in {Timeout.TotalSeconds:0} s");
        }
        catch (Exception ex) {
            Log.Info("No {Kind} list for {Target}: {Error}", kind, target, ex.Message);
            return new([], ex.Message);
        }
        finally {
            (provider as IDisposable)?.Dispose();
        }
    }
}
