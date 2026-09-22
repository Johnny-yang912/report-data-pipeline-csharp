using System.Threading.Channels;

public class ChannelWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ChannelReader<int> _reader;

    public ChannelWorker(IServiceScopeFactory scopeFactory, ChannelReader<int> reader)
    {
        _scopeFactory = scopeFactory;
        _reader = reader;
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
                    Console.WriteLine($"[channel] 完成 rawId={rawId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[channel] 失敗 rawId={rawId}: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 預期中的關機
        }

        Console.WriteLine("[channel] 已退出");
    }
}