using BookingPlatform.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class SharedRestaurantOrderConfiguration : IEntityTypeConfiguration<SharedRestaurantOrder>
{
    public void Configure(EntityTypeBuilder<SharedRestaurantOrder> builder)
    {
        builder.ToTable("shared_restaurant_orders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OwnerDisplayNameSnapshot)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(200);

        builder.Property(x => x.Note)
            .HasMaxLength(1000);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(x => x.OwnerCustomerProfileId);

        builder.HasIndex(x => x.OwnerAppUserId);

        builder.HasIndex(x => x.Status);

        builder.HasIndex(x => new { x.OwnerCustomerProfileId, x.Status });
    }
}
