using System.Threading.Channels;

public class ChannelWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ChannelReader<int> _reader;
    private readonly ILogger<ChannelWorker> _logger;

    public ChannelWorker(IServiceScopeFactory scopeFactory, ChannelReader<int> reader, ILogger<ChannelWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _reader = reader;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var rawId in _reader.ReadAllAsync(stoppingToken))
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<RawProcessor>();

                try
                {
                    await processor.ProcessAsync(rawId);
                    _logger.LogDebug("Raw {RawId} processed", rawId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Raw {RawId} failed in channel worker", rawId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 預期中的關機
        }

        _logger.LogInformation("Channel worker stopped");
    }
}