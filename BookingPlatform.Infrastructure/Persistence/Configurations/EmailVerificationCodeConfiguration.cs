using BookingPlatform.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class EmailVerificationCodeConfiguration : IEntityTypeConfiguration<EmailVerificationCode>
{
    public void Configure(EntityTypeBuilder<EmailVerificationCode> builder)
    {
        builder.ToTable("email_verification_codes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.NormalizedEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.CodeHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Purpose)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ExpiresAtUtc)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.NormalizedEmail,
            x.Purpose,
            x.ExpiresAtUtc
        });

        builder.HasIndex(x => new
        {
            x.NormalizedEmail,
            x.Purpose,
            x.UsedAtUtc
        });
    }
}