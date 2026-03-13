namespace Core.Vehicles;

public record GetBattery(ushort MaxChargeRate, float CurrentCharge, ushort Capacity);