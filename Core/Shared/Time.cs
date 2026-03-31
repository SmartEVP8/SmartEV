namespace Core.Shared;
/// <summary>
/// Simple time wrapper with implicit conversion between uint and Time.
/// The unit of <see cref="T"/> is <b>seconds</b>.
/// https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/user-defined-conversion-operators
/// </summary>
/// <param name="T"></param>
public readonly record struct Time(uint T) : IComparable<Time>
{
    /// <inheritdoc/>
    public int CompareTo(Time other) => T.CompareTo(other.T);

    // Implicitly convert int → Time
    public static implicit operator Time(uint t) => new(t);

    // Implicitly convert Time → uint
    public static implicit operator uint(Time t) => t.T;

    /// <summary>
    /// Seconds in a minute.
    /// </summary>
    public const uint SecondsPerMinute = 60;

    /// <summary>
    /// Seconds in an hour.
    /// </summary>
    public const uint SecondsPerHour = SecondsPerMinute * 60;

    /// <summary>
    /// Seconds in a day.
    /// </summary>
    public const uint SecondsPerDay = SecondsPerHour * 24;

    /// <summary>
    /// Seconds in a week.
    /// </summary>
    public const uint SecondsPerWeek = SecondsPerDay * 7;

    /// <summary>
    /// Gets the 0-based day-of-week index for this timestamp.
    /// 0 = Sunday … 6 = Saturday (epoch is Monday).
    /// </summary>
    public uint DayOfWeekIndex => (T / SecondsPerDay) % 7;

    /// <summary>
    /// Gets the day of the week for this timestamp.
    /// Epoch (T = 0) is Sunday.
    /// </summary>
    public DayOfWeek DayOfWeek => DayOfWeekIndex switch
    {
        0 => DayOfWeek.Sunday,
        1 => DayOfWeek.Monday,
        2 => DayOfWeek.Tuesday,
        3 => DayOfWeek.Wednesday,
        4 => DayOfWeek.Thursday,
        5 => DayOfWeek.Friday,
        6 => DayOfWeek.Saturday,
        _ => throw new InvalidOperationException("Unreachable")
    };

    /// <summary>
    /// Gets the number of seconds elapsed since the start of the current day (00:00).
    /// </summary>
    public uint SecondsIntoDay => T % SecondsPerDay;

    /// <summary>
    /// Gets the hour component (0–23) of the current time of day.
    /// </summary>
    public uint Hour => SecondsIntoDay / SecondsPerHour;

    /// <summary>
    /// Gets the minute component (0–59) of the current time of day.
    /// </summary>
    public uint Minute => (SecondsIntoDay % SecondsPerHour) / SecondsPerMinute;

    /// <summary>
    /// Gets the second component (0–59) of the current time of day.
    /// </summary>
    public uint Second => SecondsIntoDay % SecondsPerMinute;

    /// <summary>
    /// Gets the total number of complete days elapsed since the epoch.
    /// </summary>
    public uint TotalDays => T / SecondsPerDay;

    /// <summary>
    /// Gets the total number of complete weeks elapsed since the epoch.
    /// </summary>
    public uint TotalWeeks => T / SecondsPerWeek;

    /// <summary>
    /// Constructs a <see cref="Time"/> from explicit day, hour, minute, and second components.
    /// </summary>
    /// <param name="day">Zero-based day offset from the epoch (0 = Sunday).</param>
    /// <param name="hour">Hour of day (0–23).</param>
    /// <param name="minute">Minute of hour (0–59).</param>
    /// <param name="second">Second of minute (0–59).</param>
    /// <returns>A <see cref="Time"/> representing the given point in simulation time.</returns>
    public static Time From(uint day, uint hour = 0, uint minute = 0, uint second = 0)
        => new((day * SecondsPerDay) + (hour * SecondsPerHour) + (minute * SecondsPerMinute) + second);

    /// <summary>
    /// <returns>Returns a human-readable representation of this timestamp, e.g. <c>"Monday 08:30:00 (day 1)"</c>.</returns>
    /// </summary>
    /// <returns>Human readable time.</returns>
    public override string ToString()
        => $"{DayOfWeek} {Hour:D2}:{Minute:D2}:{Second:D2} (day {TotalDays})";
}
