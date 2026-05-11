using BookingPlatform.Domain.Scheduling;
using BookingPlatform.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class StaffScheduleRuleConfiguration : IEntityTypeConfiguration<StaffScheduleRule>
{
    public void Configure(EntityTypeBuilder<StaffScheduleRule> builder)
    {
        builder.ToTable("staff_schedule_rules");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DayOfWeek).IsRequired();
        builder.Property(x => x.WeekType).IsRequired();
        builder.Property(x => x.SegmentType).IsRequired();
        builder.Property(x => x.StartTime).IsRequired();
        builder.Property(x => x.EndTime).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.HasIndex(x => new { x.StaffMemberId, x.DayOfWeek, x.WeekType });

        builder.HasOne<StaffMember>()
            .WithMany()
            .HasForeignKey(x => x.StaffMemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}