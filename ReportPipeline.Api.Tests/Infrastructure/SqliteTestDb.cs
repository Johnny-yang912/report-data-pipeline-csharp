using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

public class SqliteTestDb : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"cas_{Guid.NewGuid()}.db");

    public SqliteTestDb()
    {
        using var db = CreateContext();
        db.Database.EnsureCreated();   // 依照你的 Model 直接建表，不需要 migration
    }

    // 每呼叫一次就產生一個新的、獨立的 context
    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_path}")
            .Options;
        return new AppDbContext(options);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();  // 先釋放連線池，檔案才刪得掉
        File.Delete(_path);
    }
}
