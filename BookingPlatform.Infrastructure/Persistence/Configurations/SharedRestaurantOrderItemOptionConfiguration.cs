using BookingPlatform.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class SharedRestaurantOrderItemOptionConfiguration : IEntityTypeConfiguration<SharedRestaurantOrderItemOption>
{
    public void Configure(EntityTypeBuilder<SharedRestaurantOrderItemOption> builder)
    {
        builder.ToTable("shared_restaurant_order_item_options");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OptionNameSnapshot)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.PriceDeltaSnapshot)
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(x => x.AmountMode)
            .HasConversion<int>();

        builder.HasIndex(x => x.SharedRestaurantOrderItemId);

        builder.HasIndex(x => x.RestaurantAddonId);

        builder.HasOne(x => x.SharedRestaurantOrderItem)
            .WithMany(x => x.Options)
            .HasForeignKey(x => x.SharedRestaurantOrderItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
