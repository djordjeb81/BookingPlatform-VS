using BookingPlatform.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class RestaurantMenuItemConfiguration : IEntityTypeConfiguration<RestaurantMenuItem>
{
    public void Configure(EntityTypeBuilder<RestaurantMenuItem> builder)
    {
        builder.ToTable("restaurant_menu_items");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.Price)
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(x => x.SendToKitchen)
    .IsRequired()
    .HasDefaultValue(true);

        builder.Property(x => x.Currency)
            .HasMaxLength(10)
            .HasDefaultValue("RSD")
            .IsRequired();

        builder.Property(x => x.IsAvailable)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.DisplayOrder)
            .HasDefaultValue(0)
            .IsRequired();

        builder.HasIndex(x => x.BusinessId);

        builder.HasIndex(x => x.CategoryId);

        builder.HasIndex(x => new { x.BusinessId, x.CategoryId, x.Name })
            .IsUnique();

        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Category)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}