using BookingPlatform.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class CustomerProfileConfiguration : IEntityTypeConfiguration<CustomerProfile>
{
    public void Configure(EntityTypeBuilder<CustomerProfile> builder)
    {
        builder.ToTable("customer_profiles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Phone)
            .HasMaxLength(50);

        builder.Property(x => x.Email)
            .HasMaxLength(256);

        builder.Property(x => x.Nickname)
            .HasMaxLength(80);

        builder.Property(x => x.AvatarUrl)
            .HasMaxLength(500);

        builder.Property(x => x.AllowUserSearch)
            .HasDefaultValue(false);

        builder.Property(x => x.AllowChatDiscovery)
            .HasDefaultValue(false);

        builder.Property(x => x.DefaultDeliveryAddress)
            .HasMaxLength(500);

        builder.Property(x => x.DefaultDeliveryCity)
            .HasMaxLength(120);

        builder.Property(x => x.DefaultDeliveryStreet)
            .HasMaxLength(200);

        builder.Property(x => x.DefaultDeliveryStreetNumber)
            .HasMaxLength(40);

        builder.Property(x => x.DefaultDeliveryApartment)
            .HasMaxLength(120);

        builder.Property(x => x.DefaultDeliveryNote)
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.AppUserId);

        // Jedan email = jedan globalni klijent.
        // Funkcionalni unique index za lower(trim(email)) dodajemo ručno u migraciji,
        // jer EF ne pravi lepo ovakav PostgreSQL index kroz standardni fluent API.
    }
}
