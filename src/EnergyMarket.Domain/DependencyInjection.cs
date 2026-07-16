using EnergyMarket.Domain.Services;
using EnergyMarket.Domain.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace EnergyMarket.Domain;

public static class DependencyInjection
{
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        services.AddScoped<IDayAheadPriceValidator, DayAheadPriceValidator>();
        services.AddScoped<DayAheadPriceImportService>();
        return services;
    }
}
