namespace Core.Helper;

using Serilog.Events;

/// <summary>
/// Extends the Serilog logger to customise log outputs.
/// </summary>
public static class Log
{
    private static void Write(
        LogEventLevel level,
        int evId,
        uint time,
        string message,
        Exception? ex,
        params (string Key, object Value)[] extra)
    {
        var log = Serilog.Log.ForContext("evId", evId)
                     .ForContext("Time", time);

        foreach (var (key, value) in extra)
            log = log.ForContext(key, value, destructureObjects: true);

        log.Write(level, message, ex);
    }

    /// <summary>
    /// Writes an informational log message with the given event ID, time, message, and optional extra properties.
    /// </summary>
    /// <param name="evId">The ID of the EV associated with the log message.</param>
    /// <param name="time">The simulation time at which the log message is being written.</param>
    /// <param name="message">The log message template, which may include placeholders for structured logging.</param>
    /// <param name="extra">An optional array of key-value pairs to include as structured properties in the log message.</param>
    /// <example>
    /// Log.Info(evId: 42, time: 1000, message: "EV {evId} started charging at time {time}.", ("StationId", stationId));.
    /// </example>
    public static void Info(
        int evId, uint time, string message,
        params (string Key, object Value)[] extra)
        => Write(LogEventLevel.Information, evId, time, message, null, extra);

    /// <summary>
    /// Writes a warning log message with the given event ID, time, message, and optional extra properties.
    /// </summary>
    /// <param name="evId">The ID of the EV associated with the log message.</param>
    /// <param name="time">The simulation time at which the log message is being written.</param>
    /// <param name="message">The log message template, which may include placeholders for structured logging.</param>
    /// <param name="extra">An optional array of key-value pairs to include as structured properties in the log message.</param>
    /// <example>
    /// Log.Warn(evId: 42, time: 1500, message: "EV {evId} has been waiting for a charger for {waitTime} seconds.", ("WaitTime", waitTime));.
    /// </example>
    public static void Warn(
        int evId, uint time, string message,
        params (string Key, object Value)[] extra)
        => Write(LogEventLevel.Warning, evId, time, message, null, extra);

    /// <summary>
    /// Writes a verbose log message with the given event ID, time, message, and optional extra properties.
    /// </summary>
    /// <param name="evId">The ID of the EV associated with the log message.</param>
    /// <param name="time">The simulation time at which the log message is being written.</param>
    /// <param name="message">The log message template, which may include placeholders for structured logging.</param>
    /// <param name="extra">An optional array of key-value pairs to include as structured properties in the log message.</param>
    /// <example>
    /// Log.Verbose(evId: 42, time: 1200, message: "EV {evId} is currently at position {position}.", ("Position", evPosition));.
    /// </example>
    public static void Verbose(
        int evId, uint time, string message,
        params (string Key, object Value)[] extra)
        => Write(LogEventLevel.Verbose, evId, time, message, null, extra);

    /// <summary>
    /// Writes an error log message with the given event ID, time, exception, message, and optional extra properties, and returns the exception for convenience in throwing. Use this in a throw.
    /// </summary>
    /// <param name="evId">The ID of the EV associated with the log message.</param>
    /// <param name="time">The simulation time at which the log message is being written.</param>
    /// <param name="ex">The exception to log and return.</param>
    /// <param name="extra">An optional array of key-value pairs to include as structured properties in the log message.</param>
    /// <returns>The exception that was passed in, for convenience in throwing.</returns>
    /// <example>
    /// throw Log.Error(evId: 42, time: 1300, ex: new InvalidOperationException("EV cannot start charging."), message: "EV {evId} failed to start charging at time {time}.", ("Reason", "No available chargers"));.
    /// </example>
    public static Exception Error(
        int evId, uint time,
        Exception ex, params (string Key, object Value)[] extra)
    {
        Write(LogEventLevel.Error, evId, time, ex.Message, ex, extra);
        return ex;
    }
}
