using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

public class RawProcessor
{
    private readonly AppDbContext _db;
    public RawProcessor(AppDbContext db) => _db = db;

    // Web 預設：屬性名不分大小寫、數字可從字串讀取（"36" → 36）
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // 預設會把中文轉成 \uXXXX，這個設定讓存進 DB 的內容保持可讀
    private static readonly JsonSerializerOptions ExtraJsonOpts = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // 搶占：只有 pending 的那一筆能被改成 processing，回傳 1 = 搶到，0 = 被別人搶走或不存在
    public async Task<int> TryClaimRawAsync(int rawId)
    {
        var claimed = await _db.Raws
            .Where(r => r.Id == rawId && r.Status == "pending")
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, "processing")
                .SetProperty(r => r.ClaimedAt, DateTime.UtcNow));
        return claimed;
    }

    public async Task<Report?> ProcessAsync(int rawId)
    {
        var claimed = await TryClaimRawAsync(rawId);
        if (claimed == 0)
            return null;

        _db.ChangeTracker.Clear();

        try
        {
            // 1. 讀 Raw
            var raw = await _db.Raws.FindAsync(rawId)
                ?? throw new InvalidOperationException($"Raw {rawId} not found");

            // 2. 拆包：解析失敗 = 連形狀都沒有，無法產生 Report → Raw 標 error，不丟例外
            var payload = TryDeserialize(raw.Payload, out var parseError);
            if (payload is null)
            {
                raw.Status = "error";
                raw.ErrorMessage = parseError;
                await _db.SaveChangesAsync();
                return null;
            }

            // 3. Clean：必填、正規化、單列規則全在 Cleaner 裡
            var result = Cleaner.Clean(payload, DateTimeOffset.UtcNow);

            // 4. 轉 ODS Entity：成功失敗都轉，品質由 IsCleaned 表示
            var report = ToReport(raw.Id, result);

            // 5. 寫入 ODS + Raw 標 processed：同一次 SaveChanges，一起成功或一起失敗
            _db.Reports.Add(report);
            raw.Status = "processed";
            await _db.SaveChangesAsync();

            return report;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2627 or 2601 } sqlEx)
        {
            // 唯一鍵衝突：Raw 標 duplicate，Report 不會寫入
            await _db.Raws
                .Where(r => r.Id == rawId && r.Status == "processing")
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, "duplicate")
                    .SetProperty(r => r.ErrorMessage, $"SQL_DUPLICATE_KEY: {sqlEx.Message}"));
            return null;
        }
        catch (Exception ex)
        {
            // 系統層級錯誤：直接更新 DB，不經過 change tracker，
            // 所以上面尚未寫成功的 Report 不會被一起送出
            await _db.Raws
                .Where(r => r.Id == rawId && r.Status == "processing")
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, "error")
                    .SetProperty(r => r.ErrorMessage, ex.Message));
            throw;
        }
    }

    private static ReportPayload? TryDeserialize(string json, out string? error)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<ReportPayload>(json, JsonOpts);
            error = payload is null ? "JSON_NULL_PAYLOAD" : null;
            return payload;
        }
        catch (JsonException ex)
        {
            error = $"JSON_PARSE_ERROR: {ex.Message}";
            return null;
        }
    }

    // 必填失敗時 result.Payload 是原始資料，string 可能為 null，所以需要 ?? string.Empty
    private static Report ToReport(int rawId, CleanResult result)
    {
        var p = result.Payload;
        return new Report
        {
            RawId = rawId,
            ReportId = p.ReportId ?? string.Empty,
            WorkOrderNo = p.WorkOrderNo ?? string.Empty,
            ItemCode = p.ItemCode ?? string.Empty,
            MachineId = p.MachineId ?? string.Empty,
            OperatorId = p.OperatorId ?? string.Empty,
            ReportTime = result.ReportTime?.ToUniversalTime(),
            QuantityOK = p.QuantityOK,
            QuantityNG = p.QuantityNG,
            NGCode = p.NGCode,
            Shift = p.Shift ?? string.Empty,
            IsCleaned = result.IsValid,
            CleanErrorMessage = result.ErrorMessage,
            IsSchemaDrift = result.IsSchemaDrift,
            SchemaDriftMessage = result.SchemaDriftMessage,
            UnmappedFields = p.Extra is { Count: > 0 } ? JsonSerializer.Serialize(p.Extra, ExtraJsonOpts) : null
        };
    }
}