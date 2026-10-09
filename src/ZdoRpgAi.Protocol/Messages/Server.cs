namespace ZdoRpgAi.Protocol.Messages;

// Server → Client

public enum ServerToClientMessageType {
    NpcSpeaksMp3,
}

public record NpcSpeaksMp3Payload(string NpcId, string Text, double DurationSec);

// Server → Mod

public enum ServerToModMessageType {
    SpeechRecognitionInProgress,
    SpeechRecognitionComplete,
    GetCharactersWhoHear,
    GetNpcInfo,
    GetPlayerInfo,
    GetSceneContext,
    SpawnOnGroundInFrontOfCharacter,
    PlaySound3dOnCharacter,
    NpcStartFollowCharacter,
    NpcStopFollowCharacter,
    NpcAttack,
    NpcStopAttack,
    ShowMessageBox,
    TransferItem,
}

public record SpeechRecognitionInProgressPayload(string PlayerId, string Text);
public record SpeechRecognitionCompletePayload(string PlayerId, string Text);
public record GetCharactersWhoHearRequestPayload(string CharacterId, float? MaxDistanceMeters = null);
public record GetNpcInfoRequestPayload(string NpcId);
public record GetNpcInfoResponsePayload(
    string ObjectId, string Name, string Race, string Sex,
    string? ClassName = null, string? Faction = null, string? FactionRank = null);
public record GetPlayerInfoRequestPayload(string PlayerId);
public record GetPlayerInfoResponsePayload(string ObjectId, string Name, string Race, string Sex);

// What changes during play, fetched once per NPC reply. Ratios are current/max in 0..1.
public record GetSceneContextRequestPayload(string NpcId, string PlayerId);
public record GetSceneContextResponsePayload(string? GameTime, string? CellName, SceneNpcState? Npc, ScenePlayerState? Player);
public record SceneNpcState(int Level, float HealthRatio, int Gold, string[] Equipped, SceneItem[] Items);
public record ScenePlayerState(
    string Name, string Race, string Sex, string? ClassName, int Level,
    float HealthRatio, float FatigueRatio, string[] Equipped);
public record SceneItem(string Id, string Name, int Count);
public record SpawnOnGroundInFrontOfCharacterPayload(string NpcId, string ItemId, int Count = 1);
public record PlaySound3dOnCharacterPayload(string NpcId, string Sound);
public record NpcStartFollowCharacterPayload(string NpcId, string TargetCharacterId);
public record NpcStopFollowCharacterPayload(string NpcId);
public record NpcAttackPayload(string NpcId, string TargetCharacterId);
public record NpcStopAttackPayload(string NpcId);
public record ShowMessageBoxPayload(string Message);
public record TransferItemPayload(string FromCharacterId, string ToCharacterId, string ItemId, int Count);

// Reply to an action: Error explains a refusal, ItemName is the display name of a transferred item
public record ActionResultPayload(bool Success, string? Error = null, string? ItemName = null);
