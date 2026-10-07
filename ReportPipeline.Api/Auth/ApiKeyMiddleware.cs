using System.Security.Cryptography;   // FixedTimeEquals
using System.Text;                    // Encoding


public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IReadOnlyList<ApiKeyEntry> _apiKeys;

    private readonly ILogger<ApiKeyMiddleware> _logger;

    public const string ClientIdItemKey = "SourceClientId";

    public ApiKeyMiddleware(RequestDelegate next, IReadOnlyList<ApiKeyEntry> apiKeys, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _apiKeys = apiKeys;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var requireKey = endpoint?.Metadata.GetMetadata<RequireApiKeyAttribute>();

        if (requireKey is null)
        {
            await _next(context);   // 沒有標籤，直接放行
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-API-Key", out var provided)
            || string.IsNullOrWhiteSpace(provided))
        {
            context.Response.StatusCode = 401;
            _logger.LogWarning("API key missing, remote ip {RemoteIp}",
                   context.Connection.RemoteIpAddress);
            return;
        }

        var providedBytes = Encoding.UTF8.GetBytes(provided.ToString());
        string? clientId = null;

        foreach (var entry in _apiKeys)
        {
            if (CryptographicOperations.FixedTimeEquals(providedBytes, Encoding.UTF8.GetBytes(entry.Key)))
            {
                clientId = entry.ClientId;
                break;
            }
        }

        if (clientId is null)
        {
            context.Response.StatusCode = 401;
            _logger.LogWarning("API key invalid, remote ip {RemoteIp}",
                   context.Connection.RemoteIpAddress);
            return;
        }

        context.Items[ClientIdItemKey] = clientId;

        await _next(context);
    }
}

