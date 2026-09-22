using System.ComponentModel.DataAnnotations;

public record RawRequest(
    [Required] string Payload
);

public record RawResponse(
    int Id,
    string Payload,
    string Status,
    string? ErrorMessage
);


public record ReportsResponse(
    int Id,
    DateTime ReceivedAt,
    int RawId,
    string ReportId,
    string WorkOrderNo,
    string ItemCode,
    string MachineId,
    string OperatorId,
    DateTimeOffset? ReportTime,
    int QuantityOK,
    int QuantityNG,
    string? NGCode,
    string Shift,
    bool IsCleaned,
    string? CleanErrorMessage,
    string? UnmappedFields,
    bool IsSchemaDrift,
    string? SchemaDriftMessage
);

