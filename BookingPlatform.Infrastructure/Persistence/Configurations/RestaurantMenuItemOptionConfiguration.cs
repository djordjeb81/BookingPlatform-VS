using BookingPlatform.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class RestaurantMenuItemOptionConfiguration : IEntityTypeConfiguration<RestaurantMenuItemOption>
{
    public void Configure(EntityTypeBuilder<RestaurantMenuItemOption> builder)
    {
        builder.ToTable("restaurant_menu_item_options");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.PriceDelta)
            .HasPrecision(12, 2)
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

        builder.HasIndex(x => x.OptionGroupId);

        builder.HasIndex(x => new { x.OptionGroupId, x.Name })
            .IsUnique();

        builder.HasOne(x => x.OptionGroup)
            .WithMany(x => x.Options)
            .HasForeignKey(x => x.OptionGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}