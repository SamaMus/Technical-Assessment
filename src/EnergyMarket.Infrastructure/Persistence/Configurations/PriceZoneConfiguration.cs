using EnergyMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyMarket.Infrastructure.Persistence.Configurations;

public sealed class PriceZoneConfiguration : IEntityTypeConfiguration<PriceZone>
{
    public void Configure(EntityTypeBuilder<PriceZone> builder)
    {
        builder.ToTable("PriceZones");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Code).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.HasData(PriceZone.Spain, PriceZone.Portugal);
    }
}
