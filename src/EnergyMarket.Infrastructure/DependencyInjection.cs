using EnergyMarket.Domain.Repositories;
using EnergyMarket.Domain.Services;
using EnergyMarket.Infrastructure.Omie;
using EnergyMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EnergyMarket.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("EnergyMarket") ?? "Data Source=energymarket.db";
        services.AddDbContext<EnergyMarketDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IDayAheadPriceRepository, DayAheadPriceRepository>();

        services.AddOptions<OmieOptions>()
            .Bind(configuration.GetSection(OmieOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<IOmieMarginalPricesClient, OmieMarginalPricesClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<OmieOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            })
            .AddStandardResilienceHandler();

        return services;
    }
}
