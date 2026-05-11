using BookingPlatform.Domain.Services;
using BookingPlatform.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class ServiceResourceUsageConfiguration : IEntityTypeConfiguration<ServiceResourceUsage>
{
    public void Configure(EntityTypeBuilder<ServiceResourceUsage> builder)
    {
        builder.ToTable("service_resource_usages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StartMinute)
            .IsRequired();

        builder.Property(x => x.DurationMin)
            .IsRequired();

        builder.Property(x => x.IsRequired)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne<StaffMember>()
            .WithMany()
            .HasForeignKey(x => x.StaffId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}