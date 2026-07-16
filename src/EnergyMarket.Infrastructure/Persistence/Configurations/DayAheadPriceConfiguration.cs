using EnergyMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyMarket.Infrastructure.Persistence.Configurations;

public sealed class DayAheadPriceConfiguration : IEntityTypeConfiguration<DayAheadPrice>
{
    public void Configure(EntityTypeBuilder<DayAheadPrice> builder)
    {
        builder.ToTable("DayAheadPrices", t =>
        {
            t.HasCheckConstraint("CK_DayAheadPrices_Price_Range", "[Price] IS NULL OR ([Price] >= -500 AND [Price] <= 4000)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.PriceZoneId).IsRequired();

        builder.Property<DateTimeOffset>("_periodStartUtc").HasColumnName("PeriodStartUtc").IsRequired();
        builder.Property<int>("_periodDurationMinutes").HasColumnName("PeriodDurationMinutes").IsRequired();

        builder.Property(x => x.Price).HasColumnType("decimal(10,2)").IsRequired(false);
        builder.Property(x => x.SourceRetrievedAtUtc).IsRequired();

        builder.HasOne<PriceZone>().WithMany().HasForeignKey(x => x.PriceZoneId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(nameof(DayAheadPrice.PriceZoneId), "_periodStartUtc")
            .IsUnique()
            .HasDatabaseName("UX_DayAheadPrices_Zone_Period");
    }
}
