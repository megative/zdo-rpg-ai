using System.Text.Json;
using System.Text.Json.Serialization;
using ZdoRpgAi.Database;

namespace ZdoRpgAi.Repository;

// Each playthrough has its own database file, chosen by the save id the mod reports. Saves made
// before save ids existed report "default", which maps to the configured path, so their memories
// carry over. Per-save files and the locking follow waterbobo24's fork
// (github.com/waterbobo24/Morrowind-llm-server-client).
public class LocalDatabaseSaveGameRepository : ISaveGameRepository, IDisposable {
    private const string DefaultSaveId = "default";

    private readonly string _basePath;
    // One SQLite connection is shared by concurrent story and director tasks
    private readonly object _lock = new();
    private SaveGameDatabase _db;
    private string _saveId = DefaultSaveId;

    public LocalDatabaseSaveGameRepository(string path) {
        _basePath = path;
        _db = Open(path);
    }

    public string SaveId {
        get {
            lock (_lock) {
                return _saveId;
            }
        }
    }

    public bool SetSaveContext(string saveId) {
        lock (_lock) {
            if (saveId == _saveId) {
                return false;
            }

            var db = Open(GetPath(saveId));
            _db.Dispose();
            _db = db;
            _saveId = saveId;
            return true;
        }
    }

