using BookingPlatform.Domain.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class AppointmentChangeRequestConfiguration : IEntityTypeConfiguration<AppointmentChangeRequest>
{
    public void Configure(EntityTypeBuilder<AppointmentChangeRequest> builder)
    {
        builder.ToTable("appointment_change_requests");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RequestType)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.InitiatedBy)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(500);

        builder.Property(x => x.Message)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.Property(x => x.ExpiresAtUtc);

        builder.Property(x => x.RespondedAtUtc);

        builder.HasIndex(x => x.AppointmentId);

        builder.HasIndex(x => new { x.Status, x.ExpiresAtUtc });

        builder.HasIndex(x => new { x.AppointmentId, x.Status, x.CreatedAtUtc });
    }
}