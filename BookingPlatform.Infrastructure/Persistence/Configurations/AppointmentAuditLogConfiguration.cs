using BookingPlatform.Domain.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class AppointmentAuditLogConfiguration : IEntityTypeConfiguration<AppointmentAuditLog>
{
    public void Configure(EntityTypeBuilder<AppointmentAuditLog> builder)
    {
        builder.ToTable("appointment_audit_logs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActionType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Message)
            .HasMaxLength(2000);

        builder.Property(x => x.OldValuesJson)
            .HasColumnType("text");

        builder.Property(x => x.NewValuesJson)
            .HasColumnType("text");

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.AppointmentId);
    }
}