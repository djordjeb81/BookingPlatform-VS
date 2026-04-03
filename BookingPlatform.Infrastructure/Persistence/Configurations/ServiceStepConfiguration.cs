using BookingPlatform.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class ServiceStepConfiguration : IEntityTypeConfiguration<ServiceStep>
{
    public void Configure(EntityTypeBuilder<ServiceStep> builder)
    {
        builder.ToTable("service_steps");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(x => new { x.ServiceId, x.StepOrder })
            .IsUnique();
    }
}