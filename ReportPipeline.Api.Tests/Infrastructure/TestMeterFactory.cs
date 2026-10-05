using System.Diagnostics.Metrics;

public class TestMeterFactory : IMeterFactory
{
    public Meter Create(MeterOptions options) => new Meter(options);
    public void Dispose() { }
}
