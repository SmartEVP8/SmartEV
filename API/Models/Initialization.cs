using ProtoBuf;

namespace API.Models;

// Init Messages
[ProtoContract]
public class WeightRanges
{
    [ProtoMember(1)]
    public int Id { get; set; }

    [ProtoMember(2)]
    public float Minimum { get; set; }

    [ProtoMember(3)]
    public float Maximum { get; set; }
}

[ProtoContract]
public class Chargers
{
    [ProtoMember(1)]
    public int Id { get; set; }

    [ProtoMember(2)]
    public int MaxPowerKW { get; set; }

    [ProtoMember(3)]
    public bool IsDual { get; set; }

    [ProtoMember(4)]
    public int StationId { get; set; }
}

[ProtoContract]
public class StationInit
{
    [ProtoMember(1)]
    public uint Id { get; set; }

    [ProtoMember(2)]
    public Position? Pos { get; set; }

    [ProtoMember(3)]
    public string? Address { get; set; }
}

[ProtoContract]
public class Init
{
    [ProtoMember(1)]
    public WeightRanges? WeightRanges { get; set; }

    [ProtoMember(2)]
    public Chargers? Chargers { get; set; }

    [ProtoMember(3)]
    public StationInit? StationInit { get; set; }
}