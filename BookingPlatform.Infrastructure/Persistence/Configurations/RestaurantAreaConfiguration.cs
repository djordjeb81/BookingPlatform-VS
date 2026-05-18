using BookingPlatform.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class RestaurantAreaConfiguration : IEntityTypeConfiguration<RestaurantArea>
{
    public void Configure(EntityTypeBuilder<RestaurantArea> builder)
    {
        builder.ToTable("restaurant_areas");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Capacity);

        builder.Property(x => x.CanvasWidth)
            .HasDefaultValue(1000)
            .IsRequired();

        builder.Property(x => x.CanvasHeight)
            .HasDefaultValue(1000)
            .IsRequired();

        builder.Property(x => x.BoundaryPointsJson)
            .HasColumnType("jsonb");

        builder.Property(x => x.DisplayOrder)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.IsReservableAsWhole)
            .HasDefaultValue(false)
            .IsRequired();

        builder.HasIndex(x => x.BusinessId);

        builder.HasIndex(x => new { x.BusinessId, x.Name })
            .IsUnique();

        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}