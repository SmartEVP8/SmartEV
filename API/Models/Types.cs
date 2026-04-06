using ProtoBuf;

namespace API.Models;

[ProtoContract]
public class Position
{
    [ProtoMember(1)]
    public double Lat { get; set; }

    [ProtoMember(2)]
    public double Lon { get; set; }
}