using System;
using System.Collections.Generic;
using System.Text;

public class CleanerTest
{
    //固定時間
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.FromHours(8));

    //合法資料
    private static ReportPayload Valid() => new
        (
        ReportId: "R001", 
        WorkOrderNo: "WO-01", 
        ItemCode: "ITEM-1",
        MachineId: "M01", 
        OperatorId: "OP01",
        ReportTime: "2026-09-12 10:00:00",
        QuantityOK: 10, 
        QuantityNG: 0, 
        NGCode: null, 
        Shift: "A"
        );

    [Fact]
    public void ValidPayload_IsValid()
    {
        var result = Cleaner.Clean(Valid(), Now);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void NegativeQuantityOK_ReturnsQtyNegative()
    {
        var p = Valid() with { QuantityOK = -1 };
        var result = Cleaner.Clean(p, Now);
        Assert.False(result.IsValid);
        Assert.Contains("QTY_NEGATIVE", result.Errors);
    }

    [Fact]
    public void NegativeQuantityZero_ReturnsQtyZero()
    {
        var p = Valid() with { QuantityOK = 0, QuantityNG = 0 };
        var result = Cleaner.Clean(p, Now);
        Assert.False(result.IsValid);
        Assert.Contains("QTY_ZERO", result.Errors);
    }

    [Fact]
    public void NGQuantityWithoutNGCode_ReturnsNGCodeUnexpected()
    {
        var p = Valid() with { QuantityNG = 1, NGCode = null };
        var result = Cleaner.Clean(p, Now);
        Assert.False(result.IsValid);
        Assert.Contains("NG_CODE_REQUIRED", result.Errors);
    }

    [Fact]
    public void NGCodeWithoutNGQuantity_ReturnsNGCodeRequired()
    {
        var p = Valid() with { QuantityNG = 0, NGCode = "NG01" };
        var result = Cleaner.Clean(p, Now);
        Assert.False(result.IsValid);
        Assert.Contains("NG_CODE_UNEXPECTED", result.Errors);
    }

    [Fact]
    public void FutureReportTime_ReturnsFutureTime()
    {
        var p = Valid() with { ReportTime = "2026-09-12 13:00:00" }; //比 Now 晚
        var result = Cleaner.Clean(p, Now);
        Assert.False(result.IsValid);
        Assert.Contains("FUTURE_TIME", result.Errors);
    }

    [Fact]
    public void Normalize_ReportId_TrimsWhitespace()
    {
        var p = Valid() with { ReportId = "  R001  " };
        var result = Cleaner.Clean(p, Now);
        Assert.Equal("R001", result.Payload.ReportId);
    }

    [Fact]
    public void Normalize_WorkOrderNo_Uppercases()
    {
        var p = Valid() with { WorkOrderNo = "wo-01" };
        var result = Cleaner.Clean(p, Now);
        Assert.Equal("WO-01", result.Payload.WorkOrderNo);
    }


    //測試班別對應，此測試僅在乎正規化
    [Theory]
    [InlineData("早班", "A")]
    [InlineData("中班", "B")]
    [InlineData("小夜", "B")]
    [InlineData("大夜", "C")]
    public void Normalize_Shift_MapsToExpected(string sh, string expected)
    {
        var p = Valid() with { Shift = sh };
        var result = Cleaner.Clean(p, Now);
        Assert.Equal(expected, result.Payload.Shift);
    }



    //測試時間偏移邊界
    [Theory]
    [InlineData("2026-09-12 12:04:00", true, null)]
    [InlineData("2026-09-12 12:05:00", true, null)]
    [InlineData("2026-09-12 12:06:00", false, "FUTURE_TIME")]
    public void TimeSkew(string reportTime, bool isValid, string? expectedError)
    {
        var p = Valid() with { ReportTime = reportTime };
        var result = Cleaner.Clean(p, Now);
        Assert.Equal(isValid, result.IsValid);

        if (expectedError is null)
        {   
            Assert.Empty(result.Errors);
        }
        else
        {
            Assert.Contains(expectedError, result.Errors);
        }
    }
}
