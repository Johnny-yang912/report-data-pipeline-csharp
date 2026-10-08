using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Threading.Channels;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;

    private readonly ChannelWriter<int> _writer;

    private readonly ILogger<ReportsController> _logger;

    public ReportsController(AppDbContext db, ChannelWriter<int> writer, ILogger<ReportsController> logger)
    {
        _db = db;
        _writer = writer;
        _logger = logger;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReportsResponse>> GetReport(int id)
    {
        var user = await _db.Reports
            .Where(r => r.Id == id)
            .Select(
            r => new ReportsResponse(
                r.Id,
                r.ReceivedAt,
                r.RawId,
                r.ReportId,
                r.WorkOrderNo,
                r.ItemCode,
                r.MachineId,
                r.OperatorId,
                r.ReportTime,
                r.QuantityOK,
                r.QuantityNG,
                r.NGCode,
                r.Shift,
                r.IsCleaned,
                r.CleanErrorMessage,
                r.UnmappedFields,
                r.IsSchemaDrift,
                r.SchemaDriftMessage
            ))  
            .FirstOrDefaultAsync();  //尋找第一筆符合。若無資料，則回傳預設值，不會報錯。

        if (user == null) return NotFound();
        return user;
    }

    [HttpPost("raw")]
    [RequireApiKey]
    public async Task<IActionResult> PostRaw([FromBody] JsonElement body)
    {
        var clientId = HttpContext.Items[ApiKeyMiddleware.ClientIdItemKey] as string;
        var raw = new Raw { Payload = body.GetRawText(), SourceClientId = clientId };
        _db.Raws.Add(raw);
        await _db.SaveChangesAsync();  //INSERT Raw

        //背景處理，拆包 → clean → 寫 ODS
        _writer.TryWrite(raw.Id);

        //快速回覆
        return Accepted(new { rawId = raw.Id, status = "pending" });

    }


    [HttpPost("raw/{id:int}/reprocess")]
    public async Task<IActionResult> ReprocessRaw(int id)
    {
        var raw = await _db.Raws
            .Where(r => r.Id == id)
            .FirstOrDefaultAsync();
        if (raw == null) return NotFound();

        var affected = await _db.Raws
            .Where(r => r.Id == id && (r.Status == "error" || r.Status == "duplicate"))
            .ExecuteUpdateAsync(r => r
                .SetProperty(x => x.Status, "pending")
                .SetProperty(x => x.ClaimedAt, (DateTime?)null)
                .SetProperty(x => x.ErrorCode, (string?)null)
                .SetProperty(x => x.ErrorMessage, (string?)null));

        if (affected == 0)
            return Conflict("Raw is not in an error or duplicate state");

        _logger.LogWarning("reprocess raw {RawId}: previous status {Status}, error {ErrorCode}", raw.Id, raw.Status, raw.ErrorCode);

        // 寫入失敗沒關係，補撈機制會撿回 pending
        _writer.TryWrite(raw.Id);
        return Accepted(new { rawId = raw.Id});
    }


    [HttpGet("raw/{id:int}")]
    public async Task<ActionResult<RawResponse>> GetRaw(int id)
    {
        var raw = await _db.Raws
            .Where(r => r.Id == id)
            .Select(r => new RawResponse(r.Id, r.Payload,r.SourceClientId, r.Status, r.ErrorCode, r.ErrorMessage))
            .FirstOrDefaultAsync();
        if (raw == null) return NotFound();
        return raw;
    }

}


