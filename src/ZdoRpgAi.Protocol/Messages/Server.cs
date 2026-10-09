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
    ModifyDisposition,
    OpenService,
    NpcThinking,
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
// Disposition is the vanilla 0-100 attitude toward the player; Services are what the NPC offers
// (barter, training, travel, spells, repair)
public record SceneNpcState(int Level, float HealthRatio, int Gold, string[] Equipped, SceneItem[] Items,
    int? Disposition = null, string[]? Services = null);
public record ScenePlayerState(
    string Name, string Race, string Sex, string? ClassName, int Level,
    float HealthRatio, float FatigueRatio, string[] Equipped,
    int Bounty = 0, SceneFaction[]? Factions = null);
public record SceneFaction(string Name, string? Rank);
public record SceneItem(string Id, string Name, int Count);
public record SpawnOnGroundInFrontOfCharacterPayload(string NpcId, string ItemId, int Count = 1);
public record PlaySound3dOnCharacterPayload(string NpcId, string Sound);
public record NpcStartFollowCharacterPayload(string NpcId, string TargetCharacterId);
public record NpcStopFollowCharacterPayload(string NpcId);
public record NpcAttackPayload(string NpcId, string TargetCharacterId);
public record NpcStopAttackPayload(string NpcId);
public record ShowMessageBoxPayload(string Message);
// The NPC is about to answer: the mod shows "Name: ..." until the line arrives
public record NpcThinkingPayload(string NpcId);
public record TransferItemPayload(string FromCharacterId, string ToCharacterId, string ItemId, int Count);
public record ModifyDispositionPayload(string NpcId, string PlayerId, int Delta);
// Service: barter, training, travel, spells or repair
public record OpenServicePayload(string NpcId, string Service);

// Reply to an action: Error explains a refusal, ItemName is the display name of a transferred item
public record ActionResultPayload(bool Success, string? Error = null, string? ItemName = null);
