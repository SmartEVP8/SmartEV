namespace Engine.Utils;

using System.Diagnostics;
using System.Text;

/// <summary>
/// Collects and displays performance metrics for named sections of code.
/// Can be shared across handlers to aggregate timings into a single table.
/// </summary>
/// <param name="title">The title displayed in the printed metrics table.</param>
public class PerformanceMetrics(string title = "Performance Metrics")
{
    private record Section
    {
        public uint Count;
        public readonly List<double> Timings = [];
    }

    private readonly Dictionary<string, Section> _sections = [];
    private readonly Stopwatch _wallClock = Stopwatch.StartNew();
    private int _totalCount;

    /// <summary>
    /// Gets the total count of metrics collected.
    /// </summary>
    public int TotalCount => _totalCount;

    /// <summary>
    /// Records a single timing sample for the given section name.
    /// </summary>
    /// <param name="section">The name of the section to record.</param>
    /// <param name="ms">The elapsed time in milliseconds.</param>
    public void Record(string section, double ms)
    {
        if (!_sections.TryGetValue(section, out var s))
            _sections[section] = s = new Section();
        s.Count++;
        s.Timings.Add(ms);
        Interlocked.Increment(ref _totalCount);
    }

    /// <summary>
    /// Times the given function and records it under the given section name.
    /// </summary>
    /// <param name="section">The name of the section to record.</param>
    /// <param name="func">The function to time.</param>
    /// <returns>The value returned by the function.</returns>
    public T Measure<T>(string section, Func<T> func)
    {
        var sw = Stopwatch.StartNew();
        var result = func();
        sw.Stop();
        Record(section, sw.Elapsed.TotalMilliseconds);
        return result;
    }

    /// <summary>
    /// Async version of <see cref="Measure{T}"/> for non-returning tasks.
    /// </summary>
    /// <param name="section">The name of the section to record.</param>
    /// <param name="action">The async action to time.</param>
    public async Task MeasureAsync(string section, Func<Task> action)
    {
        var sw = Stopwatch.StartNew();
        await action();
        sw.Stop();
        Record(section, sw.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// Async version of <see cref="Measure{T}"/>.
    /// </summary>
    /// <param name="section">The name of the section to record.</param>
    /// <param name="func">The async function to time.</param>
    /// <returns>The value returned by the function.</returns>
    public async Task<T> MeasureAsync<T>(string section, Func<Task<T>> func)
    {
        var sw = Stopwatch.StartNew();
        var result = await func();
        sw.Stop();
        Record(section, sw.Elapsed.TotalMilliseconds);
        return result;
    }

    /// <summary>
    /// Resets all collected metrics.
    /// </summary>
    public void Reset()
    {
        _sections.Clear();
        _totalCount = 0;
    }

    private static double Percentile(List<double> sorted, double p)
    {
        if (sorted.Count == 0) return 0;
        var index = p / 100.0 * (sorted.Count - 1);
        var lower = (int)Math.Floor(index);
        var upper = Math.Min(lower + 1, sorted.Count - 1);
        return sorted[lower] + (index - lower) * (sorted[upper] - sorted[lower]);
    }

    /// <summary>
    /// Prints the current metrics table to the console, overwriting in place to avoid flicker.
    /// </summary>
    /// <param name="context">Optional context string appended to the title row.</param>
    public void Print(string context = "")
    {
        const int colNum = 9;

        // Derive colName dynamically so long section names never overflow.
        var colName = _sections.Count > 0 ? Math.Max(36, _sections.Keys.Max(k => k.Length) + 2) : 36;

        var header = $"{"Section".PadRight(colName)} {"Count",colNum} {"Total ms",colNum} {"% Time",colNum} {"Avg ms",colNum} {"p50 ms",colNum} {"p95 ms",colNum} {"p99 ms",colNum} {"Max ms",colNum}";
        var totalWidth = header.Length;

        // Unify the line length variable to prevent misalignment.
        var border = new string('─', totalWidth + 2);

        // Set a fixed string width wider than the table to overwrite trailing ghost characters.
        var eraseWidth = totalWidth + 20;
        string ClearRight(string input) => input.PadRight(eraseWidth);

        var grandTotal = _sections
            .Where(x => !x.Key.Contains('.'))
            .SelectMany(x => x.Value.Timings)
            .Sum();

        var titleRow = (title + (context.Length > 0 ? " — " + context : string.Empty)).PadRight(totalWidth);

        var sb = new StringBuilder();
        sb.AppendLine(ClearRight($"┌{border}┐"));
        sb.AppendLine(ClearRight($"│ {titleRow} │"));
        sb.AppendLine(ClearRight($"│ {("Total recorded: " + _totalCount).PadRight(totalWidth)} │"));
        sb.AppendLine(ClearRight($"│ {("Wall clock: " + _wallClock.Elapsed.ToString(@"hh\:mm\:ss\.fff")).PadRight(totalWidth)} │"));
        sb.AppendLine(ClearRight($"├{border}┤"));
        sb.AppendLine(ClearRight($"│ {header} │"));
        sb.AppendLine(ClearRight($"├{border}┤"));

        // Group by top-level name (before first '.') to keep parents next to children
        var ordered = _sections
            .OrderBy(x => x.Key.Contains('.') ? x.Key[..x.Key.IndexOf('.')] : x.Key)
            .ThenBy(x => x.Key.Contains('.') ? 1 : 0)
            .ThenBy(x => x.Key);

        foreach (var (name, section) in ordered)
        {
            var isChild = name.Contains('.');
            var displayName = isChild ? "  └─ " + name[(name.IndexOf('.') + 1)..] : name;
            var paddedName = displayName.PadRight(colName);

            var sorted = section.Timings.Order().ToList();
            var total = sorted.Sum();
            var pct = grandTotal > 0 ? total / grandTotal * 100 : 0;
            var avg = sorted.Count > 0 ? total / sorted.Count : 0;
            var p50 = Percentile(sorted, 50);
            var p95 = Percentile(sorted, 95);
            var p99 = Percentile(sorted, 99);
            var max = sorted.Count > 0 ? sorted[^1] : 0;

            var row = $"│ {paddedName} {section.Count,colNum} {total,colNum:F2} {pct,8:F1}% {avg,colNum:F3} {p50,colNum:F3} {p95,colNum:F3} {p99,colNum:F3} {max,colNum:F3} │";
            sb.AppendLine(ClearRight(row));
        }

        sb.AppendLine(ClearRight($"└{border}┘"));

        // Print empty padded lines to erase leftover visual rows if the underlying dictionary shrinks in size
        for (int i = 0; i < 5; i++)
        {
            sb.AppendLine(new string(' ', eraseWidth));
        }

        Console.SetCursorPosition(0, 0);
        Console.Write(sb);
    }
}

public static class PerformanceMetricsExtensions
{
    public static async Task<T> MeasureAsync<T>(this Task<T> task, string section, PerformanceMetrics? metrics)
    {
        if (metrics is null) return await task;
        var sw = Stopwatch.StartNew();
        var result = await task;
        metrics.Record(section, sw.Elapsed.TotalMilliseconds);
        return result;
    }
}
