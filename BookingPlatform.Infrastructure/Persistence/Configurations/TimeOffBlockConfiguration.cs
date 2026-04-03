using BookingPlatform.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class TimeOffBlockConfiguration : IEntityTypeConfiguration<TimeOffBlock>
{
    public void Configure(EntityTypeBuilder<TimeOffBlock> builder)
    {
        builder.ToTable("time_off_blocks");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.BlockType)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(1000);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.BusinessId);
        builder.HasIndex(x => x.StaffMemberId);
        builder.HasIndex(x => x.StartAtUtc);
        builder.HasIndex(x => x.EndAtUtc);
    }
}