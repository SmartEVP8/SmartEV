namespace Core.Shared;
/// <summary>
/// Simple time wrapper with implicit conversion between uint and Time.
/// The unit of <see cref="Milliseconds"/> is <b>milliseconds</b>.
/// https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/user-defined-conversion-operators
/// </summary>
/// <param name="Milliseconds"></param>
public readonly record struct Time(uint Milliseconds) : IComparable<Time>
{
    /// <inheritdoc/>
    public int CompareTo(Time other) => Milliseconds.CompareTo(other.Milliseconds);

    // Implicitly convert int → Time
    public static implicit operator Time(uint t) => new(t);

    // Implicitly convert Time → uint
    public static implicit operator uint(Time t) => t.Milliseconds;

    /// <summary>
    /// Milliseconds in a second.
    /// </summary>
    public const uint MillisecondsPerSecond = 1000;

    /// <summary>
    /// Milliseconds in a minute.
    /// </summary>
    public const uint MillisecondsPerMinute = MillisecondsPerSecond * 60;

    /// <summary>
    /// Milliseconds in an hour.
    /// </summary>
    public const uint MillisecondsPerHour = MillisecondsPerMinute * 60;

    /// <summary>
    /// Milliseconds in a day.
    /// </summary>
    public const uint MillisecondsPerDay = MillisecondsPerHour * 24;

    /// <summary>
    /// Milliseconds in a week.
    /// </summary>
    public const uint MillisecondsPerWeek = MillisecondsPerDay * 7;

    /// <summary>
    /// Gets the 0-based day-of-week index for this timestamp.
    /// 0 = Sunday … 6 = Saturday (epoch is Monday).
    /// </summary>
    public uint DayOfWeekIndex => (Milliseconds / MillisecondsPerDay) % 7;

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
    /// Gets the number of milliseconds elapsed since the start of the current day (00:00).
    /// </summary>
    public uint MillisecondsIntoDay => Milliseconds % MillisecondsPerDay;

    /// <summary>
    /// Gets the hour component (0–23) of the current time of day.
    /// </summary>
    public uint Hours => MillisecondsIntoDay / MillisecondsPerHour;

    /// <summary>
    /// Gets the minute component (0–59) of the current time of day.
    /// </summary>
    public uint Minutes => (MillisecondsIntoDay % MillisecondsPerHour) / MillisecondsPerMinute;

    /// <summary>
    /// Gets the second component (0–59) of the current time of day.
    /// </summary>
    public uint Seconds => (MillisecondsIntoDay / MillisecondsPerSecond) % 60;

    /// <summary>
    /// Gets the millisecond component (0–999) of the current time of day.
    /// </summary>
    public uint Millisecond => MillisecondsIntoDay % MillisecondsPerSecond;

    /// <summary>
    ///  Gets the total number of complete seconds elapsed since the epoch.
    /// </summary>
    public uint TotalSeconds => Milliseconds / MillisecondsPerSecond;

    /// <summary>
    /// Gets the total number of complete days elapsed since the epoch.
    /// </summary>
    public uint TotalDays => Milliseconds / MillisecondsPerDay;

    /// <summary>
    /// Gets the total number of complete weeks elapsed since the epoch.
    /// </summary>
    public uint TotalWeeks => Milliseconds / MillisecondsPerWeek;

    /// <summary>
    /// Constructs a <see cref="Time"/> from explicit day, hour, minute, and second components.
    /// </summary>
    /// <param name="day">Zero-based day offset from the epoch (0 = Sunday).</param>
    /// <param name="hour">Hour of day (0–23).</param>
    /// <param name="minute">Minute of hour (0–59).</param>
    /// <param name="second">Second of minute (0–59).</param>
    /// <param name="millisecond">Millisecond of second (0–999).</param>
    /// <returns>A <see cref="Time"/> representing the given point in simulation time.</returns>
    public static Time From(uint day, uint hour = 0, uint minute = 0, uint second = 0, uint millisecond = 0)
        => new((day * MillisecondsPerDay) + (hour * MillisecondsPerHour) + (minute * MillisecondsPerMinute) + (second * MillisecondsPerSecond) + millisecond);

    /// <summary>
    /// <returns>Returns a human-readable representation of this timestamp, e.g. <c>"Monday 08:30:00 (day 1)"</c>.</returns>
    /// </summary>
    /// <returns>Human readable time.</returns>
    public override string ToString()
        => $"{DayOfWeek} {Hours:D2}:{Minutes:D2}:{Seconds:D2}.{Millisecond:D3} (day {TotalDays})";

    public static bool operator <(Time left, Time right) => left.TotalSeconds < right.TotalSeconds;
    public static bool operator >(Time left, Time right) => left.TotalSeconds > right.TotalSeconds;
    public static bool operator <=(Time left, Time right) => left.TotalSeconds <= right.TotalSeconds;
    public static bool operator >=(Time left, Time right) => left.TotalSeconds >= right.TotalSeconds;

    /// <summary>
    /// Adds two Time values together, returning a new Time.
    /// </summary>
    /// <param name="other">The compared time value in seconds.</param>
    /// <returns>A new Time representing the sum of the two input Times.</returns>
    public bool Equals(Time other) => TotalSeconds == other.TotalSeconds;

    /// <summary>
    /// Returns a hash code for this Time, based on its total number of seconds.
    /// </summary>
    /// <returns>A hash code for this Time.</returns>
    public override int GetHashCode() => TotalSeconds.GetHashCode();
}