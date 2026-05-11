using BookingPlatform.Domain.Scheduling;
using BookingPlatform.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class StaffScheduleOverrideConfiguration : IEntityTypeConfiguration<StaffScheduleOverride>
{
    public void Configure(EntityTypeBuilder<StaffScheduleOverride> builder)
    {
        builder.ToTable("staff_schedule_overrides");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Date).IsRequired();
        builder.Property(x => x.OverrideType).IsRequired();
        builder.Property(x => x.IsDayOff).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.HasIndex(x => new { x.StaffMemberId, x.Date });

        builder.HasOne<StaffMember>()
            .WithMany()
            .HasForeignKey(x => x.StaffMemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}