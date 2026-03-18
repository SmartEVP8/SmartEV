namespace Core.Shared;


/// <summary>
/// Simple time wrapper with implicit conversion between int and time
/// https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/user-defined-conversion-operators
/// </summary>
/// <param name="T"></param>
public readonly record struct Time(int T)
{
    // Implicitly convert int → Time
    public static implicit operator Time(int t) => new(t);

    // Implicitly convert Time → int
    public static implicit operator int(Time t) => t.T;
}
