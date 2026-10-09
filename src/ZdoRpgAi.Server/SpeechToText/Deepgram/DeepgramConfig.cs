namespace ZdoRpgAi.Server.SpeechToText.Deepgram;

public class DeepgramConfig {
    public required string ApiKey { get; set; }
    public int SampleRate { get; set; } = 16_000;
    public string Encoding { get; set; } = "linear16";
    public string Language { get; set; } = "en";
    public string Model { get; set; } = "nova-2";
}
