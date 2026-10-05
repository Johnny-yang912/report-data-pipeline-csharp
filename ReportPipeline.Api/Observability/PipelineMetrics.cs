using System.Diagnostics.Metrics;

public class PipelineMetrics
{
    public const string MeterName = "ReportPipeline";

    private readonly Counter<long> _rawOutcomes;

    public PipelineMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _rawOutcomes = meter.CreateCounter<long>(
            "reportpipeline.raw.outcomes",
            unit: "{raw}",
            description: "Raw rows reaching a terminal status");
    }

    public void RecordOutcome(string status, string? errorCode, string? source)
    {
        _rawOutcomes.Add(1,
            new KeyValuePair<string, object?>("status", status),
            new KeyValuePair<string, object?>("error_code", errorCode ?? "none"),
            new KeyValuePair<string, object?>("source", source ?? "unknown"));
    }
}