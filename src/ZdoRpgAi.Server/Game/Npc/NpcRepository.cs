using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Messages;
using ZdoRpgAi.Protocol.Rpc;
using ZdoRpgAi.Repository;

namespace ZdoRpgAi.Server.Game.Npc;

public record NpcInfo(string Id, string Name, string Race, string Sex,
    string? ClassName = null, string? Faction = null, string? FactionRank = null);

public class NpcRepository {
    private static readonly ILog Log = Logger.Get<NpcRepository>();

    private readonly IMainRepository _mainRepo;
    private readonly ISaveGameRepository _saveGameRepo;
    private readonly IRpcChannel _rpc;

    public NpcRepository(IMainRepository mainRepo, ISaveGameRepository saveGameRepo, IRpcChannel rpc) {
        _mainRepo = mainRepo;
        _saveGameRepo = saveGameRepo;
        _rpc = rpc;
    }

    public async Task<NpcInfo?> GetNpcInfoAsync(string npcId) {
        var cached = _saveGameRepo.GetNpcInfo(npcId)
                  ?? _mainRepo.GetNpcInfo(npcId);
        // Every NPC has a class: an entry without one was cached before classes were tracked
        if (cached is { ClassName: not null }) {
            return ToNpcInfo(cached);
        }

        GetNpcInfoResponsePayload? payload = null;
        try {
            var response = await _rpc.CallAsync(
                nameof(ServerToModMessageType.GetNpcInfo),
                JsonExtensions.SerializeToObject(
                    new GetNpcInfoRequestPayload(npcId),
                    PayloadJsonContext.Default.GetNpcInfoRequestPayload));
            payload = response.Json?.DeserializeSafe(PayloadJsonContext.Default.GetNpcInfoResponsePayload);
        }
        catch (Exception ex) when (cached != null) {
            Log.Warn("Failed to refresh info for NPC {NpcId}, using cached: {Error}", npcId, ex.Message);
        }

        // An error reply ({ error = ... }) deserializes with no name
        if (payload?.Name == null) {
            if (cached != null) {
                return ToNpcInfo(cached);
            }
            Log.Warn("Mod returned no info for NPC {NpcId}", npcId);
            return null;
        }

        var info = new RawNpcInfo(npcId, payload.Name, payload.Race, payload.Sex, payload.ClassName, payload.Faction, payload.FactionRank);
        _saveGameRepo.SaveNpcInfo(info);
        return ToNpcInfo(info);
    }

    private static NpcInfo ToNpcInfo(RawNpcInfo raw) =>
        new(raw.Id, raw.Name, raw.Race, raw.Sex, raw.ClassName, raw.Faction, raw.FactionRank);
}
