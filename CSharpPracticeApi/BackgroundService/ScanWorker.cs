using Microsoft.EntityFrameworkCore; // <- 必要

public class ScanWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ScanWorker(IServiceScopeFactory scopeFactory)   // 這個是 Singleton，安全
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var processor = scope.ServiceProvider.GetRequiredService<RawProcessor>();

                var threshold = DateTime.UtcNow.AddMinutes(-10);

                int? rawId = null; // 新增：在 try 外保存 Id，供 catch 使用

                try {

                var recovered = await db.Raws
                    .Where(r => r.Status == "processing" && r.ClaimedAt < threshold)
                    .ExecuteUpdateAsync(r => r
                        .SetProperty(x => x.Status, "pending")
                        .SetProperty(x => x.ClaimedAt, (DateTime?)null),
                        stoppingToken);

                if (recovered > 0)
                    Console.WriteLine($"[scan] 回收 stale processing: {recovered} 筆");

                // 第二步：撈 pending（回收的自然被包含在內）
                var raw = await db.Raws
                    .Where(r => r.Status == "pending")
                    .OrderBy(r => r.Id)
                    .FirstOrDefaultAsync(stoppingToken);

                rawId = raw?.Id; // 記錄 Id，讓 catch 可用

                if (raw is null)
                {
                    await Task.Delay(30_0000, stoppingToken);
                    Console.WriteLine($"[scan] idle {DateTime.Now:HH:mm:ss}");
                    continue;
                }

                 
                    await processor.ProcessAsync(raw.Id);
                    Console.WriteLine($"[scan] 完成 rawId={raw.Id}");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;   // 關機，直接離開 while
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[scan] 失敗 rawId={rawId}: {ex.Message}");
                    await Task.Delay(5_000, stoppingToken);
                }
            }

            // 處理一筆
        } // scope Dispose -> DbContext 被釋放
    }
}
