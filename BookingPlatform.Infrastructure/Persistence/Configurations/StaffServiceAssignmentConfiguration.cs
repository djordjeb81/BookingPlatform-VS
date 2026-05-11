using BookingPlatform.Domain.Services;
using BookingPlatform.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class StaffServiceAssignmentConfiguration : IEntityTypeConfiguration<StaffServiceAssignment>
{
    public void Configure(EntityTypeBuilder<StaffServiceAssignment> builder)
    {
        builder.ToTable("staff_service_assignments");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.StaffMemberId, x.ServiceId })
            .IsUnique();

        builder.HasOne<StaffMember>()
            .WithMany()
            .HasForeignKey(x => x.StaffMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Service>()
            .WithMany()
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}