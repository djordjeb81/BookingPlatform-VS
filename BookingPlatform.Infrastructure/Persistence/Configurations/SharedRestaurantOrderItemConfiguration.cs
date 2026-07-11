using BookingPlatform.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class SharedRestaurantOrderItemConfiguration : IEntityTypeConfiguration<SharedRestaurantOrderItem>
{
    public void Configure(EntityTypeBuilder<SharedRestaurantOrderItem> builder)
    {
        builder.ToTable("shared_restaurant_order_items");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.BusinessNameSnapshot)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.AddedByDisplayNameSnapshot)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.OrderPersonName)
            .HasMaxLength(200);

        builder.Property(x => x.MenuItemNameSnapshot)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.UnitPriceSnapshot)
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(x => x.LineSubtotal)
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(x => x.Note)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.SharedRestaurantOrderId);

        builder.HasIndex(x => x.BusinessId);

        builder.HasIndex(x => x.MenuItemId);

        builder.HasIndex(x => x.AddedByCustomerProfileId);

        builder.HasOne(x => x.SharedRestaurantOrder)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.SharedRestaurantOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
