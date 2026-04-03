using BookingPlatform.Domain.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CustomerName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.CustomerPhone)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.CustomerEmail)
            .HasMaxLength(200);

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();
    }
}