using BookingPlatform.Domain.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class ServiceResourceRequirementConfiguration : IEntityTypeConfiguration<ServiceResourceRequirement>
{
    public void Configure(EntityTypeBuilder<ServiceResourceRequirement> builder)
    {
        builder.ToTable("service_resource_requirements");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ServiceId)
            .IsRequired();

        builder.Property(x => x.ResourceId)
            .IsRequired();

        builder.Property(x => x.IsRequired)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => new { x.ServiceId, x.ResourceId })
            .IsUnique();
    }
}