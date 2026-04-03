using BookingPlatform.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class StaffWorkingHourConfiguration : IEntityTypeConfiguration<StaffWorkingHour>
{
    public void Configure(EntityTypeBuilder<StaffWorkingHour> builder)
    {
        builder.ToTable("staff_working_hours");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DayOfWeek).IsRequired();
        builder.Property(x => x.StartTime).IsRequired();
        builder.Property(x => x.EndTime).IsRequired();
        builder.Property(x => x.IsClosed).IsRequired();

        builder.HasIndex(x => new { x.StaffMemberId, x.DayOfWeek }).IsUnique();
    }
}