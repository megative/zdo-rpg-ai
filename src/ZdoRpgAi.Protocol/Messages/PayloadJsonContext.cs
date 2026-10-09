using System.Text.Json.Serialization;

namespace ZdoRpgAi.Protocol.Messages;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
// Server → Client
[JsonSerializable(typeof(NpcSpeaksMp3Payload))]
// Server → Mod
[JsonSerializable(typeof(SpeechRecognitionInProgressPayload))]
[JsonSerializable(typeof(SpeechRecognitionCompletePayload))]
[JsonSerializable(typeof(GetCharactersWhoHearRequestPayload))]
[JsonSerializable(typeof(GetNpcInfoRequestPayload))]
[JsonSerializable(typeof(GetNpcInfoResponsePayload))]
[JsonSerializable(typeof(GetPlayerInfoRequestPayload))]
[JsonSerializable(typeof(GetPlayerInfoResponsePayload))]
[JsonSerializable(typeof(GetSceneContextRequestPayload))]
[JsonSerializable(typeof(GetSceneContextResponsePayload))]
[JsonSerializable(typeof(SpawnOnGroundInFrontOfCharacterPayload))]
[JsonSerializable(typeof(PlaySound3dOnCharacterPayload))]
[JsonSerializable(typeof(NpcStartFollowCharacterPayload))]
[JsonSerializable(typeof(NpcStopFollowCharacterPayload))]
[JsonSerializable(typeof(NpcAttackPayload))]
[JsonSerializable(typeof(NpcStopAttackPayload))]
[JsonSerializable(typeof(ShowMessageBoxPayload))]
[JsonSerializable(typeof(NpcThinkingPayload))]
[JsonSerializable(typeof(TaskUpdatePayload))]
[JsonSerializable(typeof(GetTopicsResponsePayload))]
[JsonSerializable(typeof(GetTopicLinesRequestPayload))]
[JsonSerializable(typeof(GetTopicLinesResponsePayload))]
[JsonSerializable(typeof(TransferItemPayload))]
[JsonSerializable(typeof(ModifyDispositionPayload))]
[JsonSerializable(typeof(OpenServicePayload))]
[JsonSerializable(typeof(ActionResultPayload))]
// Mod → Client
[JsonSerializable(typeof(StartSessionAckPayload))]
[JsonSerializable(typeof(PushToTalkPayload))]
[JsonSerializable(typeof(SetPushToTalkKeyPayload))]
// Client → Mod
[JsonSerializable(typeof(SayMp3FilePayload))]
// Client → Both
[JsonSerializable(typeof(PlayerStartSpeakPayload))]
[JsonSerializable(typeof(PlayerStopSpeakPayload))]
// Client → Server
[JsonSerializable(typeof(PlayerSpeaksTextPayload))]
[JsonSerializable(typeof(PlayerSpeaksAudioPayload))]
// Mod → Server
[JsonSerializable(typeof(PlayerAddedPayload))]
[JsonSerializable(typeof(TargetChangedPayload))]
[JsonSerializable(typeof(CellChangePayload))]
[JsonSerializable(typeof(GameSaveLoadPayload))]
[JsonSerializable(typeof(NearbyCharacterInfo))]
[JsonSerializable(typeof(GetCharactersWhoHearResponsePayload))]
public partial class PayloadJsonContext : JsonSerializerContext;
