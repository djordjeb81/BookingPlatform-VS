using BookingPlatform.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class RestaurantOrderItemOptionConfiguration : IEntityTypeConfiguration<RestaurantOrderItemOption>
{
    public void Configure(EntityTypeBuilder<RestaurantOrderItemOption> builder)
    {
        builder.ToTable("restaurant_order_item_options");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OptionNameSnapshot)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.PriceDeltaSnapshot)
            .HasPrecision(12, 2)
            .IsRequired();

        builder.HasIndex(x => x.OrderItemId);

        builder.HasIndex(x => x.MenuItemOptionId);

        builder.HasOne(x => x.OrderItem)
            .WithMany(x => x.Options)
            .HasForeignKey(x => x.OrderItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.MenuItemOption)
            .WithMany()
            .HasForeignKey(x => x.MenuItemOptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}