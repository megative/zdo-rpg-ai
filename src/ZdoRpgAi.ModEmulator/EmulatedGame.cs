using System.Text.Json.Nodes;
using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Channel;
using ZdoRpgAi.Protocol.Messages;
using ZdoRpgAi.Protocol.Rpc;

namespace ZdoRpgAi.ModEmulator;

public enum GameEventKind {
    PlayerSaid, NpcSaid, Gave, Took, Disposition, Follow, StopFollow, Attack, StopAttack,
    TaskGiven, TaskUpdated, TaskDone, TaskCancelled, Dialogue, Service, MessageBox, Moved,
}

// Something that happened in the emulated game. Text is the line, the item, the task or the
// reason; Value a count or a change of disposition; TargetId whom a line was for, null for the
// player; Success false when the game refused the action.
public record GameEvent(GameEventKind Kind, string? NpcId, string Text, int Value = 0, string? TargetId = null, bool Success = true);

// Plays the mod's part for the server: answers its questions from the world, carries out what NPCs
// do and keeps a record of it. Talks to the server directly or through the client, which turns the
// NPCs' voiced lines into SayMp3File.
public class EmulatedGame {
    private static readonly ILog Log = Logger.Get<EmulatedGame>();

    private readonly RpcChannel _rpc;
    private readonly List<GameEvent> _events = [];
    private string? _targetNpcId;
    private long _lastActivity = Environment.TickCount64;

    public event Action<GameEvent>? EventAdded;

    public EmulatedGame(IChannel channel, EmulatedWorld world) {
        World = world;
        _rpc = new RpcChannel(channel);
        _rpc.MessageReceived += OnMessageReceived;
        _rpc.Disconnected += () => Log.Info("Disconnected");
    }

    public EmulatedWorld World { get; }
    public string? Target => _targetNpcId;

    public IReadOnlyList<GameEvent> Events {
        get {
            lock (_events) {
                return _events.ToList();
            }
        }
    }

    // target: the NPC the player looks at from the start, like one who has just walked up to it
    public async Task RunAsync(CancellationToken ct, string? target = null) {
        Publish(nameof(ModToServerMessageType.PlayerAdded), new PlayerAddedPayload(EmulatedWorld.PlayerId), PayloadJsonContext.Default.PlayerAddedPayload);
        PublishCellChange();
        if (target != null) {
            SetTarget(target);
        }
        using var reg = ct.Register(() => _rpc.Close());
        await _rpc.RunAsync();
    }

    public void Say(string text) {
        Touch();
        Record(new GameEvent(GameEventKind.PlayerSaid, null, text, TargetId: _targetNpcId));
        Publish(nameof(ClientToServerMessageType.PlayerSpeaksText),
            new PlayerSpeaksTextPayload(EmulatedWorld.PlayerId, text, _targetNpcId, World.GameTime), PayloadJsonContext.Default.PlayerSpeaksTextPayload);
        Log.Info("Player says: \"{Text}\" (target: {Target})", text, _targetNpcId ?? "(none)");
    }

    // Looking at an NPC: the next lines go to it, and one that remembers the player greets them
    public void SetTarget(string? npcId) {
        Touch();
        _targetNpcId = npcId;
        Publish(nameof(ModToServerMessageType.TargetChanged), new TargetChangedPayload(EmulatedWorld.PlayerId, npcId), PayloadJsonContext.Default.TargetChangedPayload);
        Log.Info(npcId != null ? "Target set to: {NpcId}" : "Target cleared", npcId ?? "");
    }

    // The player walks into another cell, taking whoever follows them along
    public void MoveTo(string cell) {
        Touch();
        World.MovePlayer(cell);
        _targetNpcId = null;
        Record(new GameEvent(GameEventKind.Moved, null, cell));
        PublishCellChange();
    }

