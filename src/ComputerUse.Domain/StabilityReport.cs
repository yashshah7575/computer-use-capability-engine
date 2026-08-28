namespace ComputerUse.Domain;

public sealed class StabilityReport
{
    public int RunCount { get; set; }
    public Dictionary<string, int> OutcomeCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public double PassRate { get; set; }
    public bool AnyDegradations { get; set; }

    public static StabilityReport From(IReadOnlyList<ExecutionResult> results)
    {
        var report = new StabilityReport { RunCount = results.Count };
        foreach (var result in results)
        {
            var key = result.Kind.ToString();
            report.OutcomeCounts[key] = report.OutcomeCounts.GetValueOrDefault(key) + 1;
            if (result.Degradations.Count > 0)
                report.AnyDegradations = true;
        }

        var pass = results.Count(r => r.Kind is ResultKind.Success or ResultKind.Recoverable);
        report.PassRate = results.Count == 0 ? 0 : (double)pass / results.Count;
        return report;
    }
}
