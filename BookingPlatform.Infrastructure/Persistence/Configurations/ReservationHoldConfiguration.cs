using BookingPlatform.Domain.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class ReservationHoldConfiguration : IEntityTypeConfiguration<ReservationHold>
{
    public void Configure(EntityTypeBuilder<ReservationHold> builder)
    {
        builder.ToTable("reservation_holds");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.HoldToken)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => x.HoldToken)
            .IsUnique();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();
    }
}