    // Waits until the server has sent nothing for `quiet` while busy() is false; false on timeout
    public async Task<bool> WaitForQuietAsync(TimeSpan quiet, TimeSpan timeout, Func<bool>? busy = null) {
        var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        while (Environment.TickCount64 < deadline) {
            var idleMs = Environment.TickCount64 - Interlocked.Read(ref _lastActivity);
            if (idleMs >= quiet.TotalMilliseconds && busy?.Invoke() != true) {
                return true;
            }
            await Task.Delay(20);
        }
        return false;
    }

    private void Touch() => Interlocked.Exchange(ref _lastActivity, Environment.TickCount64);

    private void Record(GameEvent evt) {
        lock (_events) {
            _events.Add(evt);
        }
        EventAdded?.Invoke(evt);
    }

    private void PublishCellChange() {
        var cell = World.PlayerCell;
        Publish(nameof(ModToServerMessageType.CellChange), new CellChangePayload(EmulatedWorld.PlayerId, cell), PayloadJsonContext.Default.CellChangePayload);
        Log.Info("Cell: {Cell}", cell);
    }

    private void Publish<T>(string type, T payload, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo) =>
        _rpc.Publish(type, JsonExtensions.SerializeToObject(payload, typeInfo));

    private void Respond<T>(Message msg, T payload, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo) =>
        _rpc.Respond(msg.Type, msg.Id, JsonExtensions.SerializeToObject(payload, typeInfo));

    private void RespondResult(Message msg, ActionResultPayload result) =>
        Respond(msg, result, PayloadJsonContext.Default.ActionResultPayload);

