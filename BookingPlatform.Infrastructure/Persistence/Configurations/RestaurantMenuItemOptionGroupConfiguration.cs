using BookingPlatform.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class RestaurantMenuItemOptionGroupConfiguration : IEntityTypeConfiguration<RestaurantMenuItemOptionGroup>
{
    public void Configure(EntityTypeBuilder<RestaurantMenuItemOptionGroup> builder)
    {
        builder.ToTable("restaurant_menu_item_option_groups");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.IsRequired)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.MinSelected)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.MaxSelected)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(x => x.DisplayOrder)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(x => x.MenuItemId);

        builder.HasIndex(x => new { x.MenuItemId, x.Name })
            .IsUnique();

        builder.HasOne(x => x.MenuItem)
            .WithMany(x => x.OptionGroups)
            .HasForeignKey(x => x.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}