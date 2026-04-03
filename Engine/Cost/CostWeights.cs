namespace Engine.Cost;

public record CostWeights(
    float PriceSensitivity = 0.4f,
    float PathDeviation = 0.8f,
    float EffectiveQueueSize = 1.0f,
    float Urgency = 0.5f,
    float ExpectedWaitTime = 0, // unchanged, as it is not yet implemented.
    float AvailableChargerRatio = 0 // unchanged, as it is not yet implemented.

// ...
);
