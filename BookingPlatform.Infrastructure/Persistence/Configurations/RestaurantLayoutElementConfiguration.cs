
using BookingPlatform.Domain.Resources;
using BookingPlatform.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class RestaurantLayoutElementConfiguration : IEntityTypeConfiguration<RestaurantLayoutElement>
{
    public void Configure(EntityTypeBuilder<RestaurantLayoutElement> builder)
    {
        builder.ToTable("restaurant_layout_elements");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ElementType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Label)
            .HasMaxLength(150);

        builder.Property(x => x.X)
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(x => x.Y)
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(x => x.Width)
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(x => x.Height)
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(x => x.RotationDeg)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.ShapeType)
            .HasConversion<int>()
            .HasDefaultValue(LayoutShapeType.Rectangle)
            .IsRequired();

        builder.Property(x => x.PointsJson)
            .HasColumnType("jsonb");

        builder.Property(x => x.IsObstacle)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.DisplayOrder)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(x => x.RestaurantAreaId);

        builder.HasOne(x => x.RestaurantArea)
            .WithMany()
            .HasForeignKey(x => x.RestaurantAreaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}