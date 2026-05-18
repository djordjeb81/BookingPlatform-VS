using BookingPlatform.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class RestaurantPaymentConfiguration : IEntityTypeConfiguration<RestaurantPayment>
{
    public void Configure(EntityTypeBuilder<RestaurantPayment> builder)
    {
        builder.ToTable("restaurant_payments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount)
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasMaxLength(10)
            .HasDefaultValue("RSD")
            .IsRequired();

        builder.Property(x => x.Method)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Note)
            .HasMaxLength(1000);

        builder.Property(x => x.PaidAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.BusinessId);

        builder.HasIndex(x => x.TableSessionId);

        builder.HasIndex(x => x.Status);

        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.TableSession)
            .WithMany()
            .HasForeignKey(x => x.TableSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}