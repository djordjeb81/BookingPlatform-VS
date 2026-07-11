using BookingPlatform.Domain.Fitness;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class FitnessMemberConfiguration : IEntityTypeConfiguration<FitnessMember>
{
    public void Configure(EntityTypeBuilder<FitnessMember> builder)
    {
        builder.ToTable("fitness_members");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Phone)
            .HasMaxLength(80);

        builder.Property(x => x.Email)
            .HasMaxLength(200);

        builder.Property(x => x.MemberCode)
            .HasMaxLength(80);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(x => x.BusinessId);

        builder.HasIndex(x => x.CustomerProfileId);

        builder.HasIndex(x => x.BusinessCustomerId);

        builder.HasIndex(x => x.AppUserId);

        builder.HasIndex(x => new { x.BusinessId, x.MemberCode });

        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CustomerProfile)
            .WithMany()
            .HasForeignKey(x => x.CustomerProfileId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.BusinessCustomer)
            .WithMany()
            .HasForeignKey(x => x.BusinessCustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.AppUser)
            .WithMany()
            .HasForeignKey(x => x.AppUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}