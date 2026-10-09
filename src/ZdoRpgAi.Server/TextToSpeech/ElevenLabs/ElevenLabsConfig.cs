namespace ZdoRpgAi.Server.TextToSpeech.ElevenLabs;

public class ElevenLabsConfig {
    public required string ApiKey { get; set; }
    public string Model { get; set; } = "eleven_multilingual_v2";
    public double Stability { get; set; } = 0.75;
    public double SimilarityBoost { get; set; } = 0.75;
    public double Style { get; set; } = 0.2;
    public bool UseSpeakerBoost { get; set; }
    public required VoiceIdMappingConfig VoiceIdMapping { get; set; }
}

public class VoiceIdMappingConfig {
    public required string Fallback { get; set; }
    public Dictionary<string, string> ByRaceSex { get; set; } = new();
}
