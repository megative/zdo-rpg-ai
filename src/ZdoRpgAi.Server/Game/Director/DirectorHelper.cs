using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Channel;
using ZdoRpgAi.Protocol.Messages;
using ZdoRpgAi.Protocol.Rpc;
using ZdoRpgAi.Server.TextToSpeech;
using ZdoRpgAi.Server.Util.Mp3;

namespace ZdoRpgAi.Server.Game.Director;

public class DirectorHelper {
    private static readonly ILog Log = Logger.Get<DirectorHelper>();

    private readonly IRpcChannel _rpc;

    public DirectorHelper(IRpcChannel rpc) {
        _rpc = rpc;
    }

    public void PublishNpcSpeaksMp3(string npcId, string text, ITextToSpeechOutput mp3, string? targetId) {
        var duration = Mp3Duration.Estimate(mp3.Mp3Bytes) ?? 0;
        _rpc.Publish(
            nameof(ServerToClientMessageType.NpcSpeaksMp3),
            JsonExtensions.SerializeToObject(
                new NpcSpeaksMp3Payload(npcId, text, duration, targetId),
                PayloadJsonContext.Default.NpcSpeaksMp3Payload),
            mp3.Mp3Bytes);
    }

    public void PublishNpcThinking(string npcId) {
        _rpc.Publish(
            nameof(ServerToModMessageType.NpcThinking),
            JsonExtensions.SerializeToObject(new NpcThinkingPayload(npcId), PayloadJsonContext.Default.NpcThinkingPayload));
    }

    public async Task<Observers> QueryObserversAsync(string characterId, string[]? excludeIds) {
        try {
            var response = await _rpc.CallAsync(
                nameof(ServerToModMessageType.GetCharactersWhoHear),
                JsonExtensions.SerializeToObject(
                    new GetCharactersWhoHearRequestPayload(characterId),
                    PayloadJsonContext.Default.GetCharactersWhoHearRequestPayload));

            var payload = response.Json?.DeserializeSafe(PayloadJsonContext.Default.GetCharactersWhoHearResponsePayload);
            var characters = payload?.Characters ?? [];

            var ids = characters
                .Select(c => c.CharacterId)
                .Where(id => id != characterId && (excludeIds == null ? true : !excludeIds.Contains(id)))
                .ToArray();
            return new Observers(ids, payload?.GameTime, payload?.SaveId, payload?.CellName, characters);
        }
        catch (Exception ex) {
            Log.Warn("Failed to query observers: {Error}", ex.Message);
            return new Observers([], null, null, null, []);
        }
    }

    public async Task<GetSceneContextResponsePayload?> QuerySceneContextAsync(string npcId, string playerId) {
        try {
            var response = await _rpc.CallAsync(
                nameof(ServerToModMessageType.GetSceneContext),
                JsonExtensions.SerializeToObject(
                    new GetSceneContextRequestPayload(npcId, playerId),
                    PayloadJsonContext.Default.GetSceneContextRequestPayload));

            return response.Json?.DeserializeSafe(PayloadJsonContext.Default.GetSceneContextResponsePayload);
        }
        catch (Exception ex) {
            Log.Warn("Failed to query scene context: {Error}", ex.Message);
            return null;
        }
    }
}

// GameTime, SaveId and Cell come from the mod's reply, null if it didn't report them. Nearby is
// everyone in earshot with distances, Ids the observers besides the excluded participants.
public record Observers(string[] Ids, string? GameTime, string? SaveId, string? Cell, NearbyCharacterInfo[] Nearby);
