using BookingPlatform.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class BusinessUserMembershipConfiguration : IEntityTypeConfiguration<BusinessUserMembership>
{
    public void Configure(EntityTypeBuilder<BusinessUserMembership> builder)
    {
        builder.ToTable("business_user_memberships");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(x => new { x.BusinessId, x.AppUserId })
            .IsUnique();

        builder.HasOne(x => x.Business)
            .WithMany(x => x.UserMemberships)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.AppUser)
            .WithMany(x => x.BusinessMemberships)
            .HasForeignKey(x => x.AppUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}