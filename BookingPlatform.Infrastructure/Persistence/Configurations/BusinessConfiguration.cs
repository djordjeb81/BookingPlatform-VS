using BookingPlatform.Domain.Businesses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> builder)
    {
        builder.ToTable("businesses");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.Phone)
            .HasMaxLength(50);

        builder.Property(x => x.Email)
            .HasMaxLength(200);

        builder.Property(x => x.Street)
            .HasMaxLength(200);

        builder.Property(x => x.StreetNumber)
            .HasMaxLength(50);

        builder.Property(x => x.City)
            .HasMaxLength(100);

        builder.Property(x => x.PostalCode)
            .HasMaxLength(30);

        builder.Property(x => x.Country)
            .HasMaxLength(100);

        builder.Property(x => x.GooglePlaceId)
            .HasMaxLength(200);

        builder.Property(x => x.Latitude)
            .HasPrecision(9, 6);

        builder.Property(x => x.Longitude)
            .HasPrecision(9, 6);

        builder.Property(x => x.BusinessType)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.Property(x => x.SlotIntervalMin)
            .HasColumnName("slot_interval_min")
            .IsRequired();

        builder.Property(x => x.BookingMode)
    .HasConversion<int>()
    .HasDefaultValue(BookingMode.ServiceAppointment)
    .IsRequired();

        builder.HasOne(x => x.FeatureSettings)
            .WithOne(x => x.Business)
            .HasForeignKey<BusinessFeatureSettings>(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}