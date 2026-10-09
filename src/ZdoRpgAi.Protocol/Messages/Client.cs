namespace ZdoRpgAi.Protocol.Messages;

// Client → Mod

public enum ClientToModMessageType {
    SayMp3File,
    SetPushToTalkKey,
}

public record SayMp3FilePayload(string NpcId, string Mp3Name, string Text, double? DurationSec = null, string? TargetId = null);

// Key: an openmw.input.KEY name, e.g. "Y", "F1", "LeftAlt"
public record SetPushToTalkKeyPayload(string Key);

// Mod → Client

public enum ModToClientMessageType {
    PushToTalk,
}

public record StartSessionAckPayload(string SessionId);
public record PushToTalkPayload(bool Pressed);

// Client → Both (Mod + Server)

public enum ClientToBothMessageType {
    PlayerStartSpeak,
    PlayerStopSpeak,
}

public record PlayerStartSpeakPayload(string PlayerId, string? TargetCharacterId, string GameTime);
public record PlayerStopSpeakPayload(string PlayerId, bool Cancel = false);

// Client → Server

public enum ClientToServerMessageType {
    PlayerSpeaksText,
    PlayerSpeaksAudio,
}

public record PlayerSpeaksTextPayload(string PlayerId, string Text, string? TargetCharacterId, string GameTime);
public record PlayerSpeaksAudioPayload(string PlayerId);
