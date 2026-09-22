
using System.Text.Json;
using System.Text.Json.Serialization;

public record ReportPayload(string ReportId , string WorkOrderNo, string ItemCode, string MachineId, string OperatorId, string ReportTime, int QuantityOK, int QuantityNG, string? NGCode, string Shift)
{
    // JSON 裡對不上任何參數的欄位，反序列化時會被收進這裡
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
};
