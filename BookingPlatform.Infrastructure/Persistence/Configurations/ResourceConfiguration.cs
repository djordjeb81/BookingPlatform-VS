using BookingPlatform.Domain.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.ToTable("resources");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.ResourceType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.CustomerActionText)
            .HasMaxLength(200);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.Property(x => x.AllowParallelUsage)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.CreatesOccupancy)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.LayoutX)
            .HasPrecision(10, 2);

        builder.Property(x => x.LayoutY)
            .HasPrecision(10, 2);

        builder.Property(x => x.LayoutWidth)
            .HasPrecision(10, 2);

        builder.Property(x => x.LayoutHeight)
            .HasPrecision(10, 2);

        builder.Property(x => x.LayoutRotationDeg)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.LayoutShape)
            .HasConversion<int>()
            .IsRequired()
            .HasDefaultValue(LayoutShapeType.Rectangle);

        builder.Property(x => x.LayoutPointsJson)
            .HasColumnType("jsonb");

        builder.HasIndex(x => x.BusinessId);

        builder.HasIndex(x => x.ResourceGroupId);

        builder.HasIndex(x => x.RestaurantAreaId);
    }
}