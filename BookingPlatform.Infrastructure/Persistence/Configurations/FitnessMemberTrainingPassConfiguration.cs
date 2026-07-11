using BookingPlatform.Domain.Fitness;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class FitnessMemberTrainingPassConfiguration : IEntityTypeConfiguration<FitnessMemberTrainingPass>
{
    public void Configure(EntityTypeBuilder<FitnessMemberTrainingPass> builder)
    {
        builder.ToTable("fitness_member_training_passes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PlanNameSnapshot)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.FitnessClassTypeNameSnapshot)
            .HasMaxLength(200);

        builder.Property(x => x.ValidFromDate)
            .IsRequired();

        builder.Property(x => x.ValidToDate)
            .IsRequired();

        builder.Property(x => x.TotalSessions);

        builder.Property(x => x.WeeklySessionLimit);

        builder.Property(x => x.PricePaid)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasMaxLength(10)
            .HasDefaultValue("RSD")
            .IsRequired();

        builder.Property(x => x.PaidAtUtc)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.Note)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.BusinessId);
        builder.HasIndex(x => x.FitnessMemberId);
        builder.HasIndex(x => x.FitnessMembershipPlanId);
        builder.HasIndex(x => x.FitnessClassTypeId);
        builder.HasIndex(x => new { x.BusinessId, x.ValidFromDate, x.ValidToDate });
        builder.HasIndex(x => new { x.FitnessMemberId, x.ValidFromDate, x.ValidToDate });

        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FitnessMember)
            .WithMany(x => x.TrainingPasses)
            .HasForeignKey(x => x.FitnessMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FitnessMembershipPlan)
            .WithMany(x => x.MemberTrainingPasses)
            .HasForeignKey(x => x.FitnessMembershipPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FitnessClassType)
            .WithMany()
            .HasForeignKey(x => x.FitnessClassTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(x => x.IsVoided)
    .HasDefaultValue(false)
    .IsRequired();

        builder.Property(x => x.VoidedAtUtc);

        builder.Property(x => x.VoidReason)
            .HasMaxLength(1000);

        builder.Property(x => x.VoidedByUserId);

        builder.HasIndex(x => x.IsVoided);
    }
}