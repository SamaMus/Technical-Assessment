using EnergyMarket.Api.Formatters;
using EnergyMarket.Api.Scheduling;
using EnergyMarket.Api.Time;
using EnergyMarket.Domain;
using EnergyMarket.Infrastructure;
using EnergyMarket.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDomainServices();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<ICetTimeConverter, CetTimeConverter>();
builder.Services.AddSingleton<ICsvPriceFormatter, CsvPriceFormatter>();
builder.Services.AddImportScheduling(); // Quartz.NET, hosted in-process — replaces the earlier BackgroundService approach

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Zero-friction first run: creates the SQLite schema directly from the model.
// Switch to proper `dotnet ef migrations` + `Database.Migrate()` before production.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<EnergyMarketDbContext>().Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();

public partial class Program { }
