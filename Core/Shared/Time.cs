namespace Core.Shared;

/// <summary>
/// Simple time wrapper with implicit conversion between int and time
/// https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/user-defined-conversion-operators
/// </summary>
/// <param name="T"></param>
public readonly record struct Time(uint T) : IComparable<Time>
{
    /// <inheritdoc/>
    public int CompareTo(Time other) => T.CompareTo(other.T);

    // Implicitly convert int → Time
    public static implicit operator Time(uint t) => new(t);

    // Implicitly convert Time → int
    public static implicit operator uint(Time t) => t.T;
}
