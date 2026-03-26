namespace Engine.Cost;

public record CostWeights(
    float PriceSensitivity = 0,
    float PathDeviation = 0,
    float EffectiveQueueSize = 0,
    float Urgency = 0,
    float ExpectedWaitTime = 0,
    float AvailableChargerRatio = 0
// ...
);
