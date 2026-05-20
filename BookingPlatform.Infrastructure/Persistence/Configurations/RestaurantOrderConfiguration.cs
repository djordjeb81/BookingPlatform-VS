using BookingPlatform.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class RestaurantOrderConfiguration : IEntityTypeConfiguration<RestaurantOrder>
{
    public void Configure(EntityTypeBuilder<RestaurantOrder> builder)
    {
        builder.ToTable("restaurant_orders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderDateLocal)
    .IsRequired();

        builder.Property(x => x.DailyOrderNumber)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.BusinessId,
            x.OrderDateLocal,
            x.DailyOrderNumber
        })
        .IsUnique();

        builder.HasIndex(x => new
        {
            x.BusinessId,
            x.OrderDateLocal
        });

        builder.Property(x => x.CustomerName)
            .HasMaxLength(200);

        builder.Property(x => x.CustomerPhone)
            .HasMaxLength(50);

        builder.Property(x => x.Note)
            .HasMaxLength(1000);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.OrderType)
    .HasConversion<int>()
    .IsRequired();

        builder.Property(x => x.DeliveryAddress)
            .HasMaxLength(500);

        builder.Property(x => x.DeliveryNote)
            .HasMaxLength(1000);

        builder.Property(x => x.SubtotalAmount)
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(x => x.TotalAmount)
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasMaxLength(10)
            .HasDefaultValue("RSD")
            .IsRequired();

        builder.HasIndex(x => x.BusinessId);

        builder.HasIndex(x => x.RestaurantAreaId);

        builder.HasIndex(x => x.TableResourceId);

        builder.HasIndex(x => x.TableSessionId);

        builder.HasIndex(x => x.Status);

        builder.HasIndex(x => x.OrderType);

        builder.HasIndex(x => x.RequestedPickupAtUtc);

        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RestaurantArea)
            .WithMany()
            .HasForeignKey(x => x.RestaurantAreaId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.TableSession)
            .WithMany()
            .HasForeignKey(x => x.TableSessionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}