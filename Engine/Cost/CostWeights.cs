namespace Engine.Cost;

public record CostWeights(
    float PriceSensitivity = 0,
    float PathDeviation = 0,
    float ExpectedQueueSize = 0

// ...
);
