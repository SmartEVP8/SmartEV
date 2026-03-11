namespace Core.Charging;

/// <summary>
/// Represents the result of a power allocation across one or two consumers.
/// </summary>
/// <param name="Allocated1">Power allocated to the first (or only) consumer.</param>
/// <param name="Allocated2">Power allocated to the second consumer. Zero (0) for single charging points.</param>
/// <param name="Wasted">Power that could not be consumed by any vehicle.</param>
public record PowerAllocation(double Allocated1, double Allocated2, double Wasted);