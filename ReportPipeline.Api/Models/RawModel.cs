using Microsoft.EntityFrameworkCore;

public class Raw
{
    public int Id { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow; // UTC，寫入DB時自動生成

    public string Status { get; set; } = "pending"; //pending, processing, processed, error,duplicate
    public string? ErrorMessage { get; set; } //若處理失敗，寫入錯誤訊息

    public DateTime? ClaimedAt { get; set; }  // UTC，搶佔成功時寫入

    public string ReportId { get; set; } = string.Empty; //系統攤平欄位，方便追蹤
    public string Payload { get; set; } = string.Empty; //整包原文
    
}
