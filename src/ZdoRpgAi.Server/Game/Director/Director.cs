using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Messages;
using ZdoRpgAi.Protocol.Rpc;
using ZdoRpgAi.Server.Bootstrap;
using ZdoRpgAi.Server.Game.Npc;
using ZdoRpgAi.Server.Game.Story;
using ZdoRpgAi.Server.Llm;
using ZdoRpgAi.Server.Util.Mp3;

namespace ZdoRpgAi.Server.Game.Director;

public class Director {
    private static readonly ILog Log = Logger.Get<Director>();

    private readonly Story.Story _story;
    private readonly DirectorHelper _directorHelper;
    private readonly NpcSpeechGenerator _npcSpeechGenerator;
    private readonly SimpleReactiveStrategy _simpleReactive;
    private readonly IRpcChannel _rpc;
    private readonly NpcRepository _npcRepo;
    private readonly object _bufferLock = new();
    private readonly List<StoryEvent> _buffer = [];
    private bool _processing;
    private int _playerInterruptionIteration = 0;

    private int PlayerInterruptionIteration => Volatile.Read(ref _playerInterruptionIteration);

    public Director(Story.Story story, DirectorHelper directorHelper, NpcSpeechGenerator npcSpeechGenerator, IRpcChannel rpc, ILlm mainLlm, ILlm simpleLlm, NpcRepository npcRepo, DirectorSection config) {
        _story = story;
        _directorHelper = directorHelper;
        _npcSpeechGenerator = npcSpeechGenerator;
        _rpc = rpc;
        _npcRepo = npcRepo;
        _simpleReactive = new SimpleReactiveStrategy(mainLlm, simpleLlm, story, npcRepo, rpc, directorHelper, config);
        story.EventRegistered += OnStoryEventRegistered;
    }

    private void OnStoryEventRegistered(StoryEvent evt) {
        Log.Debug("OnStoryEventRegistered");

        if (evt is StoryEvent.PlayerSpeak) {
            // The player said something new: an NPC line still being voiced or played back is stale
            Interlocked.Increment(ref _playerInterruptionIteration);
        }

        lock (_bufferLock) {
            _buffer.Add(evt);
            if (_processing) {
                Log.Trace("Buffered event {EventType} while processing", evt.GetType().Name);
                return;
            }

            _processing = true;
        }

        Log.Trace("Starting drain for event {EventType}", evt.GetType().Name);
        _ = DrainBufferAsync();
    }

    private async Task DrainBufferAsync() {
        while (true) {
            List<StoryEvent> batch;
            lock (_bufferLock) {
                if (_buffer.Count == 0) {
                    _processing = false;
                    Log.Trace("Buffer drained, processing complete");
                    return;
                }

                batch = [.. _buffer];
                _buffer.Clear();
            }

            if (batch.Count > 0) {
                Log.Trace("Draining batch of {Count} events", batch.Count);
                await ProcessStoryEventsAsync(batch);
            }
        }
    }

    private async Task ProcessStoryEventsAsync(List<StoryEvent> events) {
        Log.Trace("Received {Count} story events", events.Count);

        var strategy = DetermineStrategy(events);
        if (strategy == null) {
            Log.Trace("No strategy decided");
            return;
        }

        Log.Trace("Using strategy {Strategy}", strategy.GetType().Name);
        try {
            var newEvents = await strategy.ProcessStoryEventsAsync(events);
            Log.Trace("Strategy returned {Count} new events", newEvents.Count);
            await RegisterAndPublishAsync(newEvents);
        }
        catch (Exception ex) {
            Log.Error("Strategy {Strategy} failed: {Error}", strategy.GetType().Name, ex.Message);
        }
    }

    private async Task RegisterAndPublishAsync(List<StoryEvent> events) {
        Log.Trace("Registering and publishing {Count} events", events.Count);
        var observersCache = new Dictionary<string, string[]>();
        var speeches = new List<StoryEvent.NpcSpeak>();

        foreach (var e in events) {
            var (mainCharId, targetCharId) = e switch {
                StoryEvent.PlayerSpeak ps => (ps.PlayerCharacterId, ps.TargetCharacterId),
                StoryEvent.NpcSpeak ns => (ns.NpcCharacterId, ns.TargetCharacterId),
                StoryEvent.NpcAction na => (na.NpcCharacterId, (string?)null),
                _ => ((string?)null, (string?)null),
            };

            if (mainCharId == null) {
                _story.RegisterEvent(e, []);
                continue;
            }

            if (!observersCache.TryGetValue(mainCharId, out var observerIds)) {
                observerIds = (await _directorHelper.QueryObserversAsync(mainCharId, targetCharId != null ? [targetCharId] : null)).Ids;
                observersCache[mainCharId] = observerIds;
            }

            _story.RegisterEvent(e, observerIds);

            if (e is StoryEvent.NpcSpeak npcSpeak) {
                Log.Info("NPC {NpcId} speaks: {Text}", npcSpeak.NpcCharacterId, npcSpeak.Text);
                speeches.Add(npcSpeak);
            }
        }

        foreach (var speech in speeches) {
            if (!await SpeakAsync(speech)) {
                break;
            }
        }
    }

    // Voices a line sentence by sentence: the next part is synthesized while the previous one plays,
    // so the NPC starts talking as soon as the first sentence is ready. Returns false when the
    // player interrupted.
    private async Task<bool> SpeakAsync(StoryEvent.NpcSpeak speech) {
        var npc = await _npcRepo.GetNpcInfoAsync(speech.NpcCharacterId);
        if (npc == null) {
            Log.Warn("Cannot get NPC info id={NpcId}", speech.NpcCharacterId);
            return true;
        }

        var iteration = PlayerInterruptionIteration;
        var chunks = SpeechChunks.Split(speech.Text);
        Log.Trace("Generating speech for NPC {NpcId} in {Count} parts", npc.Id, chunks.Count);

        var next = _npcSpeechGenerator.GenerateAsync(npc, chunks[0]);
        for (var i = 0; i < chunks.Count; i++) {
            var mp3 = await next;
            if (i + 1 < chunks.Count) {
                next = _npcSpeechGenerator.GenerateAsync(npc, chunks[i + 1]);
            }

            if (PlayerInterruptionIteration != iteration) {
                Log.Trace("Speech interrupted by player for NPC {NpcId}", npc.Id);
                return false;
            }
            if (mp3 == null) {
                continue;
            }

            Log.Trace("Publishing MP3 part {Part}/{Count} for NPC {NpcId}", i + 1, chunks.Count, npc.Id);
            _directorHelper.PublishNpcSpeaksMp3(npc.Id, chunks[i], mp3);

            // A little slack, so the next part never cuts off the tail of this one
            var durationMs = (int)((Mp3Duration.Estimate(mp3.Mp3Bytes) ?? 0) * 1000);
            if (durationMs > 0) {
                await WaitUnlessInterruptedAsync(durationMs + 150, iteration);
            }
        }
        return PlayerInterruptionIteration == iteration;
    }

    private async Task WaitUnlessInterruptedAsync(int durationMs, int expectedIteration) {
        const int pollIntervalMs = 100;
        var remaining = durationMs;
        while (remaining > 0) {
            if (PlayerInterruptionIteration != expectedIteration) {
                Log.Debug("Speech playback wait interrupted by player");
                return;
            }

            var delay = Math.Min(remaining, pollIntervalMs);
            await Task.Delay(delay);
            remaining -= delay;
        }
    }

    private IDirectorStrategy? DetermineStrategy(List<StoryEvent> events) {
        var lastEventType = events.Last().GetType().Name;
        Log.Trace("Determining strategy for last event type {EventType}", lastEventType);

        if (events.Last() is StoryEvent.PlayerSpeak or StoryEvent.PlayerApproach) {
            return _simpleReactive;
        }

        return null;
    }
}
