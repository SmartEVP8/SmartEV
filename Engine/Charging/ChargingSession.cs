// ChargerState.cs
namespace Engine.Charging;

using Core.Charging;
using Core.Charging.ChargingModel;
using Core.Charging.ChargingModel.Chargepoint;

public record ChargingSession(
    uint EVId,
    ConnectedCar Car,
    uint StartTime,
    ChargingSide? Side);

public class ChargerState(ChargerBase charger)
{
    public ChargerBase Charger { get; } = charger;
    public Queue<(uint EVId, ConnectedCar Car)> Queue { get; } = new();
    public ChargingSession? SessionA { get; set; }
    public ChargingSession? SessionB { get; set; }
    public IntegrationResult? LastResult { get; set; }

    public bool IsFree => charger switch
    {
        SingleCharger => SessionA is null,
        DualCharger => SessionA is null || SessionB is null,
        _ => false
    };
}