using BookingPlatform.Domain.Fitness;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class FitnessMembershipPaymentConfiguration : IEntityTypeConfiguration<FitnessMembershipPayment>
{
    public void Configure(EntityTypeBuilder<FitnessMembershipPayment> builder)
    {
        builder.ToTable("fitness_membership_payments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasMaxLength(10)
            .HasDefaultValue("RSD")
            .IsRequired();

        builder.Property(x => x.PeriodStartDate)
            .IsRequired();

        builder.Property(x => x.PeriodEndDate)
            .IsRequired();

        builder.Property(x => x.PaidAtUtc)
            .IsRequired();

        builder.Property(x => x.PaymentMethod)
            .HasMaxLength(100);

        builder.Property(x => x.Note)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.BusinessId);

        builder.HasIndex(x => x.FitnessMemberId);

        builder.HasIndex(x => x.CreatedByUserId);

        builder.HasIndex(x => new { x.FitnessMemberId, x.PeriodStartDate, x.PeriodEndDate });

        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FitnessMember)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.FitnessMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}