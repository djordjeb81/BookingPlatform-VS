using BookingPlatform.Domain.Auth;
using BookingPlatform.Domain.Licensing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class LicensedDeviceConfiguration : IEntityTypeConfiguration<LicensedDevice>
{
    public void Configure(EntityTypeBuilder<LicensedDevice> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.HwidHash)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.ComputerName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.ProgramVersion)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.LicenseToken)
            .HasMaxLength(200);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(x => new { x.AppUserId, x.HwidHash })
            .IsUnique();

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.AppUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}