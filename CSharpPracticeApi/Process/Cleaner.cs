using System.Globalization;

/// <summary>
/// 清洗結果。Processor 依此決定寫入 Report 的內容與 IsCleaned / CleanErrorMessage。
/// </summary>
public sealed record CleanResult(
    ReportPayload Payload,          // 通過第 1 層：正規化後的資料；未通過：原始資料
    DateTimeOffset? ReportTime,     // 時間解析成功才有值
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> SchemaDrift)
{
    public bool IsValid => Errors.Count == 0;
    public string? ErrorMessage => IsValid ? null : string.Join(",", Errors);

    public bool IsSchemaDrift => SchemaDrift.Count > 0;

    public string? SchemaDriftMessage => IsSchemaDrift ? string.Join(",", SchemaDrift) : null;
}

/// <summary>
/// 純清洗邏輯：不碰 DB、不做反序列化、不讀主檔。
/// 第 1 層 必填 → 第 2 層 正規化 → 第 3 層 單列規則。前一層失敗就停。
/// </summary>
public static class Cleaner
{
    private static readonly TimeSpan TaipeiOffset = TimeSpan.FromHours(8);
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ShiftGrace = TimeSpan.FromMinutes(30);

    private static readonly Dictionary<string, string> ShiftMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A"] = "A",
        ["早班"] = "A",
        ["B"] = "B",
        ["中班"] = "B",
        ["小夜"] = "B",
        ["C"] = "C",
        ["大夜"] = "C",
    };

    // 有時區：照用（'Z' 視為 UTC）
    private static readonly string[] OffsetFormats =
    [
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
    ];

    // 無時區：視為台北時間（不可交給機器時區決定）
    private static readonly string[] LocalFormats =
    [
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss",
    ];

    // ───────────────────────── 入口 ─────────────────────────

    public static CleanResult Clean(ReportPayload raw, DateTimeOffset now)
    {
        // Schema drift：與資料是否合格無關的標記，不參與提早離開，每個出口都帶上
        var drift = CheckSchemaDrift(raw);

        // 第 1 層：必填
        var missing = CheckRequired(raw);
        if (missing.Count > 0)
            return new CleanResult(raw, null, missing, drift);

        // 第 2 層：正規化（只改形狀，不判對錯）
        var p = Normalize(raw);

        var reportTime = ParseReportTime(p.ReportTime);
        if (reportTime is null)
            return new CleanResult(p, null, ["BAD_TIME_FORMAT"], drift);

        // 第 3 層：單列規則（錯誤全部累積）
        var errors = ValidateRow(p, reportTime.Value, now);
        return new CleanResult(p, reportTime, errors, drift);
    }

    // ───────────────────────── Schema drift ─────────────────────────
    // Extra 裡的 key 就是來源多送的欄位名稱
    private static List<string> CheckSchemaDrift(ReportPayload p) =>
        p.Extra?.Keys.Order().ToList() ?? [];

    // ───────────────────────── 第 1 層：必填 ─────────────────────────

    // JSON 缺欄位時，非 nullable 的 string 仍可能是 null
    private static List<string> CheckRequired(ReportPayload p)
    {
        (string? value, string code)[] required =
        [
            (p.ReportId,    "MISSING_REPORT_ID"),
            (p.WorkOrderNo, "MISSING_WORK_ORDER"),
            (p.ItemCode,    "MISSING_ITEM_CODE"),
            (p.MachineId,   "MISSING_MACHINE_ID"),
            (p.OperatorId,  "MISSING_OPERATOR_ID"),
            (p.ReportTime,  "MISSING_REPORT_TIME"),
            (p.Shift,       "MISSING_SHIFT"),
        ];

        return required
            .Where(r => string.IsNullOrWhiteSpace(r.value))
            .Select(r => r.code)
            .ToList();
    }

    // ───────────────────────── 第 2 層：正規化 ─────────────────────────

    private static ReportPayload Normalize(ReportPayload p) => p with
    {
        ReportId = p.ReportId.Trim(),
        WorkOrderNo = NormCode(p.WorkOrderNo),
        ItemCode = NormCode(p.ItemCode),
        MachineId = NormCode(p.MachineId),
        OperatorId = NormCode(p.OperatorId),
        ReportTime = p.ReportTime.Trim(),
        NGCode = string.IsNullOrWhiteSpace(p.NGCode) ? null : NormCode(p.NGCode),
        Shift = ShiftMap.TryGetValue(p.Shift.Trim(), out var s) ? s : p.Shift.Trim(),
    };

    private static string NormCode(string v) => v.Trim().ToUpperInvariant();

    private static DateTimeOffset? ParseReportTime(string s)
    {
        if (DateTimeOffset.TryParseExact(s, OffsetFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var withOffset))
            return withOffset;

        if (DateTime.TryParseExact(s, LocalFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var local))
            return new DateTimeOffset(local, TaipeiOffset);

        return null;
    }

    // ───────────────────────── 第 3 層：單列規則 ─────────────────────────

    private static List<string> ValidateRow(ReportPayload p, DateTimeOffset reportTime, DateTimeOffset now)
    {
        var errors = new List<string>();

        // 數量
        if (p.QuantityOK < 0 || p.QuantityNG < 0) errors.Add("QTY_NEGATIVE");
        else if (p.QuantityOK + p.QuantityNG == 0) errors.Add("QTY_ZERO");

        // 不良代碼與不良數的一致性
        if (p.QuantityNG > 0 && p.NGCode is null) errors.Add("NG_CODE_REQUIRED");
        if (p.QuantityNG == 0 && p.NGCode is not null) errors.Add("NG_CODE_UNEXPECTED");

        // 時間
        if (reportTime > now + ClockSkew) errors.Add("FUTURE_TIME");

        // 班別（交班後 ShiftGrace 內報上一班視為合法）
        if (p.Shift is not ("A" or "B" or "C"))
            errors.Add("SHIFT_UNKNOWN");
        else if (p.Shift != ExpectedShift(reportTime) &&
                 p.Shift != ExpectedShift(reportTime - ShiftGrace))
            errors.Add("SHIFT_TIME_MISMATCH");

        return errors;
    }

    // A 早班 08–16、B 中班 16–24、C 大夜 00–08
    private static string ExpectedShift(DateTimeOffset t)
    {
        var hour = t.ToOffset(TaipeiOffset).Hour;
        return hour switch
        {
            >= 8 and < 16 => "A",
            >= 16 => "B",
            _ => "C",
        };
    }
}