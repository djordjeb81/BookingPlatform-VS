using BookingPlatform.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class StaffMemberConfiguration : IEntityTypeConfiguration<StaffMember>
{
    public void Configure(EntityTypeBuilder<StaffMember> builder)
    {
        builder.ToTable("staff_members");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(200);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();
    }
}