    private void OnMessageReceived(Message msg) {
        Touch();
        Log.Debug("Received: {Type} (id={Id})", msg.Type, msg.Id);

        switch (msg.Type) {
            case nameof(ServerToModMessageType.GetNpcInfo): {
                    var req = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.GetNpcInfoRequestPayload);
                    var info = req == null ? null : World.NpcInfo(req.NpcId);
                    if (info == null) {
                        Log.Warn("NPC not found: {NpcId}", req?.NpcId ?? "?");
                        _rpc.Respond(msg.Type, msg.Id, new JsonObject());
                        break;
                    }
                    Respond(msg, info, PayloadJsonContext.Default.GetNpcInfoResponsePayload);
                    break;
                }
            case nameof(ServerToModMessageType.GetPlayerInfo):
                Respond(msg, World.PlayerInfo, PayloadJsonContext.Default.GetPlayerInfoResponsePayload);
                break;
            case nameof(ServerToModMessageType.GetCharactersWhoHear): {
                    var req = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.GetCharactersWhoHearRequestPayload);
                    if (req == null) {
                        break;
                    }
                    var characters = World.WhoHears(req.CharacterId, req.MaxDistanceMeters);
                    var cell = World.CellOf(req.CharacterId) ?? World.PlayerCell;
                    _rpc.Respond(nameof(ModToServerMessageType.GetCharactersWhoHearResponse), msg.Id, JsonExtensions.SerializeToObject(
                        new GetCharactersWhoHearResponsePayload(characters, World.GameTime, World.SaveId, cell, World.GameSeconds),
                        PayloadJsonContext.Default.GetCharactersWhoHearResponsePayload));
                    Log.Debug("GetCharactersWhoHear ({CharacterId}, {Range}): {Count} nearby",
                        req.CharacterId, req.MaxDistanceMeters?.ToString("F0") ?? "default", characters.Length);
                    break;
                }
            case nameof(ServerToModMessageType.GetSceneContext): {
                    var req = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.GetSceneContextRequestPayload);
                    if (req != null) {
                        Respond(msg, World.SceneContext(req.NpcId), PayloadJsonContext.Default.GetSceneContextResponsePayload);
                    }
                    break;
                }
            case nameof(ServerToModMessageType.GetTopics):
                Respond(msg, new GetTopicsResponsePayload(World.TopicIds), PayloadJsonContext.Default.GetTopicsResponsePayload);
                break;
            case nameof(ServerToModMessageType.GetTopicLines): {
                    var req = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.GetTopicLinesRequestPayload);
                    var lines = req == null ? [] : World.TopicLines(req.Topics, req.Limit);
                    Respond(msg, new GetTopicLinesResponsePayload(lines), PayloadJsonContext.Default.GetTopicLinesResponsePayload);
                    break;
                }
            case nameof(ServerToModMessageType.TransferItem): {
                    var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.TransferItemPayload);
                    if (p == null) {
                        RespondResult(msg, new ActionResultPayload(false, "bad request"));
                        break;
                    }
                    var result = World.TransferItem(p);
                    var toPlayer = p.ToCharacterId == EmulatedWorld.PlayerId;
                    Record(new GameEvent(toPlayer ? GameEventKind.Gave : GameEventKind.Took, toPlayer ? p.FromCharacterId : p.ToCharacterId,
                        World.ItemName(p.ItemId), p.Count, Success: result.Success));
                    Log.Info("[MOD] Transfer {ItemId} x{Count} from {From} to {To}: {Result}", p.ItemId, p.Count, p.FromCharacterId, p.ToCharacterId, result.Error ?? "done");
                    RespondResult(msg, result);
                    break;
                }
            case nameof(ServerToModMessageType.ModifyDisposition): {
                    var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.ModifyDispositionPayload);
                    var result = p == null ? new ActionResultPayload(false, "bad request") : World.ModifyDisposition(p);
                    if (p != null) {
                        Record(new GameEvent(GameEventKind.Disposition, p.NpcId, p.Reason ?? "", p.Delta, Success: result.Success));
                    }
                    Log.Info("[MOD] Disposition of {NpcId} {Delta:+0;-0} ({Reason}): {Result}", p?.NpcId ?? "?", p?.Delta ?? 0, p?.Reason ?? "no reason", result.Error ?? "done");
                    RespondResult(msg, result);
                    break;
                }
            case nameof(ServerToModMessageType.NpcStartFollowCharacter): {
                    var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.NpcStartFollowCharacterPayload);
                    var result = p == null ? new ActionResultPayload(false, "bad request") : World.Follow(p.NpcId, true);
                    if (p != null) {
                        Record(new GameEvent(GameEventKind.Follow, p.NpcId, "", Success: result.Success));
                        Log.Info("[MOD] {NpcId} starts following {Target}", p.NpcId, p.TargetCharacterId);
                    }
                    RespondResult(msg, result);
                    break;
                }
            case nameof(ServerToModMessageType.NpcStopFollowCharacter): {
                    var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.NpcStopFollowCharacterPayload);
                    var result = p == null ? new ActionResultPayload(false, "bad request") : World.Follow(p.NpcId, false);
                    if (p != null) {
                        Record(new GameEvent(GameEventKind.StopFollow, p.NpcId, "", Success: result.Success));
                        Log.Info("[MOD] {NpcId} stops following", p.NpcId);
                    }
                    RespondResult(msg, result);
                    break;
                }
            case nameof(ServerToModMessageType.NpcAttack): {
                    var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.NpcAttackPayload);
                    if (p != null) {
                        Record(new GameEvent(GameEventKind.Attack, p.NpcId, ""));
                        Log.Info("[MOD] {NpcId} attacks {Target}", p.NpcId, p.TargetCharacterId);
                    }
                    RespondResult(msg, new ActionResultPayload(p != null));
                    break;
                }
            case nameof(ServerToModMessageType.NpcStopAttack): {
                    var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.NpcStopAttackPayload);
                    if (p != null) {
                        Record(new GameEvent(GameEventKind.StopAttack, p.NpcId, ""));
                        Log.Info("[MOD] {NpcId} stops attacking", p.NpcId);
                    }
                    RespondResult(msg, new ActionResultPayload(p != null));
                    break;
                }
            case nameof(ServerToModMessageType.TaskUpdate): {
                    var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.TaskUpdatePayload);
                    if (p != null) {
                        var kind = p.Status switch {
                            "Updated" => GameEventKind.TaskUpdated,
                            "Done" => GameEventKind.TaskDone,
                            "Cancelled" => GameEventKind.TaskCancelled,
                            _ => GameEventKind.TaskGiven,
                        };
                        Record(new GameEvent(kind, p.NpcId, p.Reward == null ? p.Description : $"{p.Description} ({p.Reward})"));
                        Log.Info("[MOD] Task #{TaskId} {Status} from {NpcId}: {Description} (reward: {Reward})",
                            p.TaskId?.ToString() ?? "?", p.Status, p.NpcId, p.Description, p.Reward ?? "none");
                    }
                    break;
                }
            case nameof(ServerToModMessageType.OpenDialogue): {
                    var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.OpenDialoguePayload);
                    if (p != null) {
                        Record(new GameEvent(GameEventKind.Dialogue, p.NpcId, ""));
                        Log.Info("[MOD] {NpcId} opens the dialogue window", p.NpcId);
                    }
                    RespondResult(msg, new ActionResultPayload(p != null));
                    break;
                }
            case nameof(ServerToModMessageType.OpenService): {
                    var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.OpenServicePayload);
                    var result = p == null ? new ActionResultPayload(false, "bad request") : World.OpenService(p);
                    if (p != null) {
                        Record(new GameEvent(GameEventKind.Service, p.NpcId, p.Service, Success: result.Success));
                    }
                    Log.Info("[MOD] {NpcId} opens {Service}: {Result}", p?.NpcId ?? "?", p?.Service ?? "?", result.Error ?? "done");
                    RespondResult(msg, result);
                    break;
                }
            case nameof(ServerToModMessageType.ShowMessageBox): {
                    var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.ShowMessageBoxPayload);
                    if (p != null) {
                        Record(new GameEvent(GameEventKind.MessageBox, null, p.Message));
                        Log.Info("[MOD] MessageBox: {Message}", p.Message);
                    }
                    break;
                }
            // Straight from the server, or through the client, which saves the audio first
            case nameof(ServerToClientMessageType.NpcSpeaksMp3): {
                    var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.NpcSpeaksMp3Payload);
                    if (p != null) {
                        OnNpcSaid(p.NpcId, p.Text, p.TargetId);
                    }
                    break;
                }
            case nameof(ClientToModMessageType.SayMp3File): {
                    var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.SayMp3FilePayload);
                    if (p != null) {
                        OnNpcSaid(p.NpcId, p.Text, p.TargetId);
                    }
                    break;
                }
            case nameof(ServerToModMessageType.NpcThinking): {
                    var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.NpcThinkingPayload);
                    Log.Debug("[MOD] {NpcId}: ...", p?.NpcId ?? "?");
                    break;
                }
            case nameof(ServerToModMessageType.SpeechRecognitionInProgress):
            case nameof(ServerToModMessageType.SpeechRecognitionComplete):
            case nameof(ServerToModMessageType.SpawnOnGroundInFrontOfCharacter):
            case nameof(ServerToModMessageType.PlaySound3dOnCharacter):
            case nameof(ClientToModMessageType.SetPushToTalkKey):
            case nameof(ClientToModMessageType.SetEventLogKey):
            case nameof(ClientToModMessageType.SetTextInputKey):
            case nameof(ClientToBothMessageType.PlayerStartSpeak):
            case nameof(ClientToBothMessageType.PlayerStopSpeak):
                Log.Debug("[MOD] {Type}", msg.Type);
                break;
            default:
                Log.Warn("Unhandled message type: {Type}", msg.Type);
                break;
        }
    }

    private void OnNpcSaid(string npcId, string text, string? targetId) {
        Record(new GameEvent(GameEventKind.NpcSaid, npcId, text, TargetId: targetId));
        var name = World.FindNpc(npcId)?.Name ?? npcId;
        var to = targetId == null ? "" : $" (to {World.FindNpc(targetId)?.Name ?? targetId})";
        Log.Info("[MOD] {Name}{To}: \"{Text}\"", name, to, text);
    }
}
