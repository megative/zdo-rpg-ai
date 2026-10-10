using System.Text;

namespace ZdoRpgAi.Server.Util;

public static class Wav {
    public const int SampleRate = 16_000;
    private const short Channels = 1;
    private const short BitsPerSample = 16;

    // The player's voice as the client records it: linear16, 16 kHz, mono
    public static byte[] FromPcm(byte[] pcm) {
        const int byteRate = SampleRate * Channels * BitsPerSample / 8;
        const short blockAlign = Channels * BitsPerSample / 8;
        var dataSize = pcm.Length;
        var riffSize = 36 + dataSize;

        using var ms = new MemoryStream(44 + dataSize);
        using var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

        w.Write("RIFF"u8);
        w.Write(riffSize);
        w.Write("WAVE"u8);

        w.Write("fmt "u8);
        w.Write(16);                          // fmt chunk size
        w.Write((short)1);                    // PCM
        w.Write(Channels);
        w.Write(SampleRate);
        w.Write(byteRate);
        w.Write(blockAlign);
        w.Write(BitsPerSample);

        w.Write("data"u8);
        w.Write(dataSize);
        w.Write(pcm);
        w.Flush();

        return ms.ToArray();
    }

    // Half a second of silence, enough for a service to show it works
    public static byte[] Silence() => FromPcm(new byte[SampleRate * BitsPerSample / 8 / 2]);
}
