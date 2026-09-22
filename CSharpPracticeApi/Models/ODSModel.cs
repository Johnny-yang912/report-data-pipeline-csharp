using Microsoft.EntityFrameworkCore;

public class Report
{
    public int Id { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow; // UTC，寫入DB時自動生成
    public int RawId { get; set; } //對應的 Raw Id

    //事件主體
    public string ReportId { get; set; } = string.Empty;  //事件ID，ex:R001、R002...
    public string WorkOrderNo { get; set; } = string.Empty; //工單號，ex:WO-2609-001、WO-2608-117...
    public string ItemCode { get; set; } = string.Empty;  //料號，ex: AL-BRKT-01、PCB-M3-220
    public string MachineId { get; set; } = string.Empty; //機台號，ex: CNC-03、CNC-05、SMT-L1...
    public string OperatorId { get; set; } = string.Empty; //操作員ID，ex:E1042、E1088、E2011...
    public DateTimeOffset? ReportTime { get; set; }  //事件發生時間，解析失敗時為Null。與Raw的ReceivedAt不同。
    public int QuantityOK { get; set; } //良品數量
    public int QuantityNG { get; set; } //不良品數量
    public string? NGCode { get; set; } //不良代碼(qty_ng=0 時為 null)
    public string Shift { get; set; } = string.Empty; //班別，ex: A、B、C

    //清洗錯誤標籤
    public bool IsCleaned { get; set; }
    public string? CleanErrorMessage { get; set; }

    //Schema Drift 收容與標籤
    public string? UnmappedFields { get; set; } 
    public bool IsSchemaDrift { get; set; }
    public string? SchemaDriftMessage { get; set; }
}


