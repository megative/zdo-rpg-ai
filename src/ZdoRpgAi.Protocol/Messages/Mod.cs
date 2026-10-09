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
// GameTime and SaveId describe the game the reply came from: story events are stamped with the
// in-game time and stored in the memory of that playthrough
public record GetCharactersWhoHearResponsePayload(NearbyCharacterInfo[] Characters, string? GameTime = null, string? SaveId = null);
