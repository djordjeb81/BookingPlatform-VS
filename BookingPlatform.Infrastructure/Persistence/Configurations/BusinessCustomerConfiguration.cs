using BookingPlatform.Domain.Auth;
using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class BusinessCustomerConfiguration : IEntityTypeConfiguration<BusinessCustomer>
{
    public void Configure(EntityTypeBuilder<BusinessCustomer> builder)
    {
        builder.ToTable("business_customers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Phone)
            .HasMaxLength(50);

        builder.Property(x => x.Email)
            .HasMaxLength(200);

        builder.Property(x => x.Notes)
            .HasMaxLength(2000);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(x => x.BusinessId);

        builder.HasIndex(x => new { x.BusinessId, x.FullName });

        builder.HasIndex(x => new { x.BusinessId, x.Phone });

        builder.HasIndex(x => new { x.BusinessId, x.Email });

        builder.HasIndex(x => new { x.BusinessId, x.IsActive });
        builder.Property(x => x.RemovedFromCustomerListAtUtc);

        builder.HasOne<Business>()
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.AppUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.CustomerProfileId);

        builder.HasIndex(x => new
        {
            x.BusinessId,
            x.CustomerProfileId
        })
        .IsUnique();

        builder.HasOne(x => x.CustomerProfile)
    .WithMany()
    .HasForeignKey(x => x.CustomerProfileId)
    .OnDelete(DeleteBehavior.Restrict);
    }
}