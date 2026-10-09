namespace ZdoRpgAi.Server.Util.Mp3;

public static class Mp3Duration {
    private static readonly int[] BitratesV1L3 = [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0];
    private static readonly int[] SampleRatesV1 = [44100, 48000, 32000, 0];

    // MPEG-2 and MPEG-2.5 (lower sample rates), e.g. OpenAI TTS returns 24 kHz MPEG-2 audio
    private static readonly int[] BitratesV2L3 = [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0];
    private static readonly int[] SampleRatesV2 = [22050, 24000, 16000, 0];
    private static readonly int[] SampleRatesV25 = [11025, 12000, 8000, 0];

    public static double? Estimate(byte[] data) {
        double totalSeconds = 0;
        int i = 0;

        // Skip ID3v2 tag if present
        if (data.Length > 10 && data[0] == 'I' && data[1] == 'D' && data[2] == '3') {
            int tagSize = (data[6] & 0x7F) << 21 | (data[7] & 0x7F) << 14 |
                          (data[8] & 0x7F) << 7 | (data[9] & 0x7F);
            i = 10 + tagSize;
        }

        int frameCount = 0;
        while (i + 3 < data.Length) {
            if (data[i] != 0xFF || (data[i + 1] & 0xE0) != 0xE0) {
                i++;
                continue;
            }

            int header = data[i] << 24 | data[i + 1] << 16 | data[i + 2] << 8 | data[i + 3];
            int version = (header >> 19) & 3;
            int layer = (header >> 17) & 3;
            int bitrateIdx = (header >> 12) & 0xF;
            int sampleIdx = (header >> 10) & 3;
            int padding = (header >> 9) & 1;

            // version: 3 = MPEG-1, 2 = MPEG-2, 0 = MPEG-2.5, 1 = reserved
            if (version == 1 || layer != 1 || bitrateIdx == 0 || bitrateIdx == 15 || sampleIdx == 3) {
                i++;
                continue;
            }

            var isMpeg1 = version == 3;
            int bitrate = (isMpeg1 ? BitratesV1L3 : BitratesV2L3)[bitrateIdx] * 1000;
            int sampleRate = (version switch { 3 => SampleRatesV1, 2 => SampleRatesV2, _ => SampleRatesV25 })[sampleIdx];
            int samplesPerFrame = isMpeg1 ? 1152 : 576;
            int frameSize = samplesPerFrame / 8 * bitrate / sampleRate + padding;

            if (frameSize < 4) {
                i++;
                continue;
            }

            totalSeconds += (double)samplesPerFrame / sampleRate;
            frameCount++;
            i += frameSize;
        }

        if (frameCount == 0) {
            return null;
        }

        return Math.Clamp(totalSeconds, 0, 120);
    }
}
