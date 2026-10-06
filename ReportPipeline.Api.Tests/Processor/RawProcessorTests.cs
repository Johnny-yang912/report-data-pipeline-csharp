using Microsoft.EntityFrameworkCore;

public class RawProcessorTests
{
    private const string ValidJson = """
        {
          "reportId": "R001", "workOrderNo": "WO-01", "itemCode": "ITEM-1",
          "machineId": "M01", "operatorId": "OP01",
          "reportTime": "2026-09-12 10:00:00",
          "quantityOK": 10, "quantityNG": 0, "shift": "A"
        }
        """;

    private const string MissingShiftJson = """
        {
          "reportId": "R001", "workOrderNo": "WO-01", "itemCode": "ITEM-1",
          "machineId": "M01", "operatorId": "OP01",
          "reportTime": "2026-09-12 10:00:00",
          "quantityOK": 10, "quantityNG": 0
        }
        """;


    // 塞一筆 pending 的 Raw，回傳它的 Id
    private static async Task<int> SeedRaw(SqliteTestDb testDb, string payload)
    {
        using var ctx = testDb.CreateContext();
        var raw = new Raw { Payload = payload, Status = "pending" };
        ctx.Raws.Add(raw);
        await ctx.SaveChangesAsync();
        return raw.Id;
    }

    // 用一個新的 context 執行處理
    private static async Task Process(SqliteTestDb testDb, int rawId)
    {
        using var ctx = testDb.CreateContext();
        await TestProcessorFactory.Create(ctx).ProcessAsync(rawId);
    }

    [Fact]
    public async Task ValidPayload_IsProcessed_AndReportWritten()
    {
        using var testDb = new SqliteTestDb();
        var rawId = await SeedRaw(testDb, ValidJson);

        await Process(testDb, rawId);

        using var verify = testDb.CreateContext();
        var raw = await verify.Raws.AsNoTracking().SingleAsync(r => r.Id == rawId);
        
        Assert.Equal("processed", raw.Status);
        Assert.Null(raw.ErrorCode);
        Assert.Equal(1, await verify.Reports.CountAsync());
    }

    [Theory]
    [InlineData("""{"workOrderNo": "WO-01"}""")]                    // 沒有 reportId 欄位
    [InlineData("""{"reportId": null, "workOrderNo": "WO-01"}""")]  // 值是 null
    [InlineData("""{"reportId": "   ", "workOrderNo": "WO-01"}""")] // 只有空白
    public async Task MissingReportId_IsError_AndNoReport(string payload)
    {
        using var testDb = new SqliteTestDb();
        var rawId = await SeedRaw(testDb, payload);

        await Process(testDb, rawId);

        using var verify = testDb.CreateContext();
        var raw = await verify.Raws.AsNoTracking().SingleAsync(r => r.Id == rawId);
        
        Assert.Equal("error", raw.Status);
        Assert.Equal(ErrorCodes.MissingReportId, raw.ErrorCode);
        Assert.Equal(0, await verify.Reports.CountAsync());
    }

    [Fact]
    public async Task WrongType_IsJsonParseError()
    {
        using var testDb = new SqliteTestDb();
        var rawId = await SeedRaw(testDb, """{"reportId": "R001", "quantityOK": "abc"}"""); // 語法正確，但數量給了字串

        await Process(testDb, rawId);

        using var verify = testDb.CreateContext();
        var raw = await verify.Raws.AsNoTracking().SingleAsync(r => r.Id == rawId);
        
        Assert.Equal("error", raw.Status);
        Assert.Equal(ErrorCodes.JsonParseError, raw.ErrorCode);
        Assert.Equal(0, await verify.Reports.CountAsync());
    }

    [Fact]
    public async Task PayloadNull_IsJsonNullPayloadError()
    {
        using var testDb = new SqliteTestDb();
        var rawId = await SeedRaw(testDb, "null");

        await Process(testDb, rawId);

        using var verify = testDb.CreateContext();
        var raw = await verify.Raws.AsNoTracking().SingleAsync(r => r.Id == rawId);

        Assert.Equal("error", raw.Status);
        Assert.Equal(ErrorCodes.JsonNullPayload, raw.ErrorCode);
        Assert.Equal(0, await verify.Reports.CountAsync());
    }

    [Fact]
    public async Task NoCleanJson_IsProcessed_AndReportWritten()
    {
        using var testDb = new SqliteTestDb();
        var rawId = await SeedRaw(testDb, MissingShiftJson);

        await Process(testDb, rawId);

        using var verify = testDb.CreateContext();
        var raw = await verify.Raws.AsNoTracking().SingleAsync(r => r.Id == rawId);
        var report = await verify.Reports.AsNoTracking().SingleAsync(r => r.RawId == rawId);

        Assert.Equal("processed", raw.Status);
        Assert.Null(raw.ErrorCode);
        Assert.Equal(1, await verify.Reports.CountAsync());
        Assert.False(report.IsCleaned); // 這筆資料沒有通過 Cleaner
        Assert.Equal("MISSING_SHIFT", report.CleanErrorMessage);
    }
}