using Microsoft.Extensions.Logging.Abstractions;

public static class TestProcessorFactory
{
    public static RawProcessor Create(AppDbContext db) =>
        new RawProcessor(db,
                         NullLogger<RawProcessor>.Instance,
                         new PipelineMetrics(new TestMeterFactory()));
}