using EnergyMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnergyMarket.Infrastructure.Persistence;

public sealed class EnergyMarketDbContext : DbContext
{
    public EnergyMarketDbContext(DbContextOptions<EnergyMarketDbContext> options) : base(options) { }

    public DbSet<DayAheadPrice> DayAheadPrices => Set<DayAheadPrice>();
    public DbSet<PriceZone> PriceZones => Set<PriceZone>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EnergyMarketDbContext).Assembly);
    }
}
