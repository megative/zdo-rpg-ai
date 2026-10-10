namespace ZdoRpgAi.Protocol.Messages;

// Mod → Server

public enum ModToServerMessageType {
    PlayerAdded,
    TargetChanged,
    CellChange,
    GameSaveLoad,
    GetCharactersWhoHearResponse,
}

public record PlayerAddedPayload(string PlayerId);
public record TargetChangedPayload(string PlayerId, string? NpcId);
public record CellChangePayload(string PlayerId, string CellName);
public record GameSaveLoadPayload();
public record NearbyCharacterInfo(string CharacterId, float DistanceMeters);
// GameTime, CellName (where the speaker is) and SaveId describe the game the reply came from:
// story events are stamped with time and place and stored in the memory of that playthrough.
// GameSeconds: the game clock as a number, for telling how long ago something happened.
public record GetCharactersWhoHearResponsePayload(NearbyCharacterInfo[] Characters, string? GameTime = null, string? SaveId = null, string? CellName = null,
    double? GameSeconds = null);
