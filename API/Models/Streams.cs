using ProtoBuf;

namespace API.Models;

[ProtoContract]
public class ArriveAtStation
{
    [ProtoMember(1)]
    public uint StationId { get; set; }

    [ProtoMember(2)]
    public uint Time { get; set; }
}

[ProtoContract]
public class EndCharging
{
    [ProtoMember(1)]
    public uint StationId { get; set; }

    [ProtoMember(2)]
    public uint Time { get; set; }
}

[ProtoContract]
public class StateSnapShot
{
    [ProtoMember(1)]
    public uint TotalEVs { get; set; }

    [ProtoMember(2)]
    public uint TotalCharging { get; set; }
}