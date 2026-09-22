using Microsoft.EntityFrameworkCore;

public class TryClaimTests
{
    [Fact]
    public async Task SecondClaim_ReturnsZero()
    {
        using var testDb = new SqliteTestDb();

        // Arrange：塞資料
        int rawId;
        using (var seed = testDb.CreateContext())
        {
            var raw = new Raw { ReportId = "R001", Payload = "Test payload", Status = "pending" };
            seed.Raws.Add(raw);
            await seed.SaveChangesAsync();
            rawId = raw.Id;               // SaveChanges 之後 Id 才會被填上
        }

        using var ctxA = testDb.CreateContext();
        using var ctxB = testDb.CreateContext();
        var repoA = new RawProcessor(ctxA);
        var repoB = new RawProcessor(ctxB);

        // Act
        var first = await repoA.TryClaimRawAsync(rawId);
        var second = await repoB.TryClaimRawAsync(rawId);

        // Assert
        Assert.Equal(1, first);
        Assert.Equal(0, second);

        using var verify = testDb.CreateContext();   // 用全新的 context 讀
        var row = await verify.Raws.AsNoTracking().SingleAsync(r => r.Id == rawId);
        Assert.Equal("processing", row.Status);
        Assert.NotNull(row.ClaimedAt);
    }
}

