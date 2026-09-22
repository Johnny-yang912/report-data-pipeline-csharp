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

app.MapControllers();

app.Run();
