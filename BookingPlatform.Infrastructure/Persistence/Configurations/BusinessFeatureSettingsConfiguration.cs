using BookingPlatform.Domain.Businesses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class BusinessFeatureSettingsConfiguration : IEntityTypeConfiguration<BusinessFeatureSettings>
{
    public void Configure(EntityTypeBuilder<BusinessFeatureSettings> builder)
    {
        builder.ToTable("business_feature_settings");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.BusinessId)
            .IsUnique();

        builder.Property(x => x.ServiceAppointmentsEnabled)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.TableReservationsEnabled)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.FoodOrdersEnabled)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.DrinkOrdersEnabled)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.TakeawayOrdersEnabled)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.DeliveryOrdersEnabled)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.EventHallReservationsEnabled)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.AccommodationEnabled)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.ReviewsEnabled)
            .HasDefaultValue(true)
            .IsRequired();
    }
}