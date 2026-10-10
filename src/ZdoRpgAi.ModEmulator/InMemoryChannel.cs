using System.Text.Json.Nodes;
using System.Threading.Channels;
using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Channel;
using Message = ZdoRpgAi.Protocol.Channel.Message;

namespace ZdoRpgAi.ModEmulator;

// One end of a connection inside the process. Messages go through JSON text, as over a WebSocket,
// so a payload that wouldn't survive the wire fails here too.
public sealed class InMemoryChannel : IChannel {
    private static readonly ILog Log = Logger.Get<InMemoryChannel>();

    private readonly Channel<Message> _inbox = Channel.CreateUnbounded<Message>(new UnboundedChannelOptions { SingleReader = true });
    private InMemoryChannel? _peer;
    private int _closed;

    public event Action<Message>? MessageReceived;
    public event Action? Disconnected;

    public static (InMemoryChannel A, InMemoryChannel B) CreatePair() {
        var a = new InMemoryChannel();
        var b = new InMemoryChannel();
        a._peer = b;
        b._peer = a;
        return (a, b);
    }

    public void SendMessage(Message msg) {
        if (_peer == null || Volatile.Read(ref _closed) != 0) {
            Log.Warn("Dropped {Type}: the channel is closed", msg.Type);
            return;
        }
        var json = JsonNode.Parse(msg.ToJson().ToJsonString())!.AsObject();
        _peer._inbox.Writer.TryWrite(Message.FromJson(json, msg.Binary?.ToArray()));
    }

    public async Task RunAsync() {
        try {
            await foreach (var msg in _inbox.Reader.ReadAllAsync()) {
                try {
                    MessageReceived?.Invoke(msg);
                }
                catch (Exception ex) {
                    Log.Error("Handling {Type} failed: {Error}", msg.Type, ex.ToString());
                }
            }
        }
        finally {
            Disconnected?.Invoke();
        }
    }

    public void Close() {
        if (Interlocked.Exchange(ref _closed, 1) != 0) {
            return;
        }
        _inbox.Writer.TryComplete();
        _peer?.Close();
    }
}
