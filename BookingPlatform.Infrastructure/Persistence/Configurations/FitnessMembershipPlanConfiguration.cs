using BookingPlatform.Domain.Fitness;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class FitnessMembershipPlanConfiguration : IEntityTypeConfiguration<FitnessMembershipPlan>
{
    public void Configure(EntityTypeBuilder<FitnessMembershipPlan> builder)
    {
        builder.ToTable("fitness_membership_plans");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.TotalSessions);

        builder.Property(x => x.WeeklySessionLimit);

        builder.Property(x => x.DefaultValidityDays)
            .HasDefaultValue(30)
            .IsRequired();

        builder.Property(x => x.Price)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasMaxLength(10)
            .HasDefaultValue("RSD")
            .IsRequired();

        builder.Property(x => x.UnusedSessionsCarryOver)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.DisplayOrder)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.Note)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.BusinessId);
        builder.HasIndex(x => x.FitnessClassTypeId);
        builder.HasIndex(x => new { x.BusinessId, x.IsActive });
        builder.HasIndex(x => new { x.BusinessId, x.DisplayOrder });

        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FitnessClassType)
            .WithMany()
            .HasForeignKey(x => x.FitnessClassTypeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}