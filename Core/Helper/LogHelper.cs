using Serilog;
using Serilog.Events;

public static class LogHelper
{
    public static void Write(
        LogEventLevel level,
        int evId,
        uint time,
        string message,
        Exception? ex,
        params (string Key, object Value)[] extra)
    {
        var log = Log.ForContext("evId", evId)
                     .ForContext("Time", time);

        foreach (var (key, value) in extra)
            log = log.ForContext(key, value, destructureObjects: true);

        log.Write(level, message, ex);
    }

    public static void Info(
        int evId, uint time, string message,
        params (string Key, object Value)[] extra)
        => Write(LogEventLevel.Information, evId, time, message, null, extra);

    public static void Warn(
        int evId, uint time, string message,
        params (string Key, object Value)[] extra)
        => Write(LogEventLevel.Warning, evId, time, message, null, extra);

    public static void Verbose(
        int evId, uint time, string message,
        params (string Key, object Value)[] extra)
        => Write(LogEventLevel.Verbose, evId, time, message, null, extra);

    public static Exception Error(
        int evId, uint time,
        Exception ex, params (string Key, object Value)[] extra)
    {
        Write(LogEventLevel.Error, evId, time, ex.Message, ex, extra);
        return ex;
    }
}
