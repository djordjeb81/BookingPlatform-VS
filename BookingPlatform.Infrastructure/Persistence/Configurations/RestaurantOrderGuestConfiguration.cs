using BookingPlatform.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class RestaurantOrderGuestConfiguration : IEntityTypeConfiguration<RestaurantOrderGuest>
{
    public void Configure(EntityTypeBuilder<RestaurantOrderGuest> builder)
    {
        builder.ToTable("restaurant_order_guests");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(x => x.Note)
            .HasMaxLength(1000);

        builder.Property(x => x.DisplayOrder)
            .IsRequired();

        builder.HasIndex(x => x.OrderId);

        builder.HasIndex(x => new { x.OrderId, x.DisplayOrder });

        builder.HasOne(x => x.Order)
            .WithMany(x => x.Guests)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}