using BookingPlatform.Domain.Fitness;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class FitnessMemberSessionDebtConfiguration : IEntityTypeConfiguration<FitnessMemberSessionDebt>
{
    public void Configure(EntityTypeBuilder<FitnessMemberSessionDebt> builder)
    {
        builder.ToTable("fitness_member_session_debts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SessionsCount)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .HasDefaultValue(FitnessMemberSessionDebtStatus.Open)
            .IsRequired();

        builder.Property(x => x.SettledAtUtc);

        builder.Property(x => x.VoidedAtUtc);

        builder.Property(x => x.VoidReason)
            .HasMaxLength(1000);

        builder.Property(x => x.Note)
            .HasMaxLength(1000);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.BusinessId);
        builder.HasIndex(x => x.FitnessMemberId);
        builder.HasIndex(x => x.FitnessSessionId);
        builder.HasIndex(x => x.FitnessClassTypeId);
        builder.HasIndex(x => x.FitnessMemberTrainingPassId);
        builder.HasIndex(x => new { x.BusinessId, x.Status });
        builder.HasIndex(x => new { x.FitnessMemberId, x.Status });

        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FitnessMember)
            .WithMany()
            .HasForeignKey(x => x.FitnessMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FitnessSession)
            .WithMany()
            .HasForeignKey(x => x.FitnessSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FitnessClassType)
            .WithMany()
            .HasForeignKey(x => x.FitnessClassTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.FitnessMemberTrainingPass)
            .WithMany(x => x.SessionDebts)
            .HasForeignKey(x => x.FitnessMemberTrainingPassId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}