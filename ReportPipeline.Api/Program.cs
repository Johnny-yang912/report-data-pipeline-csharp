using Microsoft.EntityFrameworkCore;
using System.Threading.Channels;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(); // 註冊 Swagger 生成器

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
}

var apiKeys = builder.Configuration
    .GetSection("ApiKeys")
    .Get<List<ApiKeyEntry>>();

if (apiKeys is null || apiKeys.Count == 0)
{
    throw new InvalidOperationException("No API keys configured in 'ApiKeys'.");
}

Console.WriteLine($"[auth] 已載入 {apiKeys.Count} 把 API key");

builder.Services.AddSingleton<IReadOnlyList<ApiKeyEntry>>(apiKeys);

builder.Services.AddScoped<RawProcessor>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddSingleton(Channel.CreateBounded<int>(
    new BoundedChannelOptions(100)
    {
        FullMode = BoundedChannelFullMode.DropWrite
    }));
builder.Services.AddSingleton(sp => sp.GetRequiredService<Channel<int>>().Reader);
builder.Services.AddSingleton(sp => sp.GetRequiredService<Channel<int>>().Writer);

builder.Services.AddHostedService<ChannelWorker>();
builder.Services.AddHostedService<ScanWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();   // 確保產生 Swagger JSON
    app.UseSwaggerUI(); // 渲染出 Swagger UI 畫面
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseMiddleware<ApiKeyMiddleware>();

app.MapControllers();

app.Run();
