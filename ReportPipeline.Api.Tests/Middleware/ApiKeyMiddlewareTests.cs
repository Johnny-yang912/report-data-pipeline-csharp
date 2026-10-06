using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

public class ApiKeyMiddlewareTests
{
    private static readonly List<ApiKeyEntry> Keys =
    [
        new ApiKeyEntry { Key = "test-key", ClientId = "CNC-03" }
    ];

    [Fact]
    public async Task ValidKey_CallsNext_AndSetsClientId()
    {
        bool nextCalled = false;

        Task FakeNext(HttpContext ctx)
        {
            nextCalled = true;          // 門被打開了，做個記號
            return Task.CompletedTask;  // 規定要回傳 Task，給一個「已經完成」的
        }
        var next = new RequestDelegate(FakeNext);

        var middleware = new ApiKeyMiddleware(next, Keys, NullLogger<ApiKeyMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-API-Key"] = "test-key";

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal("CNC-03", context.Items[ApiKeyMiddleware.ClientIdItemKey]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("wrong-key")]
    public async Task InvalidKey_Returns401(string? header)
    {
        bool nextCalled = false;
        Task FakeNext(HttpContext ctx)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }
        var next = new RequestDelegate(FakeNext);

        var middleware = new ApiKeyMiddleware(next, Keys, NullLogger<ApiKeyMiddleware>.Instance);
        
        var context = new DefaultHttpContext();

        if (header is not null)
            context.Request.Headers["X-API-Key"] = header;

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(401, context.Response.StatusCode);
    }
}
