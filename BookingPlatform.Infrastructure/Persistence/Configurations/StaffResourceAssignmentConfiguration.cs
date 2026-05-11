using BookingPlatform.Domain.Resources;
using BookingPlatform.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class StaffResourceAssignmentConfiguration : IEntityTypeConfiguration<StaffResourceAssignment>
{
    public void Configure(EntityTypeBuilder<StaffResourceAssignment> builder)
    {
        builder.ToTable("staff_resource_assignments");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.StaffMemberId, x.ResourceId })
            .IsUnique();

        builder.HasOne<StaffMember>()
            .WithMany()
            .HasForeignKey(x => x.StaffMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Resource>()
            .WithMany()
            .HasForeignKey(x => x.ResourceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}