namespace Engine.Services;

using System.Threading.Channels;
using Engine.Protocol;

/// <summary>
/// Owns all bidirectional channels for Engine communication.
/// Engine: reads commands, writes snapshots and events.
/// API: writes commands, reads snapshots and events, awaits init data.
/// </summary>
public sealed class SimulationChannel
{
    // Engine reads
    public ChannelReader<SimulationCommand> CommandReader { get; }

    // Engine writes
    public ChannelWriter<SimulationSnapshot> SnapshotWriter { get; }

    public ChannelWriter<ProtocolEvent> EventWriter { get; }

    // API reads
    public ChannelReader<SimulationSnapshot> SnapshotReader { get; }

    public ChannelReader<ProtocolEvent> EventReader { get; }

    // API writes
    public ChannelWriter<SimulationCommand> CommandWriter { get; }

    public SimulationChannel()
    {
        var commandChannel = Channel.CreateBounded<SimulationCommand>(
            new BoundedChannelOptions(16) { FullMode = BoundedChannelFullMode.DropWrite });
        CommandReader = commandChannel.Reader;
        CommandWriter = commandChannel.Writer;

        var snapshotChannel = Channel.CreateBounded<SimulationSnapshot>(
            new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.DropOldest });
        SnapshotReader = snapshotChannel.Reader;
        SnapshotWriter = snapshotChannel.Writer;

        var eventChannel = Channel.CreateBounded<ProtocolEvent>(
            new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.DropWrite });
        EventReader = eventChannel.Reader;
        EventWriter = eventChannel.Writer;
    }
}
