using System.Globalization;
using System.Text.RegularExpressions;
using ZdoRpgAi.Protocol.Messages;

namespace ZdoRpgAi.ModEmulator;

// An NPC of the emulated game. X and Y are meters from the middle of its cell; Cell null is the
// cell the player starts in.
public record NpcDefinition(
    string ObjectId, string Name, string Race, string Sex, float X, float Y,
    string ClassName, string? Faction = null, string? FactionRank = null, int Level = 1, int Gold = 0,
    string[]? Equipped = null, SceneItem[]? Items = null, string[]? Services = null, SceneQuestTopic[]? QuestTopics = null,
    string? Cell = null);

// A quest in the player's journal and the NPCs whose dialogue moves it on: like the mod, the game
// tells only them about it
public record QuestDefinition(string Name, string Entry, string[] Npcs);

public record PlayerDefinition(
    string Name, string Race, string Sex, string ClassName, int Level, string[] Equipped, SceneFaction[] Factions, int Bounty = 0,
    float HealthRatio = 1f, float FatigueRatio = 1f);

// GameTime: as the mod writes it, "09:30, 16 Last Seed 3E 427". Topics: the game's own dialogue
// lines on a topic, the same for every NPC here.
public record WorldDefinition(
    string Cell, string GameTime, PlayerDefinition Player, NpcDefinition[] Npcs, QuestDefinition[] Quests,
    Dictionary<string, string[]> Topics, Dictionary<string, int> PlayerItems, string SaveId = "emulator");

// The state of the emulated game: who is where, who carries what, who likes the player and who
// follows them. The server's requests and a test may look at it at the same time.
public partial class EmulatedWorld {
    public const string PlayerId = "player";
    public const string GoldId = "gold_001";
    // The mod's default hearing range, for requests that don't name one
    public const float DefaultHearingMeters = 2000f / 70f;

    // As in the mod: only a fellow member knows the player belongs to these
    private static readonly HashSet<string> SecretFactions = new(StringComparer.OrdinalIgnoreCase) {
        "Blades", "Thieves Guild", "Morag Tong", "Camonna Tong", "Dark Brotherhood", "Clan Aundae", "Clan Berne", "Clan Quarra",
    };

    private readonly object _lock = new();
    private readonly Dictionary<string, Dictionary<string, int>> _inventories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _dispositions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Position> _positions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _followers = new(StringComparer.OrdinalIgnoreCase);
    private Position _player;
    // The clock in seconds from the first day of the month, and the rest of the date as written
    private double _gameSeconds;
    private readonly string _monthAndYear = "";

    private readonly record struct Position(string Cell, float X, float Y);

    public EmulatedWorld(WorldDefinition definition) {
        Definition = definition;
        var time = TimeFormat().Match(definition.GameTime);
        if (time.Success) {
            _gameSeconds = ((int.Parse(time.Groups[3].Value) - 1) * 24 + int.Parse(time.Groups[1].Value)) * 3600.0 + int.Parse(time.Groups[2].Value) * 60.0;
            _monthAndYear = time.Groups[4].Value;
        }
        _player = new Position(definition.Cell, 0, 0);
        _inventories[PlayerId] = new Dictionary<string, int>(definition.PlayerItems, StringComparer.OrdinalIgnoreCase);
        foreach (var npc in definition.Npcs) {
            var items = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [GoldId] = npc.Gold };
            foreach (var item in npc.Items ?? []) {
                items[item.Id] = item.Count;
            }
            _inventories[npc.ObjectId] = items;
            _positions[npc.ObjectId] = new Position(npc.Cell ?? definition.Cell, npc.X, npc.Y);
        }
    }

    public WorldDefinition Definition { get; }
    public IReadOnlyList<NpcDefinition> Npcs => Definition.Npcs;
    public string SaveId => Definition.SaveId;

    public string PlayerCell {
        get {
            lock (_lock) {
                return _player.Cell;
            }
        }
    }

    public string GameTime {
        get {
            lock (_lock) {
                return FormatTime();
            }
        }
    }

    public double GameSeconds {
        get {
            lock (_lock) {
                return _gameSeconds;
            }
        }
    }

    // Time passes, as when the player waits or travels
    public void PassHours(double hours) {
        lock (_lock) {
            _gameSeconds += hours * 3600;
        }
    }

    // A time the world couldn't read stays as written
    private string FormatTime() {
        if (_monthAndYear.Length == 0) {
            return Definition.GameTime;
        }
        var minutes = (long)(_gameSeconds / 60);
        var day = minutes / (24 * 60) + 1;
        var ofDay = minutes % (24 * 60);
        return string.Create(CultureInfo.InvariantCulture, $"{ofDay / 60:00}:{ofDay % 60:00}, {day} {_monthAndYear}");
    }

    [GeneratedRegex(@"^(\d{1,2}):(\d{2}), (\d{1,2}) (.+)$")]
    private static partial Regex TimeFormat();

    public NpcDefinition? FindNpc(string npcId) =>
        Definition.Npcs.FirstOrDefault(n => string.Equals(n.ObjectId, npcId, StringComparison.OrdinalIgnoreCase));

    public int CountOf(string characterId, string itemId) {
        lock (_lock) {
            return _inventories.TryGetValue(characterId, out var items) ? items.GetValueOrDefault(itemId) : 0;
        }
    }

    public int DispositionOf(string npcId) {
        lock (_lock) {
            return _dispositions.GetValueOrDefault(npcId, 50);
        }
    }

    public bool IsFollowing(string npcId) {
        lock (_lock) {
            return _followers.Contains(npcId);
        }
    }

    public string? CellOf(string characterId) {
        lock (_lock) {
            if (characterId == PlayerId) {
                return _player.Cell;
            }
            return _positions.TryGetValue(characterId, out var position) ? position.Cell : null;
        }
    }

    public GetNpcInfoResponsePayload? NpcInfo(string npcId) {
        var npc = FindNpc(npcId);
        return npc == null ? null : new(npc.ObjectId, npc.Name, npc.Race, npc.Sex, npc.ClassName, npc.Faction, npc.FactionRank);
    }

    public GetPlayerInfoResponsePayload PlayerInfo =>
        new(PlayerId, Definition.Player.Name, Definition.Player.Race, Definition.Player.Sex);

    public GetSceneContextResponsePayload SceneContext(string npcId) {
        lock (_lock) {
            var npc = FindNpc(npcId);
            var npcState = npc == null ? null : new SceneNpcState(npc.Level, 1f, _inventories[npc.ObjectId].GetValueOrDefault(GoldId),
                npc.Equipped ?? [], npc.Items ?? [], _dispositions.GetValueOrDefault(npc.ObjectId, 50), npc.Services ?? [], npc.QuestTopics);

            var player = Definition.Player;
            var factions = player.Factions
                .Where(f => !SecretFactions.Contains(f.Name) || string.Equals(npc?.Faction, f.Name, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var quests = Definition.Quests
                .Where(q => q.Npcs.Contains(npcId, StringComparer.OrdinalIgnoreCase))
                .Select(q => new SceneQuest(q.Name, q.Entry))
                .ToArray();
            var playerState = new ScenePlayerState(player.Name, player.Race, player.Sex, player.ClassName, player.Level,
                player.HealthRatio, player.FatigueRatio, player.Equipped, player.Bounty, factions, quests);
            return new(FormatTime(), npc == null ? _player.Cell : _positions[npc.ObjectId].Cell, npcState, playerState, _gameSeconds);
        }
    }

    // Everyone in the speaker's cell within the distance, like the mod's GetCharactersWhoHear
    public NearbyCharacterInfo[] WhoHears(string speakerId, float? maxDistanceMeters) {
        var range = maxDistanceMeters ?? DefaultHearingMeters;
        lock (_lock) {
            var speaker = speakerId == PlayerId ? _player : _positions.GetValueOrDefault(speakerId, _player);
            return _positions
                .Where(p => !string.Equals(p.Key, speakerId, StringComparison.OrdinalIgnoreCase) && p.Value.Cell == speaker.Cell)
                .Select(p => new NearbyCharacterInfo(p.Key, Distance(p.Value, speaker)))
                .Where(c => c.DistanceMeters <= range)
                .OrderBy(c => c.DistanceMeters)
                .ToArray();
        }
    }

    // The player goes to another cell; whoever follows them comes along and stays close
    public void MovePlayer(string cell) {
        lock (_lock) {
            _player = new Position(cell, 0, 0);
            var offset = 1f;
            foreach (var follower in _followers) {
                _positions[follower] = new Position(cell, offset, 1f);
                offset += 1f;
            }
        }
    }

    // A follower walks up to the player and keeps close; one that stops stays where it is
    public ActionResultPayload Follow(string npcId, bool follow) {
        if (FindNpc(npcId) == null) {
            return new(false, "npc not found");
        }
        lock (_lock) {
            if (follow) {
                _followers.Add(npcId);
                _positions[npcId] = new Position(_player.Cell, _player.X + _followers.Count, _player.Y + 1f);
            }
            else {
                _followers.Remove(npcId);
            }
        }
        return new(true);
    }

    public ActionResultPayload TransferItem(TransferItemPayload transfer) {
        lock (_lock) {
            if (!_inventories.TryGetValue(transfer.FromCharacterId, out var from) || !_inventories.TryGetValue(transfer.ToCharacterId, out var to)) {
                return new(false, "character not found");
            }

            var have = from.GetValueOrDefault(transfer.ItemId);
            if (have < transfer.Count) {
                return new(false, have == 0 ? "does not have it" : $"has only {have}");
            }

            from[transfer.ItemId] = have - transfer.Count;
            to[transfer.ItemId] = to.GetValueOrDefault(transfer.ItemId) + transfer.Count;
            return new(true, null, ItemName(transfer.ItemId));
        }
    }

    public string ItemName(string itemId) =>
        Definition.Npcs.SelectMany(n => n.Items ?? []).FirstOrDefault(i => string.Equals(i.Id, itemId, StringComparison.OrdinalIgnoreCase))?.Name
        ?? (string.Equals(itemId, GoldId, StringComparison.OrdinalIgnoreCase) ? "Gold" : itemId);

    public ActionResultPayload ModifyDisposition(ModifyDispositionPayload change) {
        if (FindNpc(change.NpcId) == null) {
            return new(false, "npc not found");
        }
        lock (_lock) {
            _dispositions[change.NpcId] = Math.Clamp(_dispositions.GetValueOrDefault(change.NpcId, 50) + change.Delta, 0, 100);
        }
        return new(true);
    }

    public ActionResultPayload OpenService(OpenServicePayload request) {
        var npc = FindNpc(request.NpcId);
        if (npc == null) {
            return new(false, "npc not found");
        }
        return npc.Services?.Contains(request.Service) == true ? new(true) : new(false, $"does not offer {request.Service}");
    }

    public string[] TopicIds => [.. Definition.Topics.Keys];

    public TopicLines[] TopicLines(string[] topics, int limit) =>
        topics.Where(Definition.Topics.ContainsKey).Select(t => new TopicLines(t, Definition.Topics[t].Take(limit).ToArray())).ToArray();

    private static float Distance(Position a, Position b) =>
        MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
