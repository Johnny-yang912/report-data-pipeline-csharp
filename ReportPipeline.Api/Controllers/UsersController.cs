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

    public ReportsController(AppDbContext db, ChannelWriter<int> writer)
    {
        _db = db;
        _writer = writer;
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
    public async Task<IActionResult> PostRaw([FromBody] JsonElement body)
    {
        var raw = new Raw { Payload = body.GetRawText() };
        _db.Raws.Add(raw);
        await _db.SaveChangesAsync();  //INSERT Raw

        //背景處理，拆包 → clean → 寫 ODS
        _writer.TryWrite(raw.Id);

        //快速回覆
        return Accepted(new { rawId = raw.Id, status = "pending" });

    }

    [HttpGet("raw/{id:int}")]
    public async Task<ActionResult<RawResponse>> GetRaw(int id)
    {
        var raw = await _db.Raws
            .Where(r => r.Id == id)
            .Select(r => new RawResponse(r.Id, r.Payload, r.Status, r.ErrorMessage))
            .FirstOrDefaultAsync();
        if (raw == null) return NotFound();
        return raw;
    }

}