    private string GetPath(string saveId) {
        if (saveId == DefaultSaveId) {
            return _basePath;
        }

        var safeId = new string(saveId.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray());
        var dir = Path.GetDirectoryName(_basePath) ?? ".";
        return Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(_basePath)}_{safeId}{Path.GetExtension(_basePath)}");
    }

    private static SaveGameDatabase Open(string path) {
        var db = new SaveGameDatabase(path);
        db.Open();
        return db;
    }

    public long AddStoryEvent(string gameTime, string realTime, string type, string dataJson) {
        lock (_lock) {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO story_event (gameTime, realTime, type, dataJson)
                VALUES ($gameTime, $realTime, $type, $dataJson)
                RETURNING id
                """;
            cmd.Parameters.AddWithValue("$gameTime", gameTime);
            cmd.Parameters.AddWithValue("$realTime", realTime);
            cmd.Parameters.AddWithValue("$type", type);
            cmd.Parameters.AddWithValue("$dataJson", dataJson);
            return (long)cmd.ExecuteScalar()!;
        }
    }

    public void AddStoryEventObservers(long storyEventId, string[] characterIds) {
        if (characterIds.Length == 0) {
            return;
        }

        lock (_lock) {
            using var tx = _db.Connection.BeginTransaction();
            foreach (var characterId in characterIds) {
                using var cmd = _db.Connection.CreateCommand();
                cmd.CommandText = "INSERT INTO story_event_observer (storyEventId, characterId) VALUES ($eventId, $charId)";
                cmd.Parameters.AddWithValue("$eventId", storyEventId);
                cmd.Parameters.AddWithValue("$charId", characterId);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
    }

    public List<RawStoryEvent> GetActiveStoryEventsForCharacter(string characterId) {
        lock (_lock) {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = """
                SELECT DISTINCT se.id, se.gameTime, se.realTime, se.type, se.dataJson
                FROM story_event se
                LEFT JOIN story_event_observer seo ON se.id = seo.storyEventId
                WHERE se.archivedTo IS NULL
                  AND (
                    json_extract(se.dataJson, '$.playerCharacterId') = $charId
                    OR json_extract(se.dataJson, '$.targetCharacterId') = $charId
                    OR json_extract(se.dataJson, '$.npcCharacterId') = $charId
                    OR seo.characterId = $charId
                  )
                ORDER BY se.id
                """;
            cmd.Parameters.AddWithValue("$charId", characterId);

            var results = new List<RawStoryEvent>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) {
                results.Add(new RawStoryEvent(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4)));
            }
            return results;
        }
    }

    public List<RawStoryEvent> GetRecentEvents(string type, string? excludeCharacterId, int limit) {
        lock (_lock) {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = """
                SELECT se.id, se.gameTime, se.realTime, se.type, se.dataJson
                FROM story_event se
                WHERE se.type = $type
                  AND ($exclude IS NULL OR (
                    COALESCE(json_extract(se.dataJson, '$.npcCharacterId'), '') != $exclude
                    AND NOT EXISTS (
                      SELECT 1 FROM story_event_observer seo
                      WHERE seo.storyEventId = se.id AND seo.characterId = $exclude)))
                ORDER BY se.id DESC
                LIMIT $limit
                """;
            cmd.Parameters.AddWithValue("$type", type);
            cmd.Parameters.AddWithValue("$exclude", (object?)excludeCharacterId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$limit", limit);

            var results = new List<RawStoryEvent>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) {
                results.Add(new RawStoryEvent(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4)));
            }
            return results;
        }
    }

    public List<RawStoryEventSummary> GetActiveSummariesForCharacter(string characterId) {
        lock (_lock) {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = """
                SELECT DISTINCT s.id, s.summary, s.realTime
                FROM story_event_summary s
                JOIN story_event_summary_observer so ON s.id = so.summaryId
                WHERE so.characterId = $charId AND s.archivedTo IS NULL
                ORDER BY s.id
                """;
            cmd.Parameters.AddWithValue("$charId", characterId);

            var results = new List<RawStoryEventSummary>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) {
                results.Add(new RawStoryEventSummary(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2)));
            }
            return results;
        }
    }

    public long AddStoryEventSummary(string summary, string realTime, string[] characterIds) {
        lock (_lock) {
            using var tx = _db.Connection.BeginTransaction();

            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO story_event_summary (summary, realTime)
                VALUES ($summary, $realTime)
                RETURNING id
                """;
            cmd.Parameters.AddWithValue("$summary", summary);
            cmd.Parameters.AddWithValue("$realTime", realTime);
            var summaryId = (long)cmd.ExecuteScalar()!;

            foreach (var characterId in characterIds) {
                using var obsCmd = _db.Connection.CreateCommand();
                obsCmd.CommandText = "INSERT INTO story_event_summary_observer (summaryId, characterId) VALUES ($summaryId, $charId)";
                obsCmd.Parameters.AddWithValue("$summaryId", summaryId);
                obsCmd.Parameters.AddWithValue("$charId", characterId);
                obsCmd.ExecuteNonQuery();
            }

            tx.Commit();
            return summaryId;
        }
    }

    public void ArchiveStoryEvents(long[] eventIds, long summaryId) {
        if (eventIds.Length == 0) {
            return;
        }

        lock (_lock) {
            using var tx = _db.Connection.BeginTransaction();
            foreach (var eventId in eventIds) {
                using var cmd = _db.Connection.CreateCommand();
                cmd.CommandText = "UPDATE story_event SET archivedTo = $summaryId WHERE id = $eventId";
                cmd.Parameters.AddWithValue("$summaryId", summaryId);
                cmd.Parameters.AddWithValue("$eventId", eventId);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
    }

    public void ArchiveStoryEventSummaries(long[] summaryIds, long newSummaryId) {
        if (summaryIds.Length == 0) {
            return;
        }

        lock (_lock) {
            using var tx = _db.Connection.BeginTransaction();
            foreach (var summaryId in summaryIds) {
                using var cmd = _db.Connection.CreateCommand();
                cmd.CommandText = "UPDATE story_event_summary SET archivedTo = $newSummaryId WHERE id = $summaryId";
                cmd.Parameters.AddWithValue("$newSummaryId", newSummaryId);
                cmd.Parameters.AddWithValue("$summaryId", summaryId);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
    }

    public long AddNpcTask(string npcId, string description, string? reward, string gameTime) {
        lock (_lock) {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO npc_task (npcId, description, reward, status, createdGameTime)
                VALUES ($npcId, $description, $reward, $status, $gameTime)
                RETURNING id
                """;
            cmd.Parameters.AddWithValue("$npcId", npcId);
            cmd.Parameters.AddWithValue("$description", description);
            cmd.Parameters.AddWithValue("$reward", (object?)reward ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$status", nameof(NpcTaskStatus.Open));
            cmd.Parameters.AddWithValue("$gameTime", gameTime);
            return (long)cmd.ExecuteScalar()!;
        }
    }

    public List<RawNpcTask> GetOpenNpcTasks(string npcId) {
        lock (_lock) {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "SELECT id, description, reward FROM npc_task WHERE npcId = $npcId AND status = $status ORDER BY id";
            cmd.Parameters.AddWithValue("$npcId", npcId);
            cmd.Parameters.AddWithValue("$status", nameof(NpcTaskStatus.Open));

            var results = new List<RawNpcTask>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) {
                results.Add(new RawNpcTask(reader.GetInt64(0), npcId, reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
            }
            return results;
        }
    }

    public RawNpcTask? GetOpenNpcTask(long taskId) {
        lock (_lock) {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "SELECT id, npcId, description, reward FROM npc_task WHERE id = $id AND status = $status";
            cmd.Parameters.AddWithValue("$id", taskId);
            cmd.Parameters.AddWithValue("$status", nameof(NpcTaskStatus.Open));
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadTask(reader) : null;
        }
    }

    public void AddQuestSteps(long taskId, (string NpcId, string Goal)[] steps) {
        lock (_lock) {
            using var tx = _db.Connection.BeginTransaction();
            for (var i = 0; i < steps.Length; i++) {
                using var cmd = _db.Connection.CreateCommand();
                cmd.CommandText = "INSERT INTO quest_step (taskId, idx, npcId, goal) VALUES ($taskId, $idx, $npcId, $goal)";
                cmd.Parameters.AddWithValue("$taskId", taskId);
                cmd.Parameters.AddWithValue("$idx", i);
                cmd.Parameters.AddWithValue("$npcId", steps[i].NpcId);
                cmd.Parameters.AddWithValue("$goal", steps[i].Goal);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
    }

    public List<RawQuestStep> GetQuestSteps(long taskId) {
        lock (_lock) {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "SELECT idx, npcId, goal, done FROM quest_step WHERE taskId = $taskId ORDER BY idx";
            cmd.Parameters.AddWithValue("$taskId", taskId);
            var steps = new List<RawQuestStep>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) {
                steps.Add(new RawQuestStep(taskId, reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetInt64(3) != 0));
            }
            return steps;
        }
    }

    public bool MarkQuestStepDone(long taskId, int index) {
        lock (_lock) {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "UPDATE quest_step SET done = 1 WHERE taskId = $taskId AND idx = $idx AND done = 0";
            cmd.Parameters.AddWithValue("$taskId", taskId);
            cmd.Parameters.AddWithValue("$idx", index);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    public List<RawNpcTask> GetQuestsAwaiting(string npcId) {
        lock (_lock) {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = """
                SELECT t.id, t.npcId, t.description, t.reward
                FROM npc_task t
                JOIN quest_step s ON s.taskId = t.id
                WHERE t.status = $open AND s.npcId = $npcId AND s.done = 0
                  AND s.idx = (SELECT MIN(idx) FROM quest_step WHERE taskId = t.id AND done = 0)
                ORDER BY t.id
                """;
            cmd.Parameters.AddWithValue("$open", nameof(NpcTaskStatus.Open));
            cmd.Parameters.AddWithValue("$npcId", npcId);
            var tasks = new List<RawNpcTask>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) {
                tasks.Add(ReadTask(reader));
            }
            return tasks;
        }
    }

    private static RawNpcTask ReadTask(Microsoft.Data.Sqlite.SqliteDataReader reader) =>
        new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3));

    public bool CloseNpcTask(long taskId, string npcId, NpcTaskStatus status, string gameTime) {
        lock (_lock) {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = """
                UPDATE npc_task SET status = $status, closedGameTime = $gameTime
                WHERE id = $id AND npcId = $npcId AND status = $open
                """;
            cmd.Parameters.AddWithValue("$status", status.ToString());
            cmd.Parameters.AddWithValue("$gameTime", gameTime);
            cmd.Parameters.AddWithValue("$id", taskId);
            cmd.Parameters.AddWithValue("$npcId", npcId);
            cmd.Parameters.AddWithValue("$open", nameof(NpcTaskStatus.Open));
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    public RawNpcInfo? GetNpcInfo(string npcId) {
        string? json;
        lock (_lock) {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "SELECT dataJson FROM npc_new WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", npcId);
            json = cmd.ExecuteScalar() as string;
        }
        if (json == null) {
            return null;
        }

        return ToNpcInfo(npcId, json);
    }

    public List<RawNpcInfo> GetKnownNpcs() {
        var rows = new List<(string Id, string Json)>();
        lock (_lock) {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "SELECT id, dataJson FROM npc_new";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) {
                rows.Add((reader.GetString(0), reader.GetString(1)));
            }
        }
        return rows.Select(r => ToNpcInfo(r.Id, r.Json)).OfType<RawNpcInfo>().ToList();
    }

    private static RawNpcInfo? ToNpcInfo(string npcId, string json) {
        var data = JsonSerializer.Deserialize(json, NpcDataJsonContext.Default.NpcDataJson);
        return data == null ? null : new RawNpcInfo(npcId, data.Name, data.Race, data.Sex, data.ClassName, data.Faction, data.FactionRank,
            data.Personality, data.LastCell, data.LastSeenGameSeconds);
    }

    public void SaveNpcInfo(RawNpcInfo info) {
        var json = JsonSerializer.Serialize(new NpcDataJson(info.Name, info.Race, info.Sex, info.ClassName, info.Faction, info.FactionRank, info.Personality,
            info.LastCell, info.LastSeenGameSeconds), NpcDataJsonContext.Default.NpcDataJson);
        lock (_lock) {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO npc_new (id, dataJson) VALUES ($id, $data)
                ON CONFLICT(id) DO UPDATE SET dataJson = $data
                """;
            cmd.Parameters.AddWithValue("$id", info.Id);
            cmd.Parameters.AddWithValue("$data", json);
            cmd.ExecuteNonQuery();
        }
    }

    public void AddCompanion(string npcId, string gameTime) {
        lock (_lock) {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "INSERT INTO companion (npcId, sinceGameTime) VALUES ($npcId, $gameTime) ON CONFLICT(npcId) DO NOTHING";
            cmd.Parameters.AddWithValue("$npcId", npcId);
            cmd.Parameters.AddWithValue("$gameTime", gameTime);
            cmd.ExecuteNonQuery();
        }
    }

    public void RemoveCompanion(string npcId) {
        lock (_lock) {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "DELETE FROM companion WHERE npcId = $npcId";
            cmd.Parameters.AddWithValue("$npcId", npcId);
            cmd.ExecuteNonQuery();
        }
    }

    public string[] GetCompanions() {
        lock (_lock) {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "SELECT npcId FROM companion ORDER BY sinceGameTime, npcId";
            var ids = new List<string>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) {
                ids.Add(reader.GetString(0));
            }
            return [.. ids];
        }
    }

    public void Dispose() {
        lock (_lock) {
            _db.Dispose();
        }
    }
}

internal record NpcDataJson(string Name, string Race, string Sex,
    string? ClassName = null, string? Faction = null, string? FactionRank = null, string? Personality = null,
    string? LastCell = null, double? LastSeenGameSeconds = null);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(NpcDataJson))]
internal partial class NpcDataJsonContext : JsonSerializerContext;
