using BookingPlatform.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class RestaurantOrderItemConfiguration : IEntityTypeConfiguration<RestaurantOrderItem>
{
    public void Configure(EntityTypeBuilder<RestaurantOrderItem> builder)
    {
        builder.ToTable("restaurant_order_items");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.MenuItemNameSnapshot)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.UnitPriceSnapshot)
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(x => x.Quantity)
            .IsRequired();

        builder.Property(x => x.LineSubtotal)
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(x => x.Note)
            .HasMaxLength(1000);

        builder.HasOne(x => x.OrderGuest)
    .WithMany(x => x.Items)
    .HasForeignKey(x => x.OrderGuestId)
    .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.OrderGuestId);

        builder.HasIndex(x => x.OrderId);

        builder.HasIndex(x => x.MenuItemId);

        builder.HasOne(x => x.Order)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.MenuItem)
            .WithMany()
            .HasForeignKey(x => x.